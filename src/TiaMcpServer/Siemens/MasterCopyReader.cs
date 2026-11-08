using Siemens.Engineering.Library.MasterCopies;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads the master copies of a project or global library, with their folders and what each one holds.</summary>
    /// <remarks>
    /// A master copy holds one or more items, each described by its name and its Openness type
    /// (<c>MasterCopyContentDescription</c>); the type is what tells a block from a tag table, so no caller says it.
    /// </remarks>
    internal static class MasterCopyReader
    {
        private static readonly (Type Openness, MasterCopySourceKind Kind)[] KindsByType = new[]
        {
            (typeof(PlcBlock), MasterCopySourceKind.Block),
            (typeof(PlcTagTable), MasterCopySourceKind.TagTable),
            (typeof(PlcType), MasterCopySourceKind.PlcType)
        };

        /// <summary>Every folder and master copy of a library.</summary>
        /// <param name="root">The library's master copy folder.</param>
        /// <returns><c>{ folders: [path], masterCopies: [...] }</c>, each master copy as <see cref="Describe"/> gives it.</returns>
        /// <remarks>The folder tree is walked once: every read here crosses into TIA Portal while the Openness gate is held.</remarks>
        public static JsonObject Read(MasterCopyFolder root)
        {
            var folders = Folders(root).ToList();
            var copies = root.MasterCopies.Select(copy => Describe(string.Empty, copy))
                .Concat(folders.SelectMany(entry => entry.Folder.MasterCopies.Select(copy => Describe(entry.Path, copy))));

            return new JsonObject
            {
                ["folders"] = new JsonArray(folders.Select(entry => (JsonNode)entry.Path).ToArray()),
                ["masterCopies"] = new JsonArray(copies.Select(copy => (JsonNode)copy).ToArray())
            };
        }

        /// <summary>Describes one master copy.</summary>
        /// <param name="folder">Its folder path, empty at the top.</param>
        /// <param name="copy">The master copy.</param>
        /// <returns><c>{ name, path, folder, author, created, contents: [ { name, type, kind } ] }</c>; <c>kind</c> is null for an item these tools do not create.</returns>
        public static JsonObject Describe(string folder, MasterCopy copy)
        {
            return new JsonObject
            {
                ["name"] = copy.Name,
                ["path"] = string.IsNullOrEmpty(folder) ? copy.Name : $"{folder}/{copy.Name}",
                ["folder"] = folder,
                ["author"] = copy.Author,
                ["created"] = copy.CreationDate.ToString("o", CultureInfo.InvariantCulture),
                ["contents"] = new JsonArray(copy.ContentDescriptions.Select(content => (JsonNode)DescribeContent(content)).ToArray())
            };
        }

        /// <summary>What a master copy holds, read once.</summary>
        /// <param name="copy">The master copy.</param>
        /// <returns>Each item's name, type name and kind, null for a kind these tools do not create.</returns>
        public static IReadOnlyList<(string Name, string TypeName, MasterCopySourceKind? Kind)> Contents(MasterCopy copy)
        {
            return copy.ContentDescriptions.Select(content => (content.ContentName, content.ContentType?.Name ?? "item of no type", KindOf(content.ContentType))).ToList();
        }

        /// <summary>A folder by its names, outermost first, compared case aside.</summary>
        /// <param name="root">The library's master copy folder.</param>
        /// <param name="segments">The names; none for the top folder.</param>
        /// <returns>The folder and its path as TIA Portal spells it, or null when one of the names is missing.</returns>
        /// <remarks>The path is the folders' own names, not the caller's spelling, so it matches what <see cref="Read"/> lists.</remarks>
        public static (MasterCopyFolder Folder, IReadOnlyList<string> Names)? FindFolder(MasterCopyFolder root, IReadOnlyList<string> segments)
        {
            MasterCopyFolder folder = root;
            var names = new List<string>();

            foreach (var name in segments)
            {
                var nested = folder.Folders.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
                if (nested == null)
                {
                    return null;
                }

                names.Add(nested.Name);
                folder = nested;
            }

            return (folder, names);
        }

        /// <summary>A master copy in a folder by its name, compared case aside.</summary>
        /// <param name="folder">The folder.</param>
        /// <param name="name">The name.</param>
        /// <returns>The master copy, or null.</returns>
        public static MasterCopy? Find(MasterCopyFolder folder, string name)
        {
            return folder.MasterCopies.FirstOrDefault(copy => string.Equals(copy.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Every user folder under a master copy folder, by path.</summary>
        /// <param name="root">The folder.</param>
        /// <returns>Each folder with its path, e.g. <c>Motors/Pumps</c>; deeper ones after the folder holding them.</returns>
        public static IEnumerable<(string Path, MasterCopyUserFolder Folder)> Folders(MasterCopyFolder root)
        {
            return WalkFolders(root, string.Empty);
        }

        private static IEnumerable<(string Path, MasterCopyUserFolder Folder)> WalkFolders(MasterCopyFolder parent, string parentPath)
        {
            foreach (var folder in parent.Folders)
            {
                var path = string.IsNullOrEmpty(parentPath) ? folder.Name : $"{parentPath}/{folder.Name}";
                yield return (path, folder);

                foreach (var nested in WalkFolders(folder, path))
                {
                    yield return nested;
                }
            }
        }

        private static JsonObject DescribeContent(MasterCopyContentDescription content)
        {
            var kind = KindOf(content.ContentType);

            return new JsonObject
            {
                ["name"] = content.ContentName,
                ["type"] = content.ContentType?.Name,
                ["kind"] = kind == null ? null : MasterCopyRules.Name(kind.Value)
            };
        }

        private static MasterCopySourceKind? KindOf(Type? contentType)
        {
            if (contentType == null)
            {
                return null;
            }

            var match = KindsByType.FirstOrDefault(entry => entry.Openness.IsAssignableFrom(contentType));

            return match.Openness == null ? null : match.Kind;
        }
    }
}
