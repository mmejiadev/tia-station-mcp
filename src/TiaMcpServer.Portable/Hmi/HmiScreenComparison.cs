using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TiaMcpServer.Hmi
{
    /// <summary>
    /// Compares two exported classic WinCC screens item by item, attribute by attribute: a screen
    /// drawn by hand and the same screen generated, say.
    /// </summary>
    /// <remarks>
    /// Items are matched by object name, at any depth, so a pilot light the teacher put inside a group
    /// still matches a generated one that is not. For each matched item it compares its attributes and
    /// what hangs below it — font, texts, events, animations, tag links — as a flat list of facts,
    /// ignoring the object numbers, which say nothing about the screen. Objects of one kind are told
    /// apart by their position among their siblings — the first range, the second — so two ranges with
    /// their colours swapped, two functions in another order, or one range too many are differences,
    /// not the same facts in another arrangement.
    ///
    /// Grouping itself is not compared: a group is an item with a name like any other and is reported
    /// as missing when only one screen has it.
    /// </remarks>
    public static class HmiScreenComparison
    {
        private const string ItemPrefix = "Hmi.Screen.";
        private static readonly HashSet<string> NotItems = new HashSet<string>(StringComparer.Ordinal)
        {
            "Hmi.Screen.Screen", "Hmi.Screen.ScreenLayer", "Hmi.Screen.Property"
        };

        /// <summary>Compares two screens.</summary>
        /// <param name="referenceXml">The screen to match, as ExportHmi wrote it.</param>
        /// <param name="candidateXml">The screen being checked against it.</param>
        /// <returns>One line per difference; empty when the two match.</returns>
        /// <exception cref="System.Xml.XmlException">Either text is not XML.</exception>
        public static IReadOnlyList<string> Compare(string referenceXml, string candidateXml)
        {
            var reference = XDocument.Parse(referenceXml);
            var candidate = XDocument.Parse(candidateXml);

            var differences = CompareFacts("Screen", ScreenFacts(reference), ScreenFacts(candidate));
            var referenceItems = Items(reference);
            var candidateItems = Items(candidate);

            foreach (var name in referenceItems.Keys.Union(candidateItems.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
            {
                differences.AddRange(CompareItem(name, referenceItems, candidateItems));
            }

            return differences;
        }

        private static IEnumerable<string> CompareItem(string name, Dictionary<string, XElement> reference, Dictionary<string, XElement> candidate)
        {
            if (!candidate.TryGetValue(name, out var found))
            {
                return new[] { $"'{name}': only in the reference ({reference[name].Name.LocalName})" };
            }

            if (!reference.TryGetValue(name, out var expected))
            {
                return new[] { $"'{name}': only in the candidate ({found.Name.LocalName})" };
            }

            if (expected.Name != found.Name)
            {
                return new[] { $"'{name}': a {expected.Name.LocalName} in the reference, a {found.Name.LocalName} in the candidate" };
            }

            return CompareFacts($"'{name}'", ItemFacts(expected), ItemFacts(found));
        }

        private static List<string> CompareFacts(string what, IReadOnlyCollection<string> reference, IReadOnlyCollection<string> candidate)
        {
            var onlyInReference = reference.Except(candidate, StringComparer.Ordinal).Select(fact => $"{what}: reference has {fact}");
            var onlyInCandidate = candidate.Except(reference, StringComparer.Ordinal).Select(fact => $"{what}: candidate has {fact}");

            return onlyInReference.Concat(onlyInCandidate).ToList();
        }

        private static Dictionary<string, XElement> Items(XDocument document)
        {
            var items = new Dictionary<string, XElement>(StringComparer.Ordinal);

            foreach (var element in document.Descendants().Where(IsItem))
            {
                items[element.Element("AttributeList")?.Element("ObjectName")?.Value ?? string.Empty] = element;
            }

            return items;
        }

        private static bool IsItem(XElement element)
        {
            var type = element.Name.LocalName;

            return type.StartsWith(ItemPrefix, StringComparison.Ordinal)
                && !NotItems.Contains(type)
                && element.Element("AttributeList")?.Element("ObjectName") != null;
        }

        private static List<string> ScreenFacts(XDocument document)
        {
            var screen = document.Descendants("Hmi.Screen.Screen").FirstOrDefault();

            return screen?.Element("AttributeList")?.Elements().Select(attribute => $"{attribute.Name.LocalName}={attribute.Value}").ToList()
                ?? new List<string>();
        }

        /// <remarks>
        /// Every attribute and link below the item, each written with the chain of object types that
        /// leads to it — <c>Hmi.Event.Event/…/FunctionListEntry Name=SetTag</c> — and stopping at nested
        /// items, which are compared on their own.
        /// </remarks>
        private static List<string> ItemFacts(XElement item)
        {
            var facts = new List<string>();
            Collect(item, string.Empty, facts);

            return facts;
        }

        private static void Collect(XElement element, string path, List<string> facts)
        {
            foreach (var attribute in element.Element("AttributeList")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                facts.Add($"{path}{attribute.Name.LocalName}={Flatten(attribute)}");
            }

            foreach (var link in element.Element("LinkList")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                facts.Add($"{path}{link.Name.LocalName} -> {link.Element("Name")?.Value}");
            }

            var children = element.Element("ObjectList")?.Elements().Where(child => !IsItem(child)) ?? Enumerable.Empty<XElement>();

            foreach (var group in children.GroupBy(child => $"{child.Attribute("CompositionName")?.Value}:{child.Name.LocalName}"))
            {
                var position = 0;

                foreach (var child in group)
                {
                    Collect(child, $"{path}{group.Key}#{position++}/", facts);
                }
            }
        }

        /// <remarks>A text attribute holds markup, <c>&lt;body&gt;&lt;p&gt;Marxa&lt;/p&gt;&lt;/body&gt;</c>; its words are what matter.</remarks>
        private static string Flatten(XElement attribute)
        {
            return attribute.HasElements ? string.Concat(attribute.Descendants().Where(node => !node.HasElements).Select(node => node.Value)) : attribute.Value;
        }
    }
}
