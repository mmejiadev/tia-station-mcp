using Siemens.Engineering.Library.Types;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads where the project uses its library types, from TIA Portal's update check.</summary>
    /// <remarks>
    /// Measured 2026-11-15 on TIA Portal V21: <c>ProjectLibrary.UpdateCheck(project, ReportOutOfDateAndUpToDate)</c>
    /// answers with a tree of messages - a device (<c>DeviceName</c>), a type and its library version
    /// (<c>LibraryTypeName</c>, <c>LibraryVersionNumber</c>), a status ("Up to date:"), and one message per instance
    /// (<c>PathToInstance</c>, <c>InstanceName</c>, <c>CurrentVersionNumber</c>). It reaches PLC blocks and data types and
    /// WinCC Unified faceplates alike, where <c>FindInstances</c> searches only a PLC or a classic HMI. The parts are read
    /// by their keys and carried down the tree, so a level TIA Portal adds or drops does not shift the columns.
    /// </remarks>
    internal static class LibraryUsageReader
    {
        private const string InstancePathKey = "PathToInstance";
        private const string DeviceKey = "DeviceName";
        private const string TypeKey = "LibraryTypeName";
        private const string LibraryVersionKey = "LibraryVersionNumber";
        private const string InstanceVersionKey = "CurrentVersionNumber";
        private const string InstanceNameKey = "InstanceName";

        /// <summary>Turns the update check into one row per instance.</summary>
        /// <param name="result">What <c>UpdateCheck</c> returned.</param>
        /// <returns>Each instance with its device, type, library version, its own version and the status TIA Portal gave.</returns>
        internal static JsonArray Read(UpdateCheckResult result)
        {
            var rows = new JsonArray();

            Walk(result.Messages, new Dictionary<string, string>(), string.Empty, rows);

            return rows;
        }

        private static void Walk(UpdateCheckResultMessageComposition messages, Dictionary<string, string> inherited, string status, JsonArray rows)
        {
            foreach (var message in messages)
            {
                // The nearer level wins: a type nested under another names the instances below it, not the outer one.
                var parts = new Dictionary<string, string>(inherited);
                foreach (var part in message.MessageParts)
                {
                    parts[part.Key] = part.Value ?? string.Empty;
                }

                if (parts.ContainsKey(InstancePathKey))
                {
                    rows.Add(Row(parts, status));
                    continue;
                }

                Walk(message.Messages, parts, StatusOf(message) ?? status, rows);
            }
        }

        /// <remarks>
        /// The status is a level whose parts name neither a device, a type nor an instance - measured as one part,
        /// <c>UpToDate = Up-to-date</c> - and its value is what is reported. A level with no parts at all, a heading or a
        /// grouping, is not a status and leaves the one above in place.
        /// </remarks>
        private static string? StatusOf(UpdateCheckResultMessage message)
        {
            var statusParts = message.MessageParts
                .Where(part => part.Key != DeviceKey && part.Key != TypeKey && part.Key != LibraryVersionKey && part.Key != InstancePathKey && part.Key != InstanceVersionKey && part.Key != InstanceNameKey)
                .ToList();

            return statusParts.Count == 0 ? null : statusParts[0].Value;
        }

        private static JsonObject Row(Dictionary<string, string> parts, string status)
        {
            return new JsonObject
            {
                ["type"] = Part(parts, TypeKey),
                ["libraryVersion"] = Part(parts, LibraryVersionKey),
                ["device"] = Part(parts, DeviceKey),
                ["instance"] = Part(parts, InstancePathKey),
                ["instanceName"] = Part(parts, InstanceNameKey),
                ["instanceVersion"] = Part(parts, InstanceVersionKey),
                ["status"] = status
            };
        }

        private static string Part(Dictionary<string, string> parts, string key)
        {
            return parts.TryGetValue(key, out var value) ? value : string.Empty;
        }
    }
}
