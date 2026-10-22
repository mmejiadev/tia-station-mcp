using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>The WinCC Unified screen items a description may name, and the type each one is.</summary>
    /// <remarks>
    /// Read from <c>PublicAPI\V20\Siemens.Engineering.dll</c> on 2026-10-21: every concrete type deriving
    /// from <c>HmiScreenItemBase</c>. Fail closed: a name not listed here is refused, never guessed, so
    /// a type Openness adds in a later version is not drawn until it has been added on purpose.
    ///
    /// A description may write the short name (<c>rectangle</c>) or the type's own (<c>HmiRectangle</c>),
    /// in any case.
    /// </remarks>
    public static class UnifiedItemTypes
    {
        private const string Namespace = "Siemens.Engineering.HmiUnified.UI.";
        private const string TypePrefix = "Hmi";

        private static readonly string[] Shapes =
        {
            "HmiCircle", "HmiCircleSegment", "HmiCircularArc", "HmiEllipse", "HmiEllipseSegment",
            "HmiEllipticalArc", "HmiGraphicView", "HmiLine", "HmiPolygon", "HmiPolyline", "HmiRectangle", "HmiText"
        };

        private static readonly string[] Widgets =
        {
            "HmiBar", "HmiButton", "HmiCheckBoxGroup", "HmiClock", "HmiGauge", "HmiIOField",
            "HmiListBox", "HmiRadioButtonGroup", "HmiSlider", "HmiSymbolicIOField", "HmiTextBox",
            "HmiToggleSwitch", "HmiTouchArea"
        };

        private static readonly string[] Controls =
        {
            "HmiAlarmControl", "HmiDetailedParameterControl", "HmiFunctionTrendControl", "HmiMediaControl",
            "HmiProcessControl", "HmiSystemDiagnosisControl", "HmiTrendCompanion", "HmiTrendControl", "HmiWebControl"
        };

        private static readonly string[] Screens = { "HmiScreenWindow" };

        /// <summary>Items Openness creates only with a contained type: a faceplate or a custom control.</summary>
        private static readonly string[] NeedContainedType =
        {
            "HmiFaceplateContainer", "HmiCustomWebControlContainer", "HmiCustomWidgetContainer"
        };

        /// <summary>
        /// Items Openness V20 has a type for but will not create: on 2026-10-21 <c>Create&lt;HmiLabel&gt;</c>
        /// answered "Not supported" on a WinCC Unified PC runtime, where every other listed type was created.
        /// </summary>
        private static readonly string[] NotCreatable = { "HmiLabel" };

        private static readonly string[] PointBased = { "HmiPolygon", "HmiPolyline" };

        private static readonly Dictionary<string, string> ByName = Build();

        /// <summary>Every drawable item, as the full Openness type name.</summary>
        public static IReadOnlyList<string> All { get; } = ByName.Values.Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();

        /// <summary>Finds the full Openness type name of an item.</summary>
        /// <param name="name">The short name or the type's own, in any case.</param>
        /// <returns>The full type name, e.g. <c>Siemens.Engineering.HmiUnified.UI.Shapes.HmiRectangle</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the name is not a drawable item.</exception>
        public static string Resolve(string name)
        {
            var key = Normalise(name);

            if (ByName.TryGetValue(key, out var fullName))
            {
                return fullName;
            }

            if (NeedContainedType.Any(type => Normalise(type) == key))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{name}' is created with a contained type, a faceplate or a custom control, which this tool does not take yet");
            }

            if (NotCreatable.Any(type => Normalise(type) == key))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"TIA Portal V20 does not create a '{name}' through Openness; a 'text' shows a fixed text instead");
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"'{name}' is not a WinCC Unified screen item. Drawable: {string.Join(", ", All.Select(ShortName))}");
        }

        /// <summary>Whether a type is one this tool draws, so an item of it found on a screen can be drawn back.</summary>
        /// <param name="name">The short name, the type's own, or the full type name.</param>
        /// <returns>True when <see cref="Resolve"/> would give a type.</returns>
        public static bool IsDrawable(string name)
        {
            var given = name ?? string.Empty;

            return ByName.ContainsKey(Normalise(given.Substring(given.LastIndexOf('.') + 1)));
        }

        /// <summary>Whether an item is drawn from a list of points instead of a frame.</summary>
        /// <param name="fullName">The full type name, as <see cref="Resolve"/> returns it.</param>
        /// <returns>True for a polyline or a polygon.</returns>
        public static bool IsPointBased(string fullName)
        {
            return PointBased.Any(type => fullName.EndsWith("." + type, StringComparison.Ordinal));
        }

        /// <summary>The short name a description uses for an item.</summary>
        /// <param name="fullName">The full type name.</param>
        /// <returns>For <c>…Shapes.HmiRectangle</c>, <c>rectangle</c>.</returns>
        public static string ShortName(string fullName)
        {
            var given = fullName ?? string.Empty;
            var typeName = given.Substring(given.LastIndexOf('.') + 1);
            var bare = Bare(typeName);

            // The spelling a description uses, 'ioField' for IOField and 'rectangle' for Rectangle:
            // the leading capitals are lowered except the one that starts the next word. Display only;
            // lookups go through Normalise.
            var capitals = bare.TakeWhile(char.IsUpper).Count();
            var lowered = capitals > 1 && capitals < bare.Length ? capitals - 1 : capitals;

            return new string(bare.Take(lowered).Select(char.ToLowerInvariant).ToArray()) + bare.Substring(lowered);
        }

        private static string Normalise(string name)
        {
            return Bare(name).ToUpperInvariant();
        }

        private static string Bare(string name)
        {
            var trimmed = (name ?? string.Empty).Trim();

            return trimmed.StartsWith(TypePrefix, StringComparison.OrdinalIgnoreCase) ? trimmed.Substring(TypePrefix.Length) : trimmed;
        }

        private static Dictionary<string, string> Build()
        {
            var types = new Dictionary<string, string>(StringComparer.Ordinal);

            Add(types, "Shapes", Shapes);
            Add(types, "Widgets", Widgets);
            Add(types, "Controls", Controls);
            Add(types, "Screens", Screens);

            return types;
        }

        private static void Add(Dictionary<string, string> types, string group, IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                types.Add(Normalise(name), $"{Namespace}{group}.{name}");
            }
        }
    }
}
