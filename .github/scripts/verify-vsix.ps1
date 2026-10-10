# Opens a built VSIX and fails if it isn't the package we meant to ship.
# Used by CI and by the release workflow; runs anywhere pwsh does.
param(
  [Parameter(Mandatory)] [string] $Path,
  [Parameter(Mandatory)] [string] $Version
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$problems = [System.Collections.Generic.List[string]]::new()
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("vsix-" + [guid]::NewGuid().ToString('N'))
[System.IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $Path), $work)

try {
  $files = @(Get-ChildItem $work -Recurse -File | ForEach-Object { $_.FullName.Substring($work.Length + 1).Replace('\', '/') } | Sort-Object)
  Write-Host "$Path ($([math]::Round((Get-Item $Path).Length / 1KB)) KB):"
  $files | ForEach-Object { Write-Host "  $_" }

  # The DLLs, pkgdef and licence are checked through the manifest's own references below.
  foreach ($required in 'extension.vsixmanifest', 'manifest.json', 'catalog.json') {
    if ($files -notcontains $required) { $problems.Add("missing $required") }
  }

  # VS ships its own copies; bundling ours means ExcludeAssets="runtime" stopped working.
  $files | Where-Object { $_ -like 'Microsoft.VisualStudio.*' -or $_ -like 'Microsoft.ServiceHub.*' } |
    ForEach-Object { $problems.Add("$_ should not be in the package") }

  if ($files -contains 'extension.vsixmanifest') {
    [xml] $manifest = Get-Content (Join-Path $work 'extension.vsixmanifest') -Raw
    $identity = $manifest.PackageManifest.Metadata.Identity
    if ($identity.Version -ne $Version) { $problems.Add("manifest says $($identity.Version), expected $Version") }

    $licence = $manifest.PackageManifest.Metadata.License
    if ($licence -and $files -notcontains $licence) { $problems.Add("manifest licence $licence is not in the package") }

    foreach ($asset in $manifest.PackageManifest.Assets.Asset) {
      if ($files -notcontains $asset.Path.Replace('\', '/')) { $problems.Add("$($asset.Type) asset '$($asset.Path)' is not in the package") }
    }
  }

  # The snippets pkgdef points VS at this folder; an empty one means the snippets fell out of the build.
  if (-not ($files | Where-Object { $_ -like 'Snippets/TypeScript/*.snippet' })) { $problems.Add('no TypeScript snippets in Snippets/TypeScript') }

  # The site rules need the Bin reader and the metadata library it is built on.
  foreach ($dll in 'AISI.MuiLint.Site.dll', 'System.Reflection.Metadata.dll') {
    if ($files -notcontains $dll) { $problems.Add("missing $dll") }
  }

  foreach ($dll in 'AISI.MuiLint.dll', 'AISI.MuiLint.Site.dll', 'AISI.MuiLint.Vsix.dll') {
    $full = Join-Path $work $dll
    if (Test-Path $full) {
      $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($full).FileVersion
      if ($fileVersion -ne "$Version.0") { $problems.Add("$dll has file version $fileVersion, expected $Version.0") }
    }
  }

  $pkgdef = Join-Path $work 'AISI.MuiLint.Vsix.pkgdef'
  if (Test-Path $pkgdef) {
    $text = Get-Content $pkgdef -Raw
    if ($text -notmatch [regex]::Escape("""PID""=""$Version""")) { $problems.Add("pkgdef doesn't register product version $Version") }
    if ($text -notmatch 'CodeBase') { $problems.Add("pkgdef has no CodeBase, so VS can't find the package") }
  }
}
finally {
  Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($problems.Count -gt 0) {
  $problems | ForEach-Object { Write-Host "::error::$_" }
  exit 1
}

Write-Host "VSIX $Version looks right."
