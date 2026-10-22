using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>Refuses a field an item kind has nowhere to write, instead of dropping it or writing it where TIA Portal has no such attribute.</summary>
    /// <remarks>
    /// The class exports show which attributes each element has: a <c>Hmi.Screen.Line</c> has a
    /// <c>Color</c> and no <c>ForeColor</c>, a rectangle has neither a foreground colour nor a font, and a
    /// group has nothing but its name and tab index. An attribute TIA Portal does not know fails the
    /// import, and a failed classic import has ended the project session; a field silently dropped leaves
    /// the caller believing it was applied.
    /// </remarks>
    internal static class HmiFieldsNotTaken
    {
        private static readonly string[] Text = { "foreColor", "fontSize", "fontStyle", "align" };

        private static readonly Dictionary<string, string[]> ByKind = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["rectangle"] = Text,
            ["circle"] = Text,
            ["pilotLight"] = Text,
            ["graphicView"] = Text,
            ["line"] = new[] { "fontSize", "fontStyle", "align", "width", "height" },
            ["group"] = new[] { "left", "top", "width", "height", "backColor", "foreColor", "fontSize", "fontStyle", "align" }
        };

        private static readonly Dictionary<string, Func<HmiScreenItemDocument, bool>> IsGiven = new Dictionary<string, Func<HmiScreenItemDocument, bool>>(StringComparer.Ordinal)
        {
            ["left"] = item => item.Left.HasValue,
            ["top"] = item => item.Top.HasValue,
            ["width"] = item => item.Width.HasValue,
            ["height"] = item => item.Height.HasValue,
            ["backColor"] = item => item.BackColor != null,
            ["foreColor"] = item => item.ForeColor != null,
            ["fontSize"] = item => item.FontSize.HasValue,
            ["fontStyle"] = item => item.FontStyle != null,
            ["align"] = item => item.Align != null
        };

        /// <summary>Throws when an item gives a field its kind does not take.</summary>
        /// <param name="kind">The item's kind, as the description names it.</param>
        /// <param name="item">The item.</param>
        /// <param name="where">The item, as a refusal names it.</param>
        /// <exception cref="TiaMcpServer.Siemens.PortalException">A field the kind does not take is given.</exception>
        public static void Refuse(string kind, HmiScreenItemDocument item, string where)
        {
            if (!ByKind.TryGetValue(kind ?? string.Empty, out var notTaken))
            {
                return;
            }

            var given = notTaken.Where(field => IsGiven[field](item)).ToList();
            if (given.Count > 0)
            {
                throw HmiRefusal.Refuse(where, $"a {kind} takes no {string.Join(", ", given.Select(field => $"'{field}'"))}{Hint(kind!)}");
            }
        }

        private static string Hint(string kind)
        {
            if (kind.Equals("line", StringComparison.OrdinalIgnoreCase))
            {
                return "; it runs from 'left', 'top' to 'endLeft', 'endTop', and 'foreColor' is its colour";
            }

            return kind.Equals("group", StringComparison.OrdinalIgnoreCase) ? "; place and colour its items" : string.Empty;
        }
    }
}
