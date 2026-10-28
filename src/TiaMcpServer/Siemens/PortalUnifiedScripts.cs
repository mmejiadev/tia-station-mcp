using Siemens.Engineering.HmiUnified.UI.Dynamization.Script;
using Siemens.Engineering.HmiUnified.UI.ScreenGroup;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScripts;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Checking the scripts of WinCC Unified screens for faults TIA Portal compiles without a word. Read
    /// only: the scripts are read as text and never run. Only the scripts are read from Openness (the
    /// screen's and each item's events, and script dynamizations), not every property of every item, since
    /// every read crosses into TIA Portal while the Openness gate is held. See <see cref="UnifiedScriptChecker"/>.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Checks the scripts of one WinCC Unified screen, or of every screen of the HMI.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <param name="screenName">The screen, or null or empty for every screen, those in screen groups included.</param>
        /// <returns><c>{ screens, scripts, findings: [{ screen, item, where, line, rule, severity, message }] }</c>.</returns>
        /// <exception cref="PortalException">The path is not a WinCC Unified HMI, or the screen does not exist.</exception>
        public JsonObject CheckUnifiedScripts(string hmiPath, string? screenName)
        {
            try
            {
                var software = RequireUnifiedSoftware(hmiPath);
                var screens = string.IsNullOrWhiteSpace(screenName) ? AllScreens(software) : new List<HmiScreen> { FindUnifiedScreen(software, hmiPath, screenName!) };
                var context = new UnifiedScriptContext(TagTypes(software));
                var sources = screens.SelectMany(screen => ScriptsOf(screen).Select(source => (Screen: screen.Name, Source: source))).ToList();

                return new JsonObject
                {
                    ["screens"] = screens.Count,
                    ["scripts"] = sources.Count,
                    ["findings"] = new JsonArray(sources.SelectMany(entry => Findings(entry.Screen, entry.Source, context)).ToArray())
                };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "CheckUnifiedScripts", hmiPath, screenName ?? string.Empty);
            }
        }

        /// <remarks>A script with global definitions is checked knowing them, and the definitions are checked as a script of their own.</remarks>
        private static IEnumerable<JsonNode> Findings(string screen, UnifiedScriptSource source, UnifiedScriptContext context)
        {
            var inScript = UnifiedScriptChecker.Check(source.Script, context, source.Globals).Select(finding => Describe(screen, source.Item, source.Where, finding));
            var inGlobals = source.Globals == null
                ? Enumerable.Empty<JsonNode>()
                : UnifiedScriptChecker.Check(source.Globals, context).Select(finding => Describe(screen, source.Item, $"global definitions of {source.Where}", finding));

            return inScript.Concat(inGlobals);
        }

        /// <summary>The scripts of a screen: its own events, then each item's events and script animations.</summary>
        private static IEnumerable<UnifiedScriptSource> ScriptsOf(HmiScreen screen)
        {
            var own = EventScripts(screen, screen.Name);
            var items = screen.ScreenItems.SelectMany(item => EventScripts(item, item.Name).Concat(AnimationScripts(item, item.Name)));

            return own.Concat(items);
        }

        private static IEnumerable<UnifiedScriptSource> EventScripts(object owner, string name)
        {
            return (UnifiedEventWriter.Read(owner) ?? new JsonArray())
                .OfType<JsonObject>()
                .Where(handler => !string.IsNullOrWhiteSpace(Text(handler, "script")))
                .Select(handler => new UnifiedScriptSource(name, $"event {Text(handler, "event")}", Text(handler, "script")!, Text(handler, "globalDefinitions")));
        }

        private static IEnumerable<UnifiedScriptSource> AnimationScripts(object item, string name)
        {
            return UnifiedScreenReader.Dynamizations(item)
                .OfType<ScriptDynamization>()
                .Where(script => !string.IsNullOrWhiteSpace(script.ScriptCode))
                .Select(script => new UnifiedScriptSource(name, $"animation {script.PropertyName}", script.ScriptCode, UnifiedAnimationReader.GlobalsOf(script)));
        }

        private static string? Text(JsonObject owner, string field)
        {
            return owner[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        }

        private static JsonObject Describe(string screen, string item, string where, UnifiedScriptFinding finding)
        {
            return new JsonObject
            {
                ["screen"] = screen,
                ["item"] = item,
                ["where"] = where,
                ["line"] = finding.Line,
                ["rule"] = finding.Rule,
                ["severity"] = finding.Severity == UnifiedScriptSeverity.Error ? "error" : "warning",
                ["message"] = finding.Message
            };
        }

        /// <remarks>System tags count too: a script may read <c>@UserName</c>.</remarks>
        private static Dictionary<string, string> TagTypes(UnifiedHmiSoftware software)
        {
            var types = software.Tags.ToDictionary(tag => tag.Name, tag => tag.DataType, StringComparer.OrdinalIgnoreCase);
            foreach (var tag in software.SystemTags)
            {
                types[tag.Name] = tag.DataType;
            }

            return types;
        }

        private static List<HmiScreen> AllScreens(UnifiedHmiSoftware software)
        {
            return software.Screens.Concat(InScreenGroups(software.ScreenGroups)).ToList();
        }

        private static IEnumerable<HmiScreen> InScreenGroups(HmiScreenGroupComposition groups)
        {
            return groups.SelectMany(group => group.Screens.Concat(InScreenGroups(group.Groups)));
        }
    }
}
