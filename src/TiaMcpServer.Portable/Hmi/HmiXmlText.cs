using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>Turns a generated document into the text written to disk for TIA Portal to import.</summary>
    internal static class HmiXmlText
    {
        /// <summary>Serialises a document: UTF-8 declared, no byte order mark, indented as TIA Portal exports.</summary>
        /// <param name="document">The document.</param>
        /// <returns>Its text.</returns>
        public static string Serialise(XDocument document)
        {
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };

            using var buffer = new MemoryStream();
            using (var writer = XmlWriter.Create(buffer, settings))
            {
                document.Save(writer);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
