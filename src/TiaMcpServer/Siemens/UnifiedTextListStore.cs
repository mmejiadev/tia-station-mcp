using Siemens.Engineering;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedTextLists;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads and writes WinCC Unified text lists through the YAML files Openness exports and imports.</summary>
    /// <remarks>
    /// See <see cref="UnifiedTextListYaml"/> for the files. What makes a write here more than an import:
    /// TIA Portal V20 says an import succeeded when it kept nothing of a list, and when it dropped every
    /// value (measured 2026-10-25), so every import is exported again and compared with what was asked,
    /// and undone when the two differ.
    /// </remarks>
    internal static class UnifiedTextListStore
    {
        /// <summary>Reads every text list of an HMI.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="language">The culture to read texts in.</param>
        /// <param name="directory">A directory to export into, kept by the caller.</param>
        /// <returns>The lists; empty when the HMI has none.</returns>
        public static IReadOnlyList<UnifiedTextList> ReadAll(UnifiedHmiSoftware hmi, string language, string directory)
        {
            var first = hmi.HmiTextLists.FirstOrDefault();
            if (first == null)
            {
                return new List<UnifiedTextList>();
            }

            // Exporting one list writes every list into the same two files.
            Export(hmi, first.Name, directory);

            return UnifiedTextListYaml.Read(
                File.ReadAllText(Path.Combine(directory, UnifiedTextListYaml.ListFileName(first.Name)), Encoding.UTF8),
                File.ReadAllText(Path.Combine(directory, UnifiedTextListYaml.LibraryFileName(first.Name)), Encoding.UTF8),
                language);
        }

        /// <summary>Exports every list of an HMI, under the name of one of them.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="listName">A list of the HMI, whose name the two files take.</param>
        /// <param name="directory">The directory to write into; created when missing.</param>
        public static void Export(UnifiedHmiSoftware hmi, string listName, string directory)
        {
            Directory.CreateDirectory(directory);
            hmi.HmiTextLists.Export(new DirectoryInfo(directory), listName);
        }

        /// <summary>Writes a list's two files into a directory and imports them, creating or replacing the list.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="list">The list.</param>
        /// <param name="language">The project's editing language.</param>
        /// <param name="directory">The directory to write the files into; created when missing.</param>
        /// <returns>What <c>Import</c> answered, which says nothing about what it kept.</returns>
        public static bool Import(UnifiedHmiSoftware hmi, UnifiedTextList list, string language, string directory)
        {
            Directory.CreateDirectory(directory);
            var (listFile, libraryFile) = UnifiedTextListYaml.Write(list, language);
            var noBom = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(directory, UnifiedTextListYaml.ListFileName(list.Name)), listFile, noBom);
            File.WriteAllText(Path.Combine(directory, UnifiedTextListYaml.LibraryFileName(list.Name)), libraryFile, noBom);

            return hmi.HmiTextLists.Import(new DirectoryInfo(directory), UnifiedTextListYaml.ListFileName(list.Name));
        }

        /// <summary>Puts back the lists an earlier <see cref="Export"/> saved.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="directory">The directory of that export.</param>
        /// <param name="listName">The name its files took.</param>
        /// <returns>What <c>Import</c> answered.</returns>
        public static bool Restore(UnifiedHmiSoftware hmi, string directory, string listName)
        {
            return hmi.HmiTextLists.Import(new DirectoryInfo(directory), UnifiedTextListYaml.ListFileName(listName));
        }

        /// <summary>Whether a list read back is the list that was asked for, entry by entry, whatever the order.</summary>
        /// <param name="asked">The list asked for.</param>
        /// <param name="read">The list read back, or null when it is not there.</param>
        /// <returns>True when every entry has its values and its text.</returns>
        /// <remarks>Compared in the order of the values: the order TIA Portal exports in is not the description's to set.</remarks>
        public static bool IsSame(UnifiedTextList asked, UnifiedTextList? read)
        {
            return read != null
                && read.Entries.Count == asked.Entries.Count
                && Ordered(read).Zip(Ordered(asked), (got, wanted) => got.From == wanted.From && got.To == wanted.To && got.Text == wanted.Text).All(same => same);
        }

        private static IEnumerable<UnifiedTextListEntry> Ordered(UnifiedTextList list)
        {
            return list.Entries.OrderBy(entry => entry.From).ThenBy(entry => entry.To);
        }

        /// <summary>A list in the shape <c>GetUnifiedTextLists</c> prints.</summary>
        /// <param name="list">The list.</param>
        /// <returns><c>{ name, entries: [{ value, text } | { from, to, text }] }</c>.</returns>
        public static JsonObject Describe(UnifiedTextList list)
        {
            var entries = list.Entries.Select(entry => (JsonNode)(entry.IsSingleValue
                ? new JsonObject { ["value"] = entry.From, ["text"] = entry.Text }
                : new JsonObject { ["from"] = entry.From, ["to"] = entry.To, ["text"] = entry.Text }));

            return new JsonObject { ["name"] = list.Name, ["entries"] = new JsonArray(entries.ToArray()) };
        }

        /// <summary>The culture texts are written and read in: the project's editing language.</summary>
        /// <param name="project">The open project.</param>
        /// <returns>A culture name such as <c>en-US</c>.</returns>
        public static string LanguageOf(ProjectBase project)
        {
            return project.LanguageSettings.EditingLanguage.Culture.Name;
        }
    }
}
