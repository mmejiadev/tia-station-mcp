using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>
    /// What a classic WinCC export links to by name, sorted by the kind of object linked to.
    /// </summary>
    /// <remarks>
    /// Read before anything is imported, because a link to something that does not exist is not a
    /// compile error to fix later. Measured on 2026-10-14: importing an HMI tag table whose tags name
    /// a connection the panel lacks threw <c>NonRecoverableException</c>, and TIA Portal disposed of
    /// the open project — every later call failed until it was opened again.
    ///
    /// Every <c>@OpenLink</c> is read, not only the kinds expected, so that the caller can refuse a
    /// kind it does not know how to check instead of letting it through. The kinds are named after
    /// what they link to: a function parameter's <c>Value</c> is a <see cref="ScreenKind"/> under
    /// <c>Screen name</c> and a <see cref="TagKind"/> otherwise; the pictures of a button, on and
    /// off, are <see cref="PictureKind"/>; a graphic I/O field's <c>PictureList</c> is a
    /// <see cref="GraphicListKind"/>. Any other link keeps the name of its role. The roles listed are
    /// every one in the class exports of 2026-10-13.
    /// </remarks>
    public sealed class HmiDocumentLinks
    {
        /// <summary>A link to an HMI tag.</summary>
        public const string TagKind = "Tag";

        /// <summary>A link to a screen.</summary>
        public const string ScreenKind = "Screen";

        /// <summary>A link to a text list.</summary>
        public const string TextListKind = "TextList";

        /// <summary>A link to a graphic list.</summary>
        public const string GraphicListKind = "GraphicList";

        /// <summary>A link to an HMI connection.</summary>
        public const string ConnectionKind = "Connection";

        /// <summary>A link to a screen template.</summary>
        public const string TemplateKind = "Template";

        /// <summary>A link to a graphic of the project.</summary>
        public const string PictureKind = "Picture";

        /// <summary>A link to an acquisition cycle.</summary>
        public const string CycleKind = "Cycle";

        /// <summary>A link to a data type.</summary>
        public const string DataTypeKind = "DataType";

        /// <summary>A link to a PLC tag.</summary>
        public const string ControllerTagKind = "ControllerTag";

        private const string OpenLink = "@OpenLink";

        private static readonly Dictionary<string, string> KindOfRole = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Picture"] = PictureKind,
            ["PictureOn"] = PictureKind,
            ["PictureOff"] = PictureKind,
            ["PictureList"] = GraphicListKind,
            ["AcquisitionCycle"] = CycleKind,
            ["HmiDataType"] = DataTypeKind
        };

        private readonly Dictionary<string, List<string>> _byKind;

        private HmiDocumentLinks(Dictionary<string, List<string>> byKind)
        {
            _byKind = byKind;
        }

        /// <summary>Every kind of object the document links to.</summary>
        public IReadOnlyCollection<string> Kinds => _byKind.Keys;

        /// <summary>The names linked to of one kind.</summary>
        /// <param name="kind">One of the <c>…Kind</c> constants, or a role this class does not name.</param>
        /// <returns>Each name once; empty when the document has no link of that kind.</returns>
        public IReadOnlyList<string> Named(string kind)
        {
            return _byKind.TryGetValue(kind, out var names) ? names : new List<string>();
        }

        /// <summary>Reads the links of an export: a screen, a tag table, a text list.</summary>
        /// <param name="xml">The document.</param>
        /// <returns>Its links.</returns>
        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        public static HmiDocumentLinks Read(string xml)
        {
            var byKind = XDocument.Parse(xml).Descendants()
                .Where(element => (string?)element.Attribute("TargetID") == OpenLink)
                .Select(element => new { Kind = Kind(element), Name = element.Element("Name")?.Value ?? string.Empty })
                .Where(link => link.Name.Length > 0)
                .GroupBy(link => link.Kind, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(link => link.Name).Distinct(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);

            return new HmiDocumentLinks(byKind);
        }

        /// <summary>The name of what a document defines: the screen, tag table or text list it imports as.</summary>
        /// <param name="xml">The document.</param>
        /// <returns>The name, or empty when it has none.</returns>
        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        public static string NameOf(string xml)
        {
            var root = XDocument.Parse(xml).Root?.Elements().FirstOrDefault(element => element.Element("AttributeList") != null);

            return root?.Element("AttributeList")?.Element("Name")?.Value ?? string.Empty;
        }

        /// <summary>Every name of one kind that a set of exports links to.</summary>
        /// <param name="exports">Exported documents of the panel: its tag tables, screens, templates.</param>
        /// <param name="kind">The kind.</param>
        /// <returns>The names, each once.</returns>
        /// <remarks>
        /// The evidence that an object the API does not list exists: an integrated connection is not in
        /// <c>HmiTarget.Connections</c>, and the project's graphics are not reachable at all, but the tags
        /// and screens that use them are.
        /// </remarks>
        public static IReadOnlyList<string> UsedBy(IEnumerable<string> exports, string kind)
        {
            return exports.SelectMany(xml => Read(xml).Named(kind)).Distinct(StringComparer.Ordinal).ToList();
        }

        private static string Kind(XElement link)
        {
            var role = link.Name.LocalName;

            if (role == "Value")
            {
                var parameter = link.Parent?.Parent?.Element("AttributeList")?.Element("Name")?.Value;

                return parameter == HmiFunctionCall.ScreenParameter ? ScreenKind : TagKind;
            }

            return KindOfRole.TryGetValue(role, out var kind) ? kind : role;
        }
    }
}
