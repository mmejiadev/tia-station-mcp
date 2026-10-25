using Siemens.Engineering.HmiUnified.UI.Dynamization.Script;
using Siemens.Engineering.HmiUnified.UI.Enum;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HmiUnified.UI.Widgets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Siemens
{
    /// <summary>Writes, checks and reads the JavaScript events of a WinCC Unified screen item.</summary>
    /// <remarks>
    /// Measured 2026-10-23 on TIA Portal V20. Each item type has its own <c>EventHandlers</c>, whose
    /// <c>Create</c> takes that type's event enum (a button's: Activated, Deactivated, Tapped, KeyDown,
    /// KeyUp, Down, Up, ContextTapped); a handler holds an event type and one script, nothing else. The
    /// script's setter takes broken JavaScript without a word, and only <c>SyntaxCheck()</c> reports it
    /// ("missing ) after argument list"), so every script is checked: before anything is drawn, on the
    /// throw-away screen of <see cref="UnifiedRehearsal"/>, and again where it is written.
    /// </remarks>
    internal static class UnifiedEventWriter
    {
        private const string EventHandlersProperty = "EventHandlers";

        /// <summary>The text TIA Portal puts in a new script's global definition area, left out when read back.</summary>
        private const string DefaultGlobalDefinitions = "//Insert definitions for events here";

        /// <summary>Refuses an event the item's type does not have, before anything is changed.</summary>
        /// <param name="itemType">The item's Openness type.</param>
        /// <param name="script">The event.</param>
        /// <param name="itemName">The item, as a refusal names it.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> listing the events the type has.</exception>
        public static void Check(Type itemType, UnifiedEventScript script, string itemName)
        {
            EventValue(EventEnum(itemType, itemName), script.EventName, itemName);
        }

        /// <summary>Checks every event script on a throw-away screen, before anything is drawn.</summary>
        /// <param name="scratch">A screen made for the purpose, deleted by the caller.</param>
        /// <param name="items">The items and their events.</param>
        /// <returns>One reason per broken script; empty when all are valid.</returns>
        /// <remarks>A script is only checked inside a handler, so one button's handler holds each in turn.</remarks>
        public static IReadOnlyList<string> CheckScripts(HmiScreen scratch, IReadOnlyList<UnifiedItemDescription> items)
        {
            var scripts = items.SelectMany(item => item.Events.Select(script => (Item: item.Name, Script: script))).ToList();
            if (scripts.Count == 0)
            {
                return Array.Empty<string>();
            }

            var code = scratch.ScreenItems.Create<HmiButton>("Check").EventHandlers.Create(HmiButtonEventType.Tapped).Script;

            return scripts.Select(entry => Errors(code, entry.Script, entry.Item)).OfType<string>().ToList();
        }

        /// <summary>Writes an event's script onto a new item and checks its syntax again.</summary>
        /// <param name="item">The item, just created.</param>
        /// <param name="script">The event, already passed through <see cref="Check"/> and <see cref="CheckScripts"/>.</param>
        /// <param name="itemName">The item, as a refusal names it.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when TIA Portal finds the JavaScript broken.</exception>
        public static void Write(object item, UnifiedEventScript script, string itemName)
        {
            var handlers = UnifiedPropertyTree.MostDerived(item.GetType(), EventHandlersProperty)!.GetValue(item)!;
            var create = handlers.GetType().GetMethod("Create")!;
            var value = EventValue(create.GetParameters()[0].ParameterType, script.EventName, itemName);
            var handler = create.Invoke(handlers, new[] { value })!;

            if (Errors(ScriptOf(handler)!, script, itemName) is string problem)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, problem);
            }
        }

        /// <summary>Reads an item's events in the shape a description takes.</summary>
        /// <param name="item">The item.</param>
        /// <returns><c>[{ event, script, globalDefinitions? }]</c>, or null when the item has none.</returns>
        public static JsonArray? Read(object item)
        {
            var read = Handlers(item).Select(handler => (JsonNode)Describe(handler)).ToArray();

            return read.Length == 0 ? null : new JsonArray(read);
        }

        /// <summary>What in an item's events its backup could not draw back: an empty script, or one TIA Portal finds broken.</summary>
        /// <param name="item">The item about to be deleted by a redraw.</param>
        /// <param name="itemName">The item, as a reason names it.</param>
        /// <returns>One reason per event that would not come back; empty when all would.</returns>
        public static IReadOnlyList<string> Problems(object item, string itemName)
        {
            var problems = new List<string>();

            foreach (var handler in Handlers(item))
            {
                var code = ScriptOf(handler);
                var eventName = EventNameOf(handler);

                if (code == null || string.IsNullOrWhiteSpace(code.ScriptCode))
                {
                    problems.Add($"'{itemName}' has an empty '{eventName}' event, which a redraw cannot put back");
                    continue;
                }

                if (code.SyntaxCheck()?.Errors?.Any() == true)
                {
                    problems.Add($"'{itemName}' has a '{eventName}' script TIA Portal finds broken, which a redraw cannot put back");
                }
            }

            return problems;
        }

        /// <summary>Puts a script into a handler and returns what its syntax check found, or null when nothing.</summary>
        private static string? Errors(IHmiScript code, UnifiedEventScript script, string itemName)
        {
            code.ScriptCode = script.Script;
            code.GlobalDefinitionAreaScriptCode = script.GlobalDefinitions ?? DefaultGlobalDefinitions;

            var errors = code.SyntaxCheck()?.Errors?.ToList() ?? new List<string>();

            return errors.Count == 0 ? null : $"the '{script.EventName}' script of '{itemName}' is not valid JavaScript: {string.Join("; ", errors)}";
        }

        private static JsonObject Describe(object handler)
        {
            var code = ScriptOf(handler);
            var described = new JsonObject { ["event"] = EventNameOf(handler), ["script"] = code?.ScriptCode ?? string.Empty };

            var globals = code?.GlobalDefinitionAreaScriptCode ?? string.Empty;
            if (!string.Equals(globals.Trim(), DefaultGlobalDefinitions, StringComparison.Ordinal) && globals.Trim().Length > 0)
            {
                described["globalDefinitions"] = globals;
            }

            return described;
        }

        private static IEnumerable<object> Handlers(object item)
        {
            return UnifiedPropertyTree.MostDerived(item.GetType(), EventHandlersProperty)?.GetValue(item) is IEnumerable handlers
                ? handlers.Cast<object>()
                : Enumerable.Empty<object>();
        }

        private static IHmiScript? ScriptOf(object handler)
        {
            return UnifiedPropertyTree.MostDerived(handler.GetType(), "Script")?.GetValue(handler) as IHmiScript;
        }

        private static string EventNameOf(object handler)
        {
            return UnifiedPropertyTree.MostDerived(handler.GetType(), "EventType")?.GetValue(handler)?.ToString() ?? string.Empty;
        }

        private static Type EventEnum(Type itemType, string itemName)
        {
            var parameter = UnifiedPropertyTree.MostDerived(itemType, EventHandlersProperty)?.PropertyType.GetMethod("Create")?.GetParameters().FirstOrDefault()?.ParameterType;

            return parameter != null && parameter.IsEnum
                ? parameter
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{itemName}' is a {itemType.Name}, which takes no events");
        }

        private static object EventValue(Type eventEnum, string eventName, string itemName)
        {
            var names = Enum.GetNames(eventEnum).Where(name => name != "None").ToList();
            var match = names.FirstOrDefault(name => name.Equals(eventName, StringComparison.OrdinalIgnoreCase));

            return match != null
                ? Enum.Parse(eventEnum, match)
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{itemName}' has no event '{eventName}'; its events are: {string.Join(", ", names)}");
        }
    }
}
