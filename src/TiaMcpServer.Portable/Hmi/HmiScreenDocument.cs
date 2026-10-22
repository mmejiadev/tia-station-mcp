using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>Writes a classic WinCC screen as the XML ImportHmiScreen takes.</summary>
    /// <remarks>
    /// One <c>ScreenLayer</c> per layer in use, lowest index first, each with an empty name as every
    /// export of the class projects writes them. The screen's attributes are the ones an export of the
    /// teacher's <c>principal</c> carries, less the size, which the panel decides.
    /// </remarks>
    public static class HmiScreenDocument
    {
        private const string EngineeringVersion = "V20";

        /// <summary>Builds the document.</summary>
        /// <param name="screen">The screen and its items.</param>
        /// <returns>The XML text, UTF-8 declared and indented.</returns>
        public static string Build(HmiScreenDescription screen)
        {
            var writer = new HmiScreenWriter();
            var root = writer.Node("Hmi.Screen.Screen", string.Empty, () => new[]
            {
                HmiScreenWriter.Attributes(ScreenAttributes(screen)),
                HmiScreenWriter.Objects(screen.Layers.Select(layer => WriteLayer(writer, layer)).ToList())
            });

            return HmiXmlText.Serialise(new XDocument(new XElement("Document", new XElement("Engineering", new XAttribute("version", EngineeringVersion)), root)));
        }

        /// <remarks>
        /// The number is left out when the description gives none: the Openness help says a screen
        /// imported without one gets a free number, and a number already taken cancels the import.
        /// </remarks>
        private static Dictionary<string, string> ScreenAttributes(HmiScreenDescription screen)
        {
            var attributes = new Dictionary<string, string>
            {
                ["ActiveLayer"] = "0",
                ["BackColor"] = screen.BackColor,
                ["GridColor"] = "0, 0, 0",
                ["Name"] = screen.Name,
                ["Visible"] = "true"
            };

            if (screen.Number.HasValue)
            {
                attributes["Number"] = HmiScreenWriter.Number(screen.Number.Value);
            }

            return attributes;
        }

        private static XElement WriteLayer(HmiScreenWriter writer, HmiScreenLayer layer)
        {
            return writer.Node("Hmi.Screen.ScreenLayer", "Layers", () => new[]
            {
                HmiScreenWriter.Attributes(new Dictionary<string, string> { ["Index"] = HmiScreenWriter.Number(layer.Index), ["Name"] = string.Empty, ["VisibleES"] = "true" }),
                HmiScreenWriter.Objects(layer.Items.Select(item => item.Write(writer)).ToList())
            });
        }
    }
}
