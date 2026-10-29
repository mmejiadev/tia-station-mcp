using Siemens.Engineering.HmiUnified.HmiAlarm;
using Siemens.Engineering.HmiUnified.HmiTags;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.UnifiedLogging;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>What a logging list touches on an HMI, read from TIA Portal once, before anything changes.</summary>
    /// <remarks>
    /// Each read is a call into TIA Portal while the Openness gate is held. Every HMI tag's logging tags are
    /// read only when the list creates a data log, since only then can an existing one name it; otherwise
    /// only those of the HMI tags the list logs. An HMI tag is found with Openness's Find, which takes the
    /// name as TIA Portal spells it.
    /// </remarks>
    internal sealed class UnifiedLoggingSnapshot
    {
        /// <summary>Reads what a list touches.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="list">The logs and logging tags to create.</param>
        public UnifiedLoggingSnapshot(UnifiedHmiSoftware hmi, UnifiedLoggingList list)
        {
            Classes = hmi.AlarmClasses.ToDictionary(found => found.Name, found => found, StringComparer.OrdinalIgnoreCase);
            Tags = list.LoggingTags
                .Select(logged => logged.Tag)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => (Name: name, Found: hmi.Tags.Find(name)))
                .Where(entry => entry.Found != null)
                .ToDictionary(entry => entry.Name, entry => entry.Found, StringComparer.OrdinalIgnoreCase);
            DataLogs = hmi.DataLogs.Select(log => log.Name).ToList();
            State = ReadState(hmi, list);
        }

        /// <summary>The HMI's alarm classes, system ones included, by name in any case.</summary>
        public IReadOnlyDictionary<string, HmiAlarmClass> Classes { get; }

        /// <summary>The HMI tags the list logs that exist, by the name the list gives them.</summary>
        public IReadOnlyDictionary<string, HmiTag> Tags { get; }

        /// <summary>The names of the data logs the HMI holds.</summary>
        public IReadOnlyList<string> DataLogs { get; }

        /// <summary>What the HMI holds, for <see cref="UnifiedLoggingRules"/>.</summary>
        public UnifiedLoggingState State { get; }

        private UnifiedLoggingState ReadState(UnifiedHmiSoftware hmi, UnifiedLoggingList list)
        {
            var tags = list.DataLogs.Count > 0 ? hmi.Tags.ToList() : Tags.Values.ToList();
            var loggingTags = tags
                .SelectMany(tag => tag.LoggingTags.Select(logged => (Tag: tag.Name, logged.Name, DataLog: logged.DataLog ?? string.Empty)))
                .ToList();

            return new UnifiedLoggingState(
                (DataLogs, hmi.AlarmLogs.Select(log => log.Name).ToList()),
                Classes.ToDictionary(entry => entry.Key, entry => entry.Value.Log ?? string.Empty, StringComparer.OrdinalIgnoreCase),
                Tags.Keys.ToList(),
                loggingTags);
        }
    }
}
