using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedLogging;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The settings of WinCC Unified data logs and alarm logs: size, period, segments and backup. See
    /// <see cref="UnifiedLogSettingsPlan"/> for the order TIA Portal needs them written in.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Changes the settings of WinCC Unified logs, all of them or none.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software, e.g. <c>PC-System_1/HMI_RT_1</c>.</param>
        /// <param name="requests">The settings asked, per log.</param>
        /// <param name="backupDirectory">Where every log's settings before are saved.</param>
        /// <returns><c>{ logs: [ { name, maxSize, duration, segment, backupPath } ] }</c>, read back.</returns>
        /// <exception cref="PortalException">
        /// A log does not exist, would end with a segment above it or a backup to nowhere, or TIA Portal refused a
        /// write; in every case the logs keep the settings they had.
        /// </exception>
        public JsonObject SetUnifiedLogSettings(string hmiPath, IReadOnlyList<UnifiedLogSettingsRequest> requests, string backupDirectory)
        {
            _logger?.LogInformation("Setting Unified log settings on {HmiPath}...", hmiPath);

            try
            {
                var requested = requests ?? throw new PortalException(PortalErrorCode.InvalidParams, "Log settings are required");
                var writer = new UnifiedLogSettingsWriter(RequireUnifiedSoftware(hmiPath), requested);
                var after = writer.Apply(backupDirectory);

                return new JsonObject
                {
                    ["logs"] = new JsonArray(after.Select(log =>
                    {
                        var described = UnifiedLogSettingsAccess.Describe(log.Settings);
                        described["name"] = log.Name;
                        return (JsonNode)described;
                    }).ToArray())
                };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "SetUnifiedLogSettings"), "SetUnifiedLogSettings", hmiPath, string.Empty);
            }
        }
    }
}
