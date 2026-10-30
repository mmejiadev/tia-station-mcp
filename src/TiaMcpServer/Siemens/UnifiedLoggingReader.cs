using Siemens.Engineering.HmiUnified.LoggingTags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedAlarms;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads a WinCC Unified HMI's data logs, alarm logs and logging tags, in the shape CreateUnifiedLogging takes them.</summary>
    internal static class UnifiedLoggingReader
    {
        /// <summary>Reads the logs and logging tags.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="includeLoggingTags">False to leave the logging tags out, which are read tag by tag.</param>
        /// <returns><c>{ dataLogs: [...], alarmLogs: [...], loggingTags: [...] }</c>.</returns>
        /// <remarks>
        /// An alarm log's classes are the classes whose <c>Log</c> names it: the link lives on the class.
        /// A logging tag whose data log is gone still names it, as TIA Portal keeps the name, so a reader
        /// can see what the compile will refuse. A mode of <c>undefined</c> is read as it is, though
        /// CreateUnifiedLogging makes none. Reading the logging tags is a call into TIA Portal per HMI tag
        /// while the Openness gate is held, so on an HMI with thousands of tags it takes a while.
        /// </remarks>
        public static JsonObject Read(UnifiedHmiSoftware hmi, bool includeLoggingTags = true)
        {
            var classes = hmi.AlarmClasses.Select(found => (found.Name, Log: found.Log ?? string.Empty)).ToList();

            return new JsonObject
            {
                ["dataLogs"] = new JsonArray(hmi.DataLogs.Select(log => (JsonNode)new JsonObject { ["name"] = log.Name }).ToArray()),
                ["alarmLogs"] = new JsonArray(hmi.AlarmLogs.Select(log => (JsonNode)new JsonObject
                {
                    ["name"] = log.Name,
                    ["classes"] = new JsonArray(classes.Where(found => string.Equals(found.Log, log.Name, StringComparison.OrdinalIgnoreCase)).Select(found => (JsonNode)found.Name).ToArray())
                }).ToArray()),
                ["loggingTags"] = includeLoggingTags
                    ? new JsonArray(hmi.Tags.SelectMany(tag => tag.LoggingTags.Select(logged => (JsonNode)Describe(tag.Name, logged))).ToArray())
                    : new JsonArray()
            };
        }

        /// <summary>Reads only the logging tags named, each on its HMI tag.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="named">Each logging tag's HMI tag and name.</param>
        /// <returns>The logging tags found, in the shape <see cref="Read"/> gives them.</returns>
        /// <remarks>
        /// After a create, rather than every tag's logging tags: each is a call into TIA Portal while the
        /// Openness gate is held, and an HMI can have thousands of tags.
        /// </remarks>
        public static JsonArray ReadLoggingTags(UnifiedHmiSoftware hmi, IEnumerable<(string Tag, string Name)> named)
        {
            var found = named
                .Select(entry => (Tag: hmi.Tags.Find(entry.Tag), entry.Name))
                .Where(entry => entry.Tag != null)
                .SelectMany(entry => entry.Tag.LoggingTags
                    .Where(logged => string.Equals(logged.Name, entry.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(logged => (JsonNode)Describe(entry.Tag.Name, logged)));

            return new JsonArray(found.ToArray());
        }

        private static JsonObject Describe(string tag, HmiLoggingTag logged)
        {
            var entry = new JsonObject
            {
                ["tag"] = tag,
                ["name"] = logged.Name,
                ["dataLog"] = logged.DataLog,
                ["mode"] = UnifiedAlarmJson.Camel(logged.LoggingMode.ToString())
            };

            // Only a cyclic logging tag gives its cycle, as CreateUnifiedLogging takes one only there. The factor
            // goes with it: T1s with a factor of 5 writes every 5 s, and the cycle alone would say every second.
            if (logged.LoggingMode == HmiLoggingMode.Cyclic && !string.IsNullOrEmpty(logged.Cycle))
            {
                entry["cycle"] = logged.Cycle;
                entry["cycleFactor"] = logged.CycleFactor;
            }

            return entry;
        }
    }
}
