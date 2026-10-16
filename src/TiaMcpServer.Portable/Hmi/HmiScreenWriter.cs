using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>
    /// Writes the pieces every screen item is made of, in the shape a classic WinCC export has, and
    /// numbers them while it does.
    /// </summary>
    /// <remarks>
    /// One writer per screen: it holds the object numbering and the tab order, which are per
    /// document. Every shape here was read from an export of the class projects (2026-10-13):
    /// attributes listed alphabetically, objects numbered in hexadecimal parent first, links to
    /// tags, screens and functions written by name with <c>TargetID="@OpenLink"</c>, texts as
    /// <c>&lt;body&gt;&lt;p&gt;…&lt;/p&gt;&lt;/body&gt;</c> in the <c>en-US</c> culture the class
    /// projects are edited in.
    /// </remarks>
    internal sealed class HmiScreenWriter
    {
        /// <summary>The tab index of an item the operator cannot focus.</summary>
        public const int NotFocusable = -1;

        private const string OpenLink = "@OpenLink";
        private const string Culture = "en-US";
        private const string FontFamily = "Tahoma";
        private const string ScreenItemsComposition = "ScreenItems";

        private int _nextId;
        private int _nextTabIndex = 1;

        /// <summary>The next tab index, for an item the operator can focus: a button or an I/O field.</summary>
        /// <returns>1 for the first such item on the screen, then 2, and so on.</returns>
        public int NextTabIndex()
        {
            return _nextTabIndex++;
        }

        /// <summary>Writes one object, numbering it before its children so the parent gets the lower number.</summary>
        /// <param name="type">The element name, for example <c>Hmi.Screen.Button</c>.</param>
        /// <param name="composition">The role in its parent, or empty for the screen itself.</param>
        /// <param name="content">Its attribute, object and link lists; null entries are skipped.</param>
        /// <returns>The element.</returns>
        public XElement Node(string type, string composition, Func<IEnumerable<XElement?>> content)
        {
            var element = new XElement(type, new XAttribute("ID", (_nextId++).ToString("X", CultureInfo.InvariantCulture)));

            if (composition.Length > 0)
            {
                element.Add(new XAttribute("CompositionName", composition));
            }

            element.Add(content().Where(child => child != null));
            return element;
        }

        /// <summary>Writes a screen item from its style, with its frame and look applied.</summary>
        /// <param name="type">The element name.</param>
        /// <param name="placed">The style's attributes with the frame already applied; see <see cref="Place"/>.</param>
        /// <param name="children">Its child objects.</param>
        /// <returns>The element.</returns>
        public XElement Item(string type, IDictionary<string, string> placed, Func<IEnumerable<XElement?>> children)
        {
            return Node(type, ScreenItemsComposition, () => new[] { Attributes(placed), Objects(children()) });
        }

        /// <summary>Applies an item's name, box, tab index and colour overrides to its style's attributes.</summary>
        /// <param name="style">The style's attributes; changed in place and returned.</param>
        /// <param name="frame">The item's name, box and look.</param>
        /// <param name="tabIndex">Its tab index, or <see cref="NotFocusable"/>.</param>
        /// <returns><paramref name="style"/>.</returns>
        public static IDictionary<string, string> Place(IDictionary<string, string> style, HmiItemFrame frame, int tabIndex)
        {
            style["ObjectName"] = frame.Name;
            style["Left"] = Number(frame.Box.Left);
            style["Top"] = Number(frame.Box.Top);
            style["Width"] = Number(frame.Box.Width);
            style["Height"] = Number(frame.Box.Height);
            style["TabIndex"] = Number(tabIndex);

            if (frame.Look.BackColor.Length > 0)
            {
                style["BackColor"] = frame.Look.BackColor;
            }

            if (frame.Look.ForeColor.Length > 0)
            {
                style["ForeColor"] = frame.Look.ForeColor;
            }

            if (frame.Look.HorizontalAlignment.Length > 0)
            {
                style["HorizontalAlignment"] = frame.Look.HorizontalAlignment;
            }

            return style;
        }

        /// <summary>An attribute list, ordered as TIA Portal exports it.</summary>
        /// <param name="values">Attribute names and their values.</param>
        /// <returns>The <c>AttributeList</c> element.</returns>
        public static XElement Attributes(IEnumerable<KeyValuePair<string, string>> values)
        {
            return new XElement(
                "AttributeList",
                values.OrderBy(value => value.Key, StringComparer.Ordinal).Select(value => new XElement(value.Key, value.Value)));
        }

        /// <summary>An object list, or nothing when there are no objects.</summary>
        /// <param name="objects">The child objects; null entries are skipped.</param>
        /// <returns>The <c>ObjectList</c> element, or null.</returns>
        public static XElement? Objects(IEnumerable<XElement?> objects)
        {
            var present = objects.Where(child => child != null).ToList();

            return present.Count == 0 ? null : new XElement("ObjectList", present);
        }

        /// <summary>A link by name to an object outside the document: a tag, a screen.</summary>
        /// <param name="role">The link's role, for example <c>Tag</c> or <c>Value</c>.</param>
        /// <param name="targetName">The name of what it links to.</param>
        /// <returns>The <c>LinkList</c> element.</returns>
        public static XElement Link(string role, string targetName)
        {
            return new XElement("LinkList", new XElement(role, new XAttribute("TargetID", OpenLink), new XElement("Name", targetName)));
        }

        /// <summary>The item's font, in Tahoma, the only family the class projects use.</summary>
        /// <param name="font">Its size and style, already resolved.</param>
        /// <returns>The <c>Font</c> object.</returns>
        public XElement Font(HmiFont font)
        {
            return Node("Hmi.Globalization.MultiLingualFont", "Font", () => new[]
            {
                Objects(new[]
                {
                    Node("Hmi.Globalization.FontItem", "Items", () => new[]
                    {
                        Attributes(new Dictionary<string, string>
                        {
                            ["Culture"] = Culture,
                            ["FontFamily"] = FontFamily,
                            ["FontSize"] = Number(font.Size),
                            ["FontStyle"] = font.Style
                        })
                    })
                })
            });
        }

        /// <summary>A text shown on the item: a button's label, a text field's text.</summary>
        /// <param name="composition">Its role, for example <c>TextOff</c> or <c>Text</c>.</param>
        /// <param name="text">The text.</param>
        /// <returns>The <c>MultilingualText</c> object.</returns>
        public XElement Text(string composition, string text)
        {
            return Node("MultilingualText", composition, () => new[]
            {
                Objects(new[]
                {
                    Node("MultilingualTextItem", "Items", () => new[]
                    {
                        new XElement(
                            "AttributeList",
                            new XElement("Culture", Culture),
                            new XElement("Text", new XElement("body", new XElement("p", text))))
                    })
                })
            });
        }

        /// <summary>
        /// An empty text: an item's tooltip, a text list's comment. TIA Portal writes them on every
        /// such object, and writing them keeps a generated export identical to a drawn one's.
        /// </summary>
        /// <param name="composition">Its role, for example <c>HelpText</c> or <c>Comment</c>.</param>
        /// <returns>The <c>MultilingualText</c> object.</returns>
        public XElement EmptyText(string composition)
        {
            return Node("MultilingualText", composition, () => new[]
            {
                Objects(new[]
                {
                    Node("MultilingualTextItem", "Items", () => new[]
                    {
                        new XElement("AttributeList", new XElement("Culture", Culture), new XElement("Text"))
                    })
                })
            });
        }

        /// <summary>The process value of an I/O field or a bar, connected to a tag.</summary>
        /// <param name="tag">The HMI tag's name.</param>
        /// <returns>The <c>Properties</c> object.</returns>
        public XElement ProcessValue(string tag)
        {
            return Node("Hmi.Screen.Property", "Properties", () => new[]
            {
                Attributes(new Dictionary<string, string> { ["Name"] = "ProcessValue" }),
                Objects(new[]
                {
                    Node("Hmi.Dynamic.TagConnectionDynamic", "Dynamic", () => new[]
                    {
                        Attributes(new Dictionary<string, string> { ["Indirect"] = "false" }),
                        Link("Tag", tag)
                    })
                })
            });
        }

        /// <summary>The tag an animation follows.</summary>
        /// <param name="composition">Its role, for example <c>RangeTag</c> or <c>VisibilityTag</c>.</param>
        /// <param name="tag">The HMI tag's name.</param>
        /// <returns>The trigger object.</returns>
        public XElement TagTrigger(string composition, string tag)
        {
            return Node("Hmi.Dynamic.TagElementTrigger", composition, () => new[] { Link("Tag", tag) });
        }

        /// <summary>Formats a number as TIA Portal writes it, whatever the machine's culture.</summary>
        /// <param name="value">The number.</param>
        /// <returns>Its text.</returns>
        public static string Number(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
