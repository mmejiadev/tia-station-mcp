using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TiaMcpServer.Siemens;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TiaMcpServer.UnifiedTextLists
{
    /// <summary>Writes a WinCC Unified text list as the two YAML files Openness imports, and reads the files it exports.</summary>
    /// <remarks>
    /// Openness V20 gives a Unified text list no <c>Create</c> and no entries; the composition only
    /// exports and imports files (measured 2026-10-25 on TIA Portal V20 and a class project). A list is
    /// two files: <c>&lt;name&gt;.hmi.yml</c>, its entries, each pointing at a text by key, and
    /// <c>&lt;name&gt;.TextLibrary.hmi.yml</c>, the texts per language. The texts live in one library
    /// shared by the whole HMI, which TIA Portal calls <c>MyTextLibrary</c> and renumbers on import, and
    /// exporting any list writes every list. A value is written as <c>FromValue</c> and <c>ToValue</c>:
    /// an entry with only <c>Value</c> is imported with no value, and compiles as "The type of the entry is
    /// invalid". A value of 0 is exported with no value fields at all, so none is read as 0.
    /// </remarks>
    public static class UnifiedTextListYaml
    {
        private const string Version = "#Version: 2.0";
        private const string LineBreak = "\r\n";

        /// <summary>The name of the file holding a list's entries, which <c>Import</c> is given.</summary>
        /// <param name="listName">The list's name.</param>
        /// <returns>The file name.</returns>
        public static string ListFileName(string listName)
        {
            return listName + ".hmi.yml";
        }

        /// <summary>The name of the file holding a list's texts, read beside its list file.</summary>
        /// <param name="listName">The list's name.</param>
        /// <returns>The file name.</returns>
        public static string LibraryFileName(string listName)
        {
            return listName + ".TextLibrary.hmi.yml";
        }

        /// <summary>Writes a list as the two files Openness imports.</summary>
        /// <param name="list">The list.</param>
        /// <param name="language">The culture its texts are in, e.g. <c>en-US</c>: the project's editing language.</param>
        /// <returns>The list file's text and the library file's text.</returns>
        public static (string List, string Library) Write(UnifiedTextList list, string language)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            var library = list.Name + "_Texts";
            var entries = new StringBuilder();
            var texts = new StringBuilder();

            for (var index = 0; index < list.Entries.Count; index++)
            {
                var entry = list.Entries[index];
                entries.Append($"          Text_list_entry_{index}:{LineBreak}")
                    .Append($"            Value: {Number(entry.From)}{LineBreak}")
                    .Append($"            FromValue: {Number(entry.From)}{LineBreak}")
                    .Append($"            ToValue: {Number(entry.To)}{LineBreak}")
                    .Append($"            Text: {library}.Text_{index}{LineBreak}");
                texts.Append($"      Text_{index}:{LineBreak}        Text:{LineBreak}        - {Quoted(entry.Text)}{LineBreak}");
            }

            var listFile = $"{Version}{LineBreak}{LineBreak}TextListContainers:{LineBreak}  DeviceTextList:{LineBreak}    ResourceListType: TextList{LineBreak}"
                + $"    ResourceLists:{LineBreak}      {list.Name}:{LineBreak}        Entries:{LineBreak}{entries}";
            var libraryFile = $"{Version}{LineBreak}{LineBreak}TextLibraries:{LineBreak}  {library}:{LineBreak}    Type: Text{LineBreak}    Languages:{LineBreak}"
                + $"    - {language}{LineBreak}    DefaultLanguage: {language}{LineBreak}    Entries:{LineBreak}{texts}";

            return (listFile, libraryFile);
        }

        /// <summary>Reads every list from the two files an export wrote.</summary>
        /// <param name="listFile">The text of the list file.</param>
        /// <param name="libraryFile">The text of the library file.</param>
        /// <param name="language">The culture to read texts in; the library's default when it has none in it.</param>
        /// <returns>The lists, in the order of the file.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when the files are not in the shape measured on V20.</exception>
        public static IReadOnlyList<UnifiedTextList> Read(string listFile, string libraryFile, string language)
        {
            var texts = ReadTexts(Load(libraryFile), language);
            var containers = Map(Child(Load(listFile), "TextListContainers"), "TextListContainers");

            return containers.Children.Values
                .Select(container => Map(container, "a text list container"))
                .Where(container => Scalar(Optional(container, "ResourceListType")) == "TextList")
                .SelectMany(container => Map(Child(container, "ResourceLists"), "ResourceLists").Children)
                .Select(list => ReadList(Scalar(list.Key)!, list.Value, texts))
                .ToList();
        }

        /// <remarks>A list TIA Portal kept no entry of is exported as its name and nothing else (<c>Ranged:</c>, measured 2026-10-25).</remarks>
        private static UnifiedTextList ReadList(string name, YamlNode list, IReadOnlyDictionary<string, string> texts)
        {
            if (list is YamlScalarNode empty && string.IsNullOrEmpty(empty.Value))
            {
                return new UnifiedTextList(name, new List<UnifiedTextListEntry>());
            }

            var entries = Optional(Map(list, "a text list"), "Entries") is YamlMappingNode given
                ? given.Children.Values.Select(entry => ReadEntry(Map(entry, "a text list entry"), texts, name)).ToList()
                : new List<UnifiedTextListEntry>();

            return new UnifiedTextList(name, entries);
        }

        private static UnifiedTextListEntry ReadEntry(YamlMappingNode entry, IReadOnlyDictionary<string, string> texts, string listName)
        {
            var value = Long(Optional(entry, "Value")) ?? 0;
            var from = Long(Optional(entry, "FromValue")) ?? value;
            var to = Long(Optional(entry, "ToValue")) ?? value;
            var key = Scalar(Optional(entry, "Text")) ?? string.Empty;

            return texts.TryGetValue(key, out var text)
                ? new UnifiedTextListEntry(from, to, text)
                : throw new PortalException(PortalErrorCode.InvalidState, $"The text list '{listName}' points at a text '{key}' its library does not hold");
        }

        /// <remarks>A text is keyed <c>Library.Key</c>, as an entry names it; its sequence holds one text per language, in the library's order.</remarks>
        private static Dictionary<string, string> ReadTexts(YamlMappingNode root, string language)
        {
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var library in Map(Child(root, "TextLibraries"), "TextLibraries").Children)
            {
                var body = Map(library.Value, "a text library");
                var languages = Optional(body, "Languages") is YamlSequenceNode listed ? listed.Children.Select(Scalar).ToList() : new List<string?>();
                var index = languages.IndexOf(language);
                var slot = index >= 0 ? index : Math.Max(0, languages.IndexOf(Scalar(Optional(body, "DefaultLanguage"))));

                var entries = Optional(body, "Entries") is YamlMappingNode mapped ? mapped.Children.ToList() : new List<KeyValuePair<YamlNode, YamlNode>>();

                foreach (var text in entries)
                {
                    texts[$"{Scalar(library.Key)}.{Scalar(text.Key)}"] = Optional(Map(text.Value, "a text"), "Text") is YamlSequenceNode perLanguage && perLanguage.Children.Count > slot
                        ? Scalar(perLanguage.Children[slot]) ?? string.Empty
                        : string.Empty;
                }
            }

            return texts;
        }

        private static YamlMappingNode Load(string text)
        {
            try
            {
                var stream = new YamlStream();
                stream.Load(new StringReader(text ?? string.Empty));

                return stream.Documents.Count > 0 ? Map(stream.Documents[0].RootNode, "the file") : throw Unknown("the file is empty");
            }
            catch (YamlException exception)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"TIA Portal exported a text list file that is not YAML: {exception.Message}", null, exception);
            }
        }

        private static YamlNode Child(YamlMappingNode owner, string key)
        {
            return Optional(owner, key) ?? throw Unknown($"'{key}' is missing");
        }

        private static YamlNode? Optional(YamlMappingNode owner, string key)
        {
            return owner.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;
        }

        private static YamlMappingNode Map(YamlNode node, string what)
        {
            return node as YamlMappingNode ?? throw Unknown($"{what} is not a mapping");
        }

        private static string? Scalar(YamlNode? node)
        {
            return (node as YamlScalarNode)?.Value;
        }

        private static long? Long(YamlNode? node)
        {
            var text = Scalar(node);
            if (text == null)
            {
                return null;
            }

            return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : throw Unknown($"'{text}' is not a whole number");
        }

        private static PortalException Unknown(string detail)
        {
            return new PortalException(PortalErrorCode.InvalidState, $"TIA Portal exported a text list in a shape this tool does not know ({detail}); it was read on TIA Portal V20");
        }

        private static string Number(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <remarks>Single-quoted YAML: nothing is an escape but a doubled quote, so any text but a line break goes through.</remarks>
        private static string Quoted(string text)
        {
            return "'" + text.Replace("'", "''") + "'";
        }
    }
}
