using Siemens.Engineering.HmiUnified.HmiTags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads a WinCC Unified HMI's connections and tags.</summary>
    /// <remarks>
    /// Through the typed properties only: <c>GetAttributeInfos()</c> on a new tag ends the TIA Portal
    /// session ("Property : ConfirmationType not found", measured 2026-10-22).
    /// </remarks>
    internal static class UnifiedTagReader
    {
        /// <summary>Finds a tag table by its name or, for one in a group, by the path GetUnifiedTags prints.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="name">The table's name or path, in any case.</param>
        /// <returns>The table and its path, or null when there is none.</returns>
        public static (string Path, HmiTagTable Table)? FindTable(UnifiedHmiSoftware hmi, string name)
        {
            foreach (var entry in Tables(hmi))
            {
                if (entry.Path.Equals(name, StringComparison.OrdinalIgnoreCase) || entry.Table.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>Reads the connections and every tag, table by table.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <returns><c>{ connections: [...], tags: [...] }</c>.</returns>
        public static JsonObject Read(UnifiedHmiSoftware hmi)
        {
            var connections = hmi.Connections.Select(connection => (JsonNode)new JsonObject
            {
                ["name"] = connection.Name,
                ["partner"] = connection.Partner,
                ["station"] = connection.Station,
                ["driver"] = connection.CommunicationDriver,
                ["integrated"] = !string.IsNullOrEmpty(connection.Partner)
            }).ToArray();

            var tags = Tables(hmi).SelectMany(entry => entry.Table.Tags.Select(tag => (JsonNode)Describe(tag, entry.Path))).ToArray();

            return new JsonObject { ["connections"] = new JsonArray(connections), ["tags"] = new JsonArray(tags) };
        }

        /// <summary>Describes one tag.</summary>
        /// <param name="tag">The tag.</param>
        /// <param name="table">The path of its table.</param>
        /// <returns>Its name, table, data type and, when linked, connection, PLC, PLC tag and address.</returns>
        public static JsonObject Describe(HmiTag tag, string table)
        {
            var described = new JsonObject { ["name"] = tag.Name, ["table"] = table, ["dataType"] = tag.DataType };

            if (!IsInternal(tag.Connection))
            {
                described["connection"] = tag.Connection;
                described["plcName"] = tag.PlcName;
                described["plcTag"] = tag.PlcTag;
                described["address"] = tag.Address;
            }

            return described;
        }

        /// <summary>The path of the table a tag is in.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="tag">The tag.</param>
        /// <returns>The table's path, groups included.</returns>
        /// <remarks>
        /// A tag's <c>Parent</c> is the HMI, not its table (measured 2026-10-22), so the tables are searched,
        /// by name: <c>Contains</c> with the object just created found it in no table (measured 2026-10-22).
        /// Tag names are unique on the HMI, in any case.
        /// </remarks>
        public static string TableOf(UnifiedHmiSoftware hmi, HmiTag tag)
        {
            return Tables(hmi).FirstOrDefault(entry => entry.Table.Tags.Find(tag.Name) != null).Path ?? string.Empty;
        }

        private static bool IsInternal(string connection)
        {
            return string.IsNullOrEmpty(connection) || connection.StartsWith("<", StringComparison.Ordinal);
        }

        private static IEnumerable<(string Path, HmiTagTable Table)> Tables(UnifiedHmiSoftware hmi)
        {
            return hmi.TagTables.Select(table => (table.Name, table)).Concat(InGroups(hmi.TagTableGroups, string.Empty));
        }

        private static IEnumerable<(string Path, HmiTagTable Table)> InGroups(HmiTagTableGroupComposition groups, string prefix)
        {
            foreach (var group in groups)
            {
                var path = $"{prefix}{group.Name}/";

                foreach (var table in group.TagTables)
                {
                    yield return (path + table.Name, table);
                }

                foreach (var nested in InGroups(group.Groups, path))
                {
                    yield return nested;
                }
            }
        }
    }
}
