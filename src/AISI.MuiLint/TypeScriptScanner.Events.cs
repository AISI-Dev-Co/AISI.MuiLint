using System;
using System.Collections.Generic;
using System.Globalization;

namespace AISI.MuiLint
{
    public static partial class TypeScriptScanner
    {
        private const string HandleEventDecorator = "@handleEvent";

        private static readonly Dictionary<string, string> ArgsOfEvent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ValueChanged"] = "ValueChangedHandlerArgs",
            ["RowSelected"] = "RowSelectedHandlerArgs",
            ["CurrentRowChanged"] = "CurrentRowChangedHandlerArgs",
            ["GetRowCss"] = "RowCssHandlerArgs",
            ["GetCellCss"] = "CellCssHandlerArgs",
            ["CallbackCompleted"] = "CallbackCompletedHandlerArgs",
        };

        /// <summary>AISI0032 to AISI0034: what each @handleEvent is attached to and what its method is handed.</summary>
        private static void ScanHandlers(string path, TsModule module, ScreenModel? screen, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (TsEventHandler handler in module.Handlers)
            {
                if (!ArgsOfEvent.TryGetValue(handler.Event, out string? expectedArgs))
                {
                    continue;
                }

                string target = handler.Event == "ValueChanged" ? "field" : "column";
                if (handler.PropertiesKnown && handler.Event != "CallbackCompleted")
                {
                    string? problem = null;
                    if (!handler.Has("view"))
                    {
                        problem = string.Format(
                            CultureInfo.InvariantCulture,
                            "@handleEvent(CustomEventType.{0}) names no view, so the handler is attached to nothing. Add {{ view: \"...\" }}.",
                            handler.Event);
                    }
                    else if (handler.Event == "ValueChanged" && !handler.Has("field"))
                    {
                        problem = "A ValueChanged handler needs the field it watches: { view: \"...\", field: \"...\" }.";
                    }
                    else if (handler.Event == "GetCellCss" && !handler.Has("column") && !handler.Has("allColumns"))
                    {
                        problem = "A GetCellCss handler needs a column, or allColumns: true.";
                    }

                    if (problem != null)
                    {
                        results.Add(HtmlMergeScanner.Create(DiagnosticIds.HandlerMissingTarget, problem, path, handler.DecoratorStart, HandleEventDecorator.Length, lineMap));
                    }
                }

                TsLiteral? view = handler.Literal("view");
                TsLiteral? field = handler.Event == "ValueChanged" || handler.Event == "GetCellCss" ? handler.Literal(target) : null;
                ICollection<string>? declared = screen == null || view == null || field == null ? null : screen.FieldsOf(view.Value.Value);
                if (declared != null && field != null && view != null && HtmlMergeScanner.IsIdentifier(field.Value.Value))
                {
                    bool found = false;
                    foreach (string name in declared)
                    {
                        found |= string.Equals(name, field.Value.Value, StringComparison.OrdinalIgnoreCase);
                    }

                    if (!found)
                    {
                        string message = string.Format(
                            CultureInfo.InvariantCulture,
                            "@handleEvent names the {0} '{1}', but {2}, the class of view '{3}', declares no field called '{1}'.",
                            target,
                            field.Value.Value,
                            screen!.ClassOf(view.Value.Value),
                            view.Value.Value);
                        results.Add(HtmlMergeScanner.Create(DiagnosticIds.HandlerFieldNotDeclared, message, path, field.Value.Start, field.Value.Value.Length, lineMap));
                    }
                }

                // RowSelected and CurrentRowChanged hand over the same arguments, and Acumatica's screens mix the two types freely.
                bool rowEvents = (handler.Event == "RowSelected" || handler.Event == "CurrentRowChanged")
                    && (handler.ArgsType == "RowSelectedHandlerArgs" || handler.ArgsType == "CurrentRowChangedHandlerArgs");
                if (handler.ArgsType != expectedArgs && !rowEvents && ArgsOfEvent.ContainsValue(handler.ArgsType))
                {
                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} handles {1}, but its parameter is {2}, the arguments of another event. Use {3}.",
                        handler.MethodName,
                        handler.Event,
                        handler.ArgsType,
                        expectedArgs);
                    results.Add(HtmlMergeScanner.Create(DiagnosticIds.HandlerArgsOfAnotherEvent, message, path, handler.ArgsTypeStart, handler.ArgsType.Length, lineMap));
                }
            }
        }

        /// <summary>AISI0035: methods of this file's screen extensions that share a name with a method the screen already has.</summary>
        private static void ScanMethodCollisions(string path, TsModule module, ScreenModel screen, LineMap lineMap, List<Diagnostic> results)
        {
            foreach (TsClass c in module.Classes)
            {
                if (c.Name == screen.ScreenClass || !screen.IsPartOf(c.Name, screen.ScreenClass))
                {
                    continue;
                }

                foreach (KeyValuePair<string, TsMember> member in c.Members)
                {
                    if (!member.Value.IsMethod || member.Key == "constructor")
                    {
                        continue;
                    }

                    string? owner = screen.MethodOwner(member.Key, c.Name, out bool ownerIsHandler);
                    if (owner == null)
                    {
                        continue;
                    }

                    bool isHandler = false;
                    foreach (TsEventHandler handler in module.Handlers)
                    {
                        isHandler |= handler.MethodStart == member.Value.Offset;
                    }

                    // client-controls renames a handler that meets another handler of the same name, so both still run.
                    if (isHandler && ownerIsHandler)
                    {
                        continue;
                    }

                    string message = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}.{1}() has the same name as a method of {2}. Extension methods are copied onto the screen by name, so one replaces the other without a word. Rename it.",
                        c.Name,
                        member.Key,
                        owner);
                    results.Add(HtmlMergeScanner.Create(DiagnosticIds.ExtensionMethodCollision, message, path, member.Value.Offset, member.Key.Length, lineMap));
                }
            }
        }
    }
}
