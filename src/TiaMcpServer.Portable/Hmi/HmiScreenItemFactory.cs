using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Hmi
{
    /// <summary>Builds screen items from their description, refusing one that is incomplete.</summary>
    /// <remarks>
    /// One factory per screen: it numbers the default names per kind, as TIA Portal does when an item
    /// is dropped on a screen (<c>Button_1</c>, <c>Button_2</c>). Every refusal names the item by its
    /// position and name, because a description of twenty items with "tag is required" is not
    /// actionable without knowing which one.
    ///
    /// Default sizes are the ones the class projects use: buttons and I/O fields 96 by 32, pilot lights
    /// 38 across, bars 80 by 230. Events are read by <see cref="HmiEventFactory"/> and animations by
    /// <see cref="HmiAnimationFactory"/>; every kind takes animations.
    /// </remarks>
    internal sealed class HmiScreenItemFactory
    {
        private const string DefaultMode = "InOutput";
        private const string DefaultFormat = "9999";
        private const string DefaultOffColor = "255, 0, 0";
        private const string DefaultOnColor = "0, 255, 0";
        private const double DefaultBarMaximum = 100;

        private static readonly string[] Modes = { "InOutput", "Output", "Input" };
        private static readonly string[] Alignments = { "Left", "Center", "Right" };
        private static readonly string[] FontStyles = { "Bold", "Regular" };
        private static readonly string[] DateTimeShows = { "time", "date", "both" };
        private static readonly HashSet<string> KindsWithEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "button", "symbolicField" };

        private static readonly Regex FormatPattern = new Regex(@"^9+(\.9+)?$", RegexOptions.CultureInvariant);

        private readonly Dictionary<string, HmiItemKind> _kinds;
        private readonly Dictionary<HmiItemKind, int> _defaultNamesUsed = new Dictionary<HmiItemKind, int>();

        /// <summary>Creates a factory for one screen.</summary>
        public HmiScreenItemFactory()
        {
            _kinds = new Dictionary<string, HmiItemKind>(StringComparer.OrdinalIgnoreCase)
            {
                ["button"] = new HmiItemKind("Button", Size(96, 32), HmiItemStyle.Button()["BackColor"], BuildButton),
                ["ioField"] = new HmiItemKind("I/O field", Size(96, 32), HmiItemStyle.IoField()["BackColor"], BuildIoField),
                ["symbolicField"] = new HmiItemKind("Symbolic I/O field", Size(96, 32), HmiItemStyle.SymbolicIoField()["BackColor"], BuildSymbolicField),
                ["text"] = new HmiItemKind("Text field", Size(100, 20), HmiItemStyle.TextField()["BackColor"], BuildText),
                ["pilotLight"] = new HmiItemKind("Circle", Size(38, 38), DefaultOffColor, BuildPilotLight),
                ["bar"] = new HmiItemKind("Bar", Size(80, 230), HmiItemStyle.Bar()["BackColor"], BuildBar),
                ["dateTime"] = new HmiItemKind("Date/time field", Size(105, 30), HmiItemStyle.DateTimeField()["BackColor"], BuildDateTime)
            };
        }

        /// <summary>Builds one item.</summary>
        /// <param name="item">Its description.</param>
        /// <param name="position">Its position in the description, from 1, named in a refusal.</param>
        /// <returns>The item.</returns>
        /// <exception cref="TiaMcpServer.Siemens.PortalException">The description is incomplete or invalid.</exception>
        public HmiScreenItem Create(HmiScreenItemDocument item, int position)
        {
            var kind = FindKind(item.Type, position);
            var name = string.IsNullOrWhiteSpace(item.Name) ? NextDefaultName(kind) : item.Name!.Trim();
            var where = $"Item {position} ('{name}')";

            if (HmiEventFactory.HasEvents(item) && !KindsWithEvents.Contains((item.Type ?? string.Empty).Trim()))
            {
                throw HmiRefusal.Refuse(where, "only buttons and symbolic fields take 'press' or 'events'; this item's would be lost");
            }
            var look = ReadLook(item, where);
            var animations = HmiAnimationFactory.Read(item.Animations, look.BackColor.Length > 0 ? look.BackColor : kind.StyleBackColor, where);

            return kind.Build(item, new HmiItemFrame(name, ReadBox(item, kind, where), look, animations), where);
        }

        private static HmiBox Size(int width, int height)
        {
            return new HmiBox(0, 0, width, height);
        }

        private HmiItemKind FindKind(string? type, int position)
        {
            return _kinds.TryGetValue((type ?? string.Empty).Trim(), out var kind)
                ? kind
                : throw HmiRefusal.Refuse($"Item {position}", $"type '{type}' is not one this generator writes. Use one of: {string.Join(", ", _kinds.Keys)}");
        }

        private string NextDefaultName(HmiItemKind kind)
        {
            _defaultNamesUsed.TryGetValue(kind, out var used);
            _defaultNamesUsed[kind] = used + 1;

            return $"{kind.NamePrefix}_{(used + 1).ToString(CultureInfo.InvariantCulture)}";
        }

        private static HmiBox ReadBox(HmiScreenItemDocument item, HmiItemKind kind, string where)
        {
            var box = new HmiBox(
                item.Left ?? throw HmiRefusal.Refuse(where, "'left' is required: where an item goes is what the description is for"),
                item.Top ?? throw HmiRefusal.Refuse(where, "'top' is required: where an item goes is what the description is for"),
                item.Width ?? kind.DefaultSize.Width,
                item.Height ?? kind.DefaultSize.Height);

            return box.Left >= 0 && box.Top >= 0 && box.Width > 0 && box.Height > 0
                ? box
                : throw HmiRefusal.Refuse(where, "'left' and 'top' must not be negative, and 'width' and 'height' must be positive");
        }

        private static HmiLook ReadLook(HmiScreenItemDocument item, string where)
        {
            if (item.FontSize.HasValue && item.FontSize.Value <= 0)
            {
                throw HmiRefusal.Refuse(where, "'fontSize' must be positive");
            }

            return new HmiLook(
                OptionalColor(item.BackColor, where, "backColor"),
                OptionalColor(item.ForeColor, where, "foreColor"),
                new HmiFont(item.FontSize ?? 0, OptionalChoice(item.FontStyle, FontStyles, where, "fontStyle")),
                OptionalChoice(item.Align, Alignments, where, "align"));
        }

        private static HmiButton BuildButton(HmiScreenItemDocument item, HmiItemFrame frame, string where)
        {
            return new HmiButton(frame, HmiRefusal.RequireText(item.Text, where, "text"), HmiEventFactory.Read(item, where));
        }

        private static HmiIoField BuildIoField(HmiScreenItemDocument item, HmiItemFrame frame, string where)
        {
            var format = (item.Format ?? DefaultFormat).Trim();

            if (!FormatPattern.IsMatch(format))
            {
                throw HmiRefusal.Refuse(where, $"'format' is the digits shown, written with 9s, for example '9999' or '999.9'; '{format}' is not");
            }

            return new HmiIoField(frame, HmiRefusal.RequireText(item.Tag, where, "tag"), ReadMode(item, where), format);
        }

        private static HmiSymbolicIoField BuildSymbolicField(HmiScreenItemDocument item, HmiItemFrame frame, string where)
        {
            var source = new HmiSymbolicSource(HmiRefusal.RequireText(item.Tag, where, "tag"), HmiRefusal.RequireText(item.TextList, where, "textList"));

            return new HmiSymbolicIoField(frame, source, ReadMode(item, where), HmiEventFactory.ReadOptional(item, where, HmiEventFactory.FieldEvents));
        }

        private static HmiTextField BuildText(HmiScreenItemDocument item, HmiItemFrame frame, string where)
        {
            return new HmiTextField(frame, HmiRefusal.RequireText(item.Text, where, "text"));
        }

        /// <remarks>
        /// A pilot light is a circle with its own appearance animation, and its own visibility animation
        /// when it has a visibility tag, so a frame animation of either kind would be a second one.
        /// </remarks>
        private static HmiPilotLight BuildPilotLight(HmiScreenItemDocument item, HmiItemFrame frame, string where)
        {
            var visibilityTag = (item.VisibleTag ?? string.Empty).Trim();
            var clash = frame.Animations.FirstOrDefault(animation =>
                animation.Kind == HmiAppearanceAnimation.KindName || (animation.Kind == HmiVisibilityAnimation.KindName && visibilityTag.Length > 0));

            if (clash != null)
            {
                throw HmiRefusal.Refuse(where, $"a pilot light already has its own {clash.Kind} animation, set through offColor, onColor, flashing and visibleTag");
            }

            var colors = new HmiPilotLightColors(
                HmiColor.Parse(item.OffColor ?? DefaultOffColor, $"{where}, offColor"),
                HmiColor.Parse(item.OnColor ?? DefaultOnColor, $"{where}, onColor"),
                item.Flashing ?? false);

            return new HmiPilotLight(frame, HmiRefusal.RequireText(item.Tag, where, "tag"), colors, visibilityTag);
        }

        private static HmiBar BuildBar(HmiScreenItemDocument item, HmiItemFrame frame, string where)
        {
            var minimum = item.Minimum ?? 0;
            var maximum = item.Maximum ?? DefaultBarMaximum;

            return maximum > minimum
                ? new HmiBar(frame, HmiRefusal.RequireText(item.Tag, where, "tag"), minimum, maximum)
                : throw HmiRefusal.Refuse(where, "'maximum' must be greater than 'minimum'");
        }

        private static HmiDateTimeField BuildDateTime(HmiScreenItemDocument item, HmiItemFrame frame, string where)
        {
            var show = OptionalChoice(item.Show ?? DateTimeShows[0], DateTimeShows, where, "show");

            return new HmiDateTimeField(frame, show != DateTimeShows[0], show != DateTimeShows[1]);
        }

        private static string ReadMode(HmiScreenItemDocument item, string where)
        {
            return OptionalChoice(item.Mode ?? DefaultMode, Modes, where, "mode");
        }

        private static string OptionalChoice(string? text, string[] choices, string where, string field)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return choices.FirstOrDefault(choice => choice.Equals(text!.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw HmiRefusal.Refuse(where, $"'{field}' must be one of: {string.Join(", ", choices)}");
        }

        private static string OptionalColor(string? text, string where, string field)
        {
            return string.IsNullOrWhiteSpace(text) ? string.Empty : HmiColor.Parse(text!, $"{where}, {field}");
        }
    }
}
