using Microsoft.Extensions.Logging;
using Siemens.Engineering.HmiUnified.UI.Controls;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedTrends;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// WinCC Unified trend controls: reading their trends, and filling one just drawn with trends of live or
    /// logged tags. See <see cref="UnifiedTrendWriter"/> and <see cref="UnifiedTrendRules"/> for what was measured.
    /// </remarks>
    public partial class Portal
    {
        private const string TrendBackupFile = "trend-control.json";

        /// <summary>Reads every trend control on a WinCC Unified HMI's screens.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software, e.g. <c>PC-System_1/HMI_RT_1</c>.</param>
        /// <returns><c>{ trendControls: [ { screen, control, areas } ] }</c>.</returns>
        /// <exception cref="PortalException">The path is not a WinCC Unified HMI.</exception>
        public JsonObject GetUnifiedTrends(string hmiPath)
        {
            try
            {
                return new JsonObject { ["trendControls"] = UnifiedTrendReader.Read(AllScreens(RequireUnifiedSoftware(hmiPath))) };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "GetUnifiedTrends", hmiPath, string.Empty);
            }
        }

        /// <summary>Fills a WinCC Unified trend control that shows no trend yet, all of a list or none of it.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <param name="list">The screen, the control, its trends and its time range.</param>
        /// <param name="backupDirectory">Where the control as it was is written.</param>
        /// <returns>The control, read back.</returns>
        /// <exception cref="PortalException">
        /// The screen, the control, a tag or a logging tag does not exist; the control already shows a trend; or TIA
        /// Portal refused a value. In every case the control is left as it was.
        /// </exception>
        public JsonObject SetUnifiedTrends(string hmiPath, UnifiedTrendList list, string backupDirectory)
        {
            _logger?.LogInformation("Setting Unified trends on {HmiPath}...", hmiPath);

            try
            {
                var requested = list ?? throw new PortalException(PortalErrorCode.InvalidParams, "A trend list is required");
                var hmi = RequireUnifiedSoftware(hmiPath);
                var control = RequireTrendControl(FindUnifiedScreen(hmi, hmiPath, requested.Screen), requested);
                var writer = new UnifiedTrendWriter(hmi, control, requested);

                writer.Check();
                SaveTrendBackup(backupDirectory, UnifiedTrendReader.Describe(control));

                return writer.Write();
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "SetUnifiedTrends"), "SetUnifiedTrends", hmiPath, string.Empty);
            }
        }

        private static HmiTrendControl RequireTrendControl(global::Siemens.Engineering.HmiUnified.UI.Screens.HmiScreen screen, UnifiedTrendList list)
        {
            var item = screen.ScreenItems.Find(list.Control)
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"The screen '{screen.Name}' has no item '{list.Control}'; draw a trend control with DrawUnifiedScreen first. Trend controls on it: {string.Join(", ", screen.ScreenItems.OfType<HmiTrendControl>().Select(found => found.Name))}");

            return item as HmiTrendControl
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"'{list.Control}' on '{screen.Name}' is a {item.GetType().Name}, not a trend control");
        }

        private static void SaveTrendBackup(string backupDirectory, JsonObject before)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: the trend control is read into it before it changes");
            }

            Directory.CreateDirectory(backupDirectory);
            File.WriteAllText(Path.Combine(backupDirectory, TrendBackupFile), before.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
