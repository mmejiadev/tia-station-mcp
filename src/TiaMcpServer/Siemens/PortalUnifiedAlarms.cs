using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedAlarms;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// WinCC Unified alarm classes, discrete alarms and analog alarms: reading them, and creating new
    /// ones. See <see cref="UnifiedAlarmChecker"/> and <see cref="UnifiedAlarmWriter"/> for what was measured.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Reads a WinCC Unified HMI's alarm classes and alarms.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software, e.g. <c>PC-System_1/HMI_RT_1</c>.</param>
        /// <returns><c>{ classes: [...], discrete: [...], analog: [...] }</c>, texts in the project's editing language.</returns>
        /// <exception cref="PortalException">The path is not a WinCC Unified HMI.</exception>
        public JsonObject GetUnifiedAlarms(string hmiPath)
        {
            try
            {
                return UnifiedAlarmReader.Read(RequireUnifiedSoftware(hmiPath), CurrentProject!.LanguageSettings.EditingLanguage);
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "GetUnifiedAlarms", hmiPath, string.Empty);
            }
        }

        /// <summary>Creates WinCC Unified alarm classes and alarms, all of them or none.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <param name="list">The classes and alarms.</param>
        /// <returns>What was created, read back.</returns>
        /// <exception cref="PortalException">
        /// A name is taken; a trigger tag or class does not exist; a tag does not fit its alarm, a bit its tag
        /// or a value its tag's type; a tag and bit already raise an alarm; or TIA Portal refused one. In
        /// every case nothing is left created.
        /// </exception>
        /// <remarks>Only new objects are made and nothing existing changes, so no backup is taken.</remarks>
        public JsonObject CreateUnifiedAlarms(string hmiPath, UnifiedAlarmList list)
        {
            _logger?.LogInformation("Creating Unified alarms on {HmiPath}...", hmiPath);

            try
            {
                var requested = list ?? throw new PortalException(PortalErrorCode.InvalidParams, "An alarm list is required");
                var hmi = RequireUnifiedSoftware(hmiPath);
                var writer = new UnifiedAlarmWriter(hmi, CurrentProject!.LanguageSettings.EditingLanguage);

                writer.Check(requested);
                var created = writer.Create(requested);
                var all = UnifiedAlarmReader.Read(hmi, CurrentProject!.LanguageSettings.EditingLanguage);

                return new JsonObject
                {
                    ["classes"] = Only(all["classes"]!.AsArray(), created.Classes),
                    ["discrete"] = Only(all["discrete"]!.AsArray(), created.Discrete),
                    ["analog"] = Only(all["analog"]!.AsArray(), created.Analog)
                };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "CreateUnifiedAlarms"), "CreateUnifiedAlarms", hmiPath, string.Empty);
            }
        }

        private static JsonArray Only(JsonArray read, System.Collections.Generic.IReadOnlyList<string> names)
        {
            return new JsonArray(read.Where(entry => names.Contains(entry!["name"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase)).Select(entry => entry!.DeepClone()).ToArray());
        }
    }
}
