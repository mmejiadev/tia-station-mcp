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
        /// <returns>One object per trend control, with its screen.</returns>
        public static JsonArray Read(IEnumerable<HmiScreen> screens)
        {
            return new JsonArray(screens.SelectMany(screen => screen.ScreenItems.OfType<HmiTrendControl>().Select(control =>
            {
                var described = Describe(control);
                described["screen"] = screen.Name;
                return (JsonNode)described;
            })).ToArray());
        }

        /// <summary>Describes one trend control.</summary>
        /// <param name="control">The control.</param>
        /// <returns><c>{ control, areas: [ { name, trends: [ { source, name, color, mode } ], timeRange } ] }</c>.</returns>
        public static JsonObject Describe(HmiTrendControl control)
        {
            return new JsonObject
            {
                ["control"] = control.Name,
                ["areas"] = new JsonArray(control.TrendAreas.Select(area => (JsonNode)DescribeArea(area)).ToArray())
            };
        }

        private static JsonObject DescribeArea(HmiTrendAreaPart area)
        {
            var described = new JsonObject
            {
                ["name"] = area.Name,
                ["trends"] = new JsonArray(area.Trends.Select(trend => (JsonNode)new JsonObject
                {
                    ["source"] = trend.DataSourceY.Source ?? string.Empty,
                    ["name"] = UnifiedRichText.Read(trend.DisplayName.Items.Select(item => item.Text).FirstOrDefault() ?? string.Empty),
                    ["color"] = $"#{trend.LineColor.R:X2}{trend.LineColor.G:X2}{trend.LineColor.B:X2}",
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
