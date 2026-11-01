using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.UI.Controls;
using Siemens.Engineering.HmiUnified.UI.Parts;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads WinCC Unified trend controls: each area's trends, their sources, and the time range.</summary>
    internal static class UnifiedTrendReader
    {
        /// <summary>Every trend control on the screens given.</summary>
        /// <param name="screens">The screens, screen groups included, as the portal walks them.</param>
        /// <param name="language">The project's editing language, which a trend's name is read in.</param>
        /// <returns>One object per trend control, with its screen.</returns>
        public static JsonArray Read(IEnumerable<HmiScreen> screens, Language language)
        {
            return new JsonArray(screens.SelectMany(screen => screen.ScreenItems.OfType<HmiTrendControl>().Select(control =>
            {
                var described = Describe(control, language);
                described["screen"] = screen.Name;
                return (JsonNode)described;
            })).ToArray());
        }

        /// <summary>Describes one trend control.</summary>
        /// <param name="control">The control.</param>
        /// <param name="language">The project's editing language, which a trend's name is read in.</param>
        /// <returns><c>{ control, areas: [ { name, trends: [ { source, name, color, mode } ], timeRange } ] }</c>.</returns>
        public static JsonObject Describe(HmiTrendControl control, Language language)
        {
            return new JsonObject
            {
                ["control"] = control.Name,
                ["areas"] = new JsonArray(control.TrendAreas.Select(area => (JsonNode)DescribeArea(area, language)).ToArray())
            };
        }

        private static JsonObject DescribeArea(HmiTrendAreaPart area, Language language)
        {
            var described = new JsonObject
            {
                ["name"] = area.Name,
                ["trends"] = new JsonArray(area.Trends.Select(trend => (JsonNode)new JsonObject
                {
                    ["source"] = trend.DataSourceY.Source ?? string.Empty,
                    ["name"] = UnifiedRichText.Read(trend.DisplayName.Items.Find(language)?.Text ?? string.Empty),
                    ["color"] = UnifiedPropertyConverter.Describe(trend.LineColor)!.Value.GetString(),
                    ["mode"] = trend.TrendMode.ToString()
                }).ToArray())
            };

            if (area.BottomTimeAxes.Count > 0)
            {
                described["timeRange"] = new JsonObject { ["factor"] = area.BottomTimeAxes[0].TimeRangeFactor, ["base"] = area.BottomTimeAxes[0].TimeRangeBase.ToString() };
            }

            return described;
        }
    }
}
