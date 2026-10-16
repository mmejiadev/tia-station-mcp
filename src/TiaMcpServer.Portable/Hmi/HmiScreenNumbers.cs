using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>The number of every screen of a panel, read from its export.</summary>
    /// <remarks>
    /// From the export because the API does not give it: reading <c>Number</c> from a classic
    /// screen throws "'get_Number' is not supported" (measured 2026-10-14). Every HMI write exports
    /// the whole panel first, so the numbers are there to read.
    /// </remarks>
    public static class HmiScreenNumbers
    {
        /// <summary>Reads screen numbers from exported documents.</summary>
        /// <param name="exports">Exported documents; anything that is not a screen with a number is skipped.</param>
        /// <returns>Each screen's name and number.</returns>
        /// <exception cref="System.Xml.XmlException">A document is not XML.</exception>
        public static IReadOnlyList<KeyValuePair<string, int>> Read(IEnumerable<string> exports)
        {
            return exports
                .Select(xml => XDocument.Parse(xml).Root?.Element("Hmi.Screen.Screen")?.Element("AttributeList"))
                .Where(attributes => attributes?.Element("Name") != null && attributes.Element("Number") != null)
                .Select(attributes => new KeyValuePair<string, int>(
                    attributes!.Element("Name")!.Value,
                    int.Parse(attributes.Element("Number")!.Value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                .ToList();
        }
    }
}
