using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace AISI.MuiLint.Site
{
    /// <summary>A type as a signature names it: just enough to substitute generic arguments into.</summary>
    internal abstract class TypeSig
    {
        /// <summary>This type with each type parameter replaced by the matching argument.</summary>
        public abstract TypeSig Substitute(ImmutableArray<TypeSig> args);
    }

    /// <summary>A named type, with its arguments when it is a generic instantiation.</summary>
    internal sealed class NamedSig : TypeSig
    {
        public NamedSig(string name, ImmutableArray<TypeSig> args)
        {
            Name = name;
            Args = args;
        }

        /// <summary>Gets the metadata full name, for example <c>PX.Data.PXSelect`2</c>, nested types joined with '+'.</summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type arguments. Metadata flattens them: a type nested in a generic type takes
        /// the outer type's arguments first.
        /// </summary>
        public ImmutableArray<TypeSig> Args { get; }

        public override TypeSig Substitute(ImmutableArray<TypeSig> args)
        {
            if (Args.IsEmpty)
            {
                return this;
            }

            ImmutableArray<TypeSig>.Builder substituted = ImmutableArray.CreateBuilder<TypeSig>(Args.Length);
            foreach (TypeSig arg in Args)
            {
                substituted.Add(arg.Substitute(args));
            }

            return new NamedSig(Name, substituted.MoveToImmutable());
        }

        public override string ToString()
        {
            return Args.IsEmpty ? Name : Name + "<" + string.Join(",", Args) + ">";
        }
    }

    /// <summary>A type parameter of the enclosing type, by position.</summary>
    internal sealed class ParamSig : TypeSig
    {
        public ParamSig(int index)
        {
            Index = index;
        }

        public int Index { get; }

        public override TypeSig Substitute(ImmutableArray<TypeSig> args)
        {
            return Index < args.Length ? args[Index] : this;
        }

        public override string ToString()
        {
            return "!" + Index;
        }
    }

    /// <summary>Anything else (arrays, pointers, method type parameters): never a view, action or DAC.</summary>
    internal sealed class OtherSig : TypeSig
    {
        public static readonly OtherSig Instance = new OtherSig();

        public override TypeSig Substitute(ImmutableArray<TypeSig> args)
        {
            return this;
        }

        public override string ToString()
        {
            return "?";
        }
    }

    /// <summary>Decodes signatures into <see cref="TypeSig"/> trees, naming types without resolving them.</summary>
    internal sealed class TypeSigProvider : ISignatureTypeProvider<TypeSig, object?>
    {
        public static readonly TypeSigProvider Instance = new TypeSigProvider();

        // Deeper nesting than this is malformed metadata, perhaps a cycle.
        private const int MaxNesting = 32;

        /// <summary>The full name of a type definition, nested types joined with '+'.</summary>
        public static string FullName(MetadataReader reader, TypeDefinitionHandle handle)
        {
            return FullName(reader, handle, 0);
        }

        /// <summary>The full name of a type reference, nested types joined with '+'.</summary>
        public static string FullName(MetadataReader reader, TypeReferenceHandle handle)
        {
            return FullName(reader, handle, 0);
        }

        private static string FullName(MetadataReader reader, TypeDefinitionHandle handle, int depth)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            string name = reader.GetString(type.Name);
            TypeDefinitionHandle outer = type.GetDeclaringType();
            if (!outer.IsNil)
            {
                return depth < MaxNesting
                    ? FullName(reader, outer, depth + 1) + "+" + name
                    : throw new BadImageFormatException("Type nesting too deep.");
            }

            string ns = reader.GetString(type.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }

        private static string FullName(MetadataReader reader, TypeReferenceHandle handle, int depth)
        {
            TypeReference type = reader.GetTypeReference(handle);
            string name = reader.GetString(type.Name);
            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return depth < MaxNesting
                    ? FullName(reader, (TypeReferenceHandle)type.ResolutionScope, depth + 1) + "+" + name
                    : throw new BadImageFormatException("Type nesting too deep.");
            }

            string ns = reader.GetString(type.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }

        /// <summary>Decodes a base type or other type handle.</summary>
        public TypeSig Decode(MetadataReader reader, EntityHandle handle)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    return Named(FullName(reader, (TypeDefinitionHandle)handle));
                case HandleKind.TypeReference:
                    return Named(FullName(reader, (TypeReferenceHandle)handle));
                case HandleKind.TypeSpecification:
                    return reader.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(this, null);
                default:
                    return OtherSig.Instance;
            }
        }

        public TypeSig GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            return Named(FullName(reader, handle));
        }

        public TypeSig GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            return Named(FullName(reader, handle));
        }

        public TypeSig GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }

        public TypeSig GetGenericInstantiation(TypeSig genericType, ImmutableArray<TypeSig> typeArguments)
        {
            return genericType is NamedSig named ? new NamedSig(named.Name, typeArguments) : OtherSig.Instance;
        }

        public TypeSig GetGenericTypeParameter(object? genericContext, int index)
        {
            return new ParamSig(index);
        }

        public TypeSig GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return Named("System." + typeCode.ToString());
        }

        public TypeSig GetGenericMethodParameter(object? genericContext, int index)
        {
            return OtherSig.Instance;
        }

        public TypeSig GetArrayType(TypeSig elementType, ArrayShape shape)
        {
            return OtherSig.Instance;
        }

        public TypeSig GetSZArrayType(TypeSig elementType)
        {
            return OtherSig.Instance;
        }

        public TypeSig GetByReferenceType(TypeSig elementType)
        {
            return OtherSig.Instance;
        }

        public TypeSig GetPointerType(TypeSig elementType)
        {
            return OtherSig.Instance;
        }

        public TypeSig GetPinnedType(TypeSig elementType)
        {
            return elementType;
        }

        public TypeSig GetModifiedType(TypeSig modifier, TypeSig unmodifiedType, bool isRequired)
        {
            return unmodifiedType;
        }

        public TypeSig GetFunctionPointerType(MethodSignature<TypeSig> signature)
        {
            return OtherSig.Instance;
        }

        private static NamedSig Named(string name)
        {
            return new NamedSig(name, ImmutableArray<TypeSig>.Empty);
        }
    }
}
