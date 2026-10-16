using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>Writes a classic WinCC HMI tag table as the XML TIA Portal imports.</summary>
    /// <remarks>
    /// The shape is the smallest one measured to import and compile with 0 errors and 0 warnings
    /// (2026-10-13, into a copy of the teacher's <c>0967</c> project): per tag, a name and three
    /// links by name — acquisition cycle, connection, PLC tag. Every attribute left out takes its
    /// default, and the data type is taken from the PLC tag.
    ///
    /// Links use <c>TargetID="@OpenLink"</c>, which is how an export names an object outside the
    /// document: TIA Portal looks it up by name. How a name that does not exist is reported, at the
    /// import or at the HMI compile, has not been measured.
    /// </remarks>
    public static class HmiTagTableDocument
    {
        /// <summary>The acquisition cycle the class projects use for every tag.</summary>
        public const string DefaultAcquisitionCycle = "1 s";

        /// <summary>The name TIA Portal gives the first integrated HMI connection.</summary>
        public const string DefaultConnection = "HMI_Connection_1";

        private const string EngineeringVersion = "V20";
        private const string OpenLink = "@OpenLink";

        /// <summary>Builds the document.</summary>
        /// <param name="tableName">The HMI tag table's name.</param>
        /// <param name="connectionName">The HMI connection every tag reads through.</param>
        /// <param name="links">The tags, in the order they are written.</param>
        /// <returns>The XML text, UTF-8 declared, indented as TIA Portal exports it.</returns>
        /// <exception cref="ArgumentException">A name is empty, or there are no tags.</exception>
        public static string Build(string tableName, string connectionName, IReadOnlyList<HmiTagLink> links)
        {
            RequireName(tableName, nameof(tableName));
            RequireName(connectionName, nameof(connectionName));

            if (links == null || links.Count == 0)
            {
                throw new ArgumentException("A tag table needs at least one tag", nameof(links));
            }

            var tags = links.Select((link, index) => Tag(link, connectionName, index + 1));
            var table = new XElement(
                "Hmi.Tag.TagTable",
                new XAttribute("ID", Id(0)),
                new XElement("AttributeList", new XElement("Name", tableName)),
                new XElement("ObjectList", tags));

            return HmiXmlText.Serialise(new XDocument(new XElement("Document", new XElement("Engineering", new XAttribute("version", EngineeringVersion)), table)));
        }

        private static XElement Tag(HmiTagLink link, string connectionName, int index)
        {
            RequireName(link.HmiTagName, nameof(link.HmiTagName));
            RequireName(link.ControllerTagName, nameof(link.ControllerTagName));

            return new XElement(
                "Hmi.Tag.Tag",
                new XAttribute("ID", Id(index)),
                new XAttribute("CompositionName", "Tags"),
                new XElement("AttributeList", new XElement("Name", link.HmiTagName)),
                new XElement(
                    "LinkList",
                    Link("AcquisitionCycle", DefaultAcquisitionCycle),
                    Link("Connection", connectionName),
                    Link("ControllerTag", link.ControllerTagName)));
        }

        private static XElement Link(string role, string targetName)
        {
            return new XElement(role, new XAttribute("TargetID", OpenLink), new XElement("Name", targetName));
        }

        /// <remarks>Hexadecimal, as TIA Portal numbers the objects of an export.</remarks>
        private static string Id(int index)
        {
            return index.ToString("X", CultureInfo.InvariantCulture);
        }

        private static void RequireName(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{parameterName} is required", parameterName);
            }
        }
    }
}
