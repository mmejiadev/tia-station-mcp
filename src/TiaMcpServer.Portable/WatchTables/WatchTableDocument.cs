using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.WatchTables
{
    /// <summary>
    /// Writes a watch or force table with its rows as the SimaticML document TIA Portal imports.
    /// </summary>
    /// <remarks>
    /// Openness creates no row in a table, so rows go in as a document, the way LAD blocks do. The shape is the one
    /// TIA Portal V21 exported on 2026-11-13 after a probe imported rows into a project: a table element with its
    /// name, and one entry element per row in <c>Entries</c> holding <c>Address</c>, <c>DisplayFormat</c>, the
    /// prepared value and its trigger, <c>MonitorTrigger</c>, and a comment. The intention to force or modify is
    /// left out: Openness answers "The property 'ForceIntention' is read-only", so the box is ticked online.
    /// Triggers are <c>Permanent</c>, the one written and read back.
    /// </remarks>
    public static class WatchTableDocument
    {
        private const string Trigger = "Permanent";
        private const string CommentCulture = "en-US";

        /// <summary>Writes the document.</summary>
        /// <param name="kind">The table it is.</param>
        /// <param name="tableName">The table's name; the force table's is the one TIA Portal gave it.</param>
        /// <param name="rows">The rows, already checked by <see cref="WatchTableRowReader"/>.</param>
        /// <param name="tiaMajorVersion">The TIA Portal version the document is for, e.g. 21.</param>
        /// <returns>The document as text.</returns>
        public static string Build(WatchTableKind kind, string tableName, IReadOnlyList<WatchTableRow> rows, int tiaMajorVersion)
        {
            var ids = new IdSequence();
            var table = new XElement(
                kind == WatchTableKind.Force ? "SW.WatchAndForceTables.PlcForceTable" : "SW.WatchAndForceTables.PlcWatchTable",
                new XAttribute("ID", ids.Next()),
                new XElement("AttributeList", new XElement("Name", tableName)),
                new XElement("ObjectList", rows.Select(row => Entry(kind, row, ids))));

            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Document", new XElement("Engineering", new XAttribute("version", "V" + tiaMajorVersion.ToString(CultureInfo.InvariantCulture))), table));

            return document.Declaration + "\r\n" + document.Root;
        }

        private static XElement Entry(WatchTableKind kind, WatchTableRow row, IdSequence ids)
        {
            var isForce = kind == WatchTableKind.Force;
            var attributes = new XElement("AttributeList", new XElement("Address", row.Address), new XElement("DisplayFormat", row.DisplayFormat));

            if (isForce)
            {
                attributes.Add(new XElement("ForceValue", row.Value));
            }
            else if (row.Value.Length > 0)
            {
                attributes.Add(new XElement("ModifyTrigger", Trigger), new XElement("ModifyValue", row.Value));
            }

            attributes.Add(new XElement("MonitorTrigger", Trigger));

            return new XElement(
                isForce ? "SW.WatchAndForceTables.PlcForceTableEntry" : "SW.WatchAndForceTables.PlcWatchTableEntry",
                new XAttribute("ID", ids.Next()),
                new XAttribute("CompositionName", "Entries"),
                attributes,
                new XElement("ObjectList", Comment(row.Comment, ids)));
        }

        private static XElement Comment(string text, IdSequence ids)
        {
            return new XElement(
                "MultilingualText",
                new XAttribute("ID", ids.Next()),
                new XAttribute("CompositionName", "Comment"),
                new XElement(
                    "ObjectList",
                    new XElement(
                        "MultilingualTextItem",
                        new XAttribute("ID", ids.Next()),
                        new XAttribute("CompositionName", "Items"),
                        new XElement("AttributeList", new XElement("Culture", CommentCulture), new XElement("Text", text)))));
        }

        /// <remarks>SimaticML numbers the objects of a document; the numbers only have to differ.</remarks>
        private sealed class IdSequence
        {
            private int _next;

            public string Next()
            {
                return (_next++).ToString("X", CultureInfo.InvariantCulture);
            }
        }
    }
}
