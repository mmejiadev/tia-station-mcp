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

        private static readonly System.Text.RegularExpressions.Regex Imports = new System.Text.RegularExpressions.Regex(@"\bimport\s[^;\n]*;?", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

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

            var handler = scratch.ScreenItems.Create<HmiButton>("Check").EventHandlers.Create(HmiButtonEventType.Tapped);

            return scripts.Select(entry => Errors(handler.Script, entry.Script, entry.Item)).OfType<string>().ToList();
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

        /// <summary>Writes a screen's global definitions through one script, unless they already read so.</summary>
        /// <param name="code">The script.</param>
        /// <param name="globals">The definitions.</param>
        /// <remarks>
        /// Measured 2026-11-18 on V21: the area is the screen's, so the second item's script already reads what the first
        /// wrote. Written only when it differs, so drawing many items does not rewrite it once per item.
        /// </remarks>
        internal static void WriteGlobals(IHmiScript code, string globals)
        {
            if (!string.Equals((code.GlobalDefinitionAreaScriptCode ?? string.Empty).Trim(), globals.Trim(), StringComparison.Ordinal))
            {
                code.GlobalDefinitionAreaScriptCode = globals;
            }
        }

        /// <summary>Writes the global definitions a screen's events share, imports included, once its items are drawn.</summary>
        /// <param name="screen">The screen.</param>
        /// <param name="globals">The whole area; null for none.</param>
        /// <remarks>Through the first event script of the screen or its items: the area is the screen's (see <see cref="Errors"/>).</remarks>
        public static void WriteScreenGlobals(HmiScreen screen, string? globals)
        {
            var code = Owners(screen, withItems: true)
                .SelectMany(Handlers)
                .Select(ScriptOf)
                .FirstOrDefault(script => script != null);

            if (code != null)
            {
                WriteGlobals(code, globals ?? DefaultGlobalDefinitions);
            }
        }

        /// <summary>Whether a screen's events' global definitions hold an import, which a syntax check does not survive.</summary>
        /// <param name="screen">The screen.</param>
        /// <returns>True when they do.</returns>
        public static bool HoldsImport(HmiScreen screen)
        {
            return ScreenGlobals(screen, withItems: true) is string globals && Imports.IsMatch(globals);
        }

        private static string WithoutImports(string? globals)
        {
            var stripped = Imports.Replace(globals ?? string.Empty, string.Empty);

            return stripped.Trim().Length == 0 ? DefaultGlobalDefinitions : stripped;
        }

        /// <summary>The global definitions a screen's events share, as its own events or its items' read them.</summary>
        /// <param name="screen">The screen.</param>
        /// <param name="withItems">Whether its items' events are read too, or only its own (the items are about to be redrawn).</param>
        /// <returns>The definitions; null when none holds any.</returns>
        /// <remarks>Measured 2026-11-18: one area per screen for all its events (see <c>UnifiedScreenGlobals</c>).</remarks>
        public static string? ScreenGlobals(HmiScreen screen, bool withItems)
        {
            return Owners(screen, withItems)
                .SelectMany(owner => (Read(owner) ?? new JsonArray()).OfType<JsonObject>())
                .Select(handler => handler["globalDefinitions"]?.GetValue<string>())
                .FirstOrDefault(globals => globals != null);
        }

        /// <summary>What in an item's events its backup could not draw back: an empty script, or one TIA Portal finds broken.</summary>
        /// <param name="item">The item about to be deleted by a redraw.</param>
        /// <param name="itemName">The item, as a reason names it.</param>
        /// <param name="skipSyntaxCheck">Whether the scripts are not syntax-checked.</param>
        /// <returns>One reason per event that would not come back; empty when all would.</returns>
        /// <remarks>
        /// With <paramref name="skipSyntaxCheck"/>, the scripts are not syntax-checked: on a screen whose area holds an
        /// import, a syntax check ends TIA Portal's session (see <see cref="Errors"/>). The drawing's own rehearsal checks
        /// them, imports left out, before anything is deleted.
        /// </remarks>
        public static IReadOnlyList<string> Problems(object item, string itemName, bool skipSyntaxCheck = false)
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

                if (!skipSyntaxCheck && code.SyntaxCheck()?.Errors?.Any() == true)
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

            // Checked with the imports left out; the whole area is written once, after the screen's items are drawn
            // (WriteScreenGlobals). Measured 2026-11-19 on V21, with a script module in the HMI: with an import in the
            // screen's area, SyntaxCheck throws "Error when calling method 'SyntaxCheck'" and TIA Portal's session ends -
            // already the first check. With the area at its default, two checks in a row pass. An import does not change
            // whether a script parses; what it names is CheckUnifiedScripts' to check.
            WriteGlobals(code, WithoutImports(script.GlobalDefinitions));
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

        /// <remarks>
        /// The screen, then its items when asked. <c>Enumerable.Concat</c> by name: measured 2026-11-19, the extension
        /// <c>new object[] { screen }.Concat(items)</c> bound in this assembly to an overload that appends its argument as
        /// one element - the item collection itself, not its items - so no item's events were ever found.
        /// </remarks>
        private static List<object> Owners(HmiScreen screen, bool withItems)
        {
            var items = withItems ? screen.ScreenItems.Cast<object>() : Enumerable.Empty<object>();

            return Enumerable.Concat(new object[] { screen }, items).ToList();
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
