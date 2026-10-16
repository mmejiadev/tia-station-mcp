using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>Builds a button's events from their description, refusing one that is incomplete.</summary>
    /// <remarks>
    /// Two ways to write them. The short one, for the common case, runs one function on press:
    /// <c>"press": "setBitWhilePressed", "tag": "Marxa"</c>. The full one lists any number of
    /// functions, each on its event, run in order:
    /// <code>
    /// "events": [
    ///   { "event": "click", "function": "setTag", "tag": "Mode", "value": 1 },
    ///   { "event": "click", "function": "activateScreen", "screen": "Manual" } ]
    /// </code>
    /// Events are <c>press</c> and <c>click</c>, measured on buttons in the class exports, and
    /// <c>change</c>, measured on a symbolic I/O field.
    /// </remarks>
    internal static class HmiEventFactory
    {
        private const string PressEvent = "press";

        /// <summary>The events measured on buttons.</summary>
        public static readonly IReadOnlyCollection<string> ButtonEvents = new[] { PressEvent, "click" };

        /// <summary>The event measured on a symbolic I/O field.</summary>
        public static readonly IReadOnlyCollection<string> FieldEvents = new[] { "change" };

        private static readonly Dictionary<string, string> EventNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PressEvent] = "Press",
            ["click"] = "Click",
            ["change"] = "Change"
        };

        private static readonly Dictionary<string, Func<HmiActionDocument, string, HmiFunctionCall>> Functions =
            new Dictionary<string, Func<HmiActionDocument, string, HmiFunctionCall>>(StringComparer.OrdinalIgnoreCase)
            {
                ["setBitWhilePressed"] = (action, where) => HmiFunctionCall.SetBitWhileKeyPressed(HmiRefusal.RequireText(action.Tag, where, "tag")),
                ["setTag"] = (action, where) => HmiFunctionCall.SetTag(HmiRefusal.RequireText(action.Tag, where, "tag"), action.Value ?? 0),
                ["activateScreen"] = (action, where) => HmiFunctionCall.ActivateScreen(HmiRefusal.RequireText(action.Screen, where, "screen")),
                ["activatePreviousScreen"] = (action, where) => HmiFunctionCall.ActivatePreviousScreen(),
                ["activateScreenByNumber"] = (action, where) => HmiFunctionCall.ActivateScreenByNumber(HmiRefusal.RequireText(action.Tag, where, "tag"))
            };

        /// <summary>Builds a button's events: the short form, the full list, or both.</summary>
        /// <param name="item">The button's description.</param>
        /// <param name="where">The item, named in a refusal.</param>
        /// <returns>One handler per event, its functions in the order written.</returns>
        /// <exception cref="TiaMcpServer.Siemens.PortalException">No function is given, or one is incomplete.</exception>
        public static List<HmiEventBinding> Read(HmiScreenItemDocument item, string where)
        {
            var bindings = ReadOptional(item, where, ButtonEvents);

            return bindings.Count > 0
                ? bindings
                : throw HmiRefusal.Refuse(where, $"a button needs 'press' or 'events'; functions are: {string.Join(", ", Functions.Keys)}");
        }

        /// <summary>Builds an item's events, which it may have none of.</summary>
        /// <param name="item">The item's description.</param>
        /// <param name="where">The item, named in a refusal.</param>
        /// <param name="allowed">The events this kind of item has been measured with.</param>
        /// <returns>One binding per event, possibly none.</returns>
        /// <exception cref="TiaMcpServer.Siemens.PortalException">An event the item kind does not take, or an incomplete function.</exception>
        public static List<HmiEventBinding> ReadOptional(HmiScreenItemDocument item, string where, IReadOnlyCollection<string> allowed)
        {
            return ShortForm(item).Concat(item.Events ?? new List<HmiActionDocument>())
                .Select((action, index) => new { Event = EventName(action, where, allowed), Call = Call(action, $"{where}, function {index + 1}") })
                .GroupBy(entry => entry.Event, StringComparer.Ordinal)
                .Select(group => new HmiEventBinding(group.Key, group.Select(entry => entry.Call).ToList()))
                .ToList();
        }

        private static IEnumerable<HmiActionDocument> ShortForm(HmiScreenItemDocument item)
        {
            if (string.IsNullOrWhiteSpace(item.Press))
            {
                return Enumerable.Empty<HmiActionDocument>();
            }

            return new[] { new HmiActionDocument { Event = PressEvent, Function = item.Press, Tag = item.Tag, Value = item.Value, Screen = item.Screen } };
        }

        /// <remarks>
        /// Each kind takes only the events it was measured with: an event TIA Portal does not take on
        /// an item would be refused by the import, and an import failure may end the session.
        /// </remarks>
        private static string EventName(HmiActionDocument action, string where, IReadOnlyCollection<string> allowed)
        {
            var requested = (action.Event ?? PressEvent).Trim();

            return allowed.Contains(requested, StringComparer.OrdinalIgnoreCase) && EventNames.TryGetValue(requested, out var name)
                ? name
                : throw HmiRefusal.Refuse(where, $"'event' must be one of: {string.Join(", ", allowed)} for this kind of item");
        }

        /// <summary>Whether a description gives an item events at all, in either form.</summary>
        /// <param name="item">The item's description.</param>
        /// <returns>True when it has <c>press</c> or <c>events</c>.</returns>
        public static bool HasEvents(HmiScreenItemDocument item)
        {
            return !string.IsNullOrWhiteSpace(item.Press) || (item.Events?.Count ?? 0) > 0;
        }

        private static HmiFunctionCall Call(HmiActionDocument action, string where)
        {
            return Functions.TryGetValue((action.Function ?? string.Empty).Trim(), out var build)
                ? build(action, where)
                : throw HmiRefusal.Refuse(where, $"'function' must be one of: {string.Join(", ", Functions.Keys)}");
        }
    }
}
