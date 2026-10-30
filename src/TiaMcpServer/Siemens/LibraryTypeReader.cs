using Siemens.Engineering.Library.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads the types of a project or global library, with their versions.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: a WinCC Unified faceplate type is a plain <c>LibraryType</c>,
    /// with no export format and nothing of its content reachable, and so is any other type Openness has no
    /// class for; a PLC data type is a <c>PlcTypeLibraryType</c>, a block a <c>CodeBlockLibraryType</c>. So
    /// a faceplate cannot be told apart by its class; every released version of a plain type is given the
    /// reference a faceplate container would name it by, and placing it is what tells.
    /// </remarks>
    internal static class LibraryTypeReader
    {
        private const string PlainTypeClass = nameof(LibraryType);

        /// <summary>Every type in a library's type folder and its subfolders.</summary>
        /// <param name="root">The library's type folder.</param>
        /// <returns>Each type with its folder path, e.g. <c>Motors/</c>, empty at the top.</returns>
        public static IEnumerable<(string Folder, LibraryType Type)> All(LibraryTypeFolder root)
        {
            return Walk(root.Types, root.Folders, string.Empty);
        }

        /// <summary>Every type in a library by name, read in one walk.</summary>
        /// <param name="root">The library's type folder.</param>
        /// <returns>The types, looked up case aside: TIA Portal keeps one type per name whatever its case.</returns>
        public static Dictionary<string, (string Folder, LibraryType Type)> ByName(LibraryTypeFolder root)
        {
            var types = new Dictionary<string, (string Folder, LibraryType Type)>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in All(root))
            {
                types[entry.Type.Name] = entry;
            }

            return types;
        }

        /// <summary>Every user folder under a library's type folder, by path.</summary>
        /// <param name="root">The library's type folder.</param>
        /// <returns>Each folder with its path, e.g. <c>Motors/Pumps/</c>; deeper ones after the folder holding them.</returns>
        public static IEnumerable<(string Path, LibraryTypeUserFolder Folder)> Folders(LibraryTypeFolder root)
        {
            return WalkFolders(root.Folders, string.Empty);
        }

        /// <summary>A type and every type its versions depend on, and theirs, transitively.</summary>
        /// <param name="type">The type.</param>
        /// <returns>The names, the type's own first.</returns>
        public static IReadOnlyList<string> WithDependencies(LibraryType type)
        {
            var names = new List<string>();
            var pending = new Queue<LibraryType>(new[] { type });

            while (pending.Count > 0)
            {
                var next = pending.Dequeue();
                if (names.Contains(next.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                names.Add(next.Name);
                foreach (var dependency in next.Versions.SelectMany(version => version.Dependencies))
                {
                    pending.Enqueue(dependency.TypeObject);
                }
            }

            return names;
        }

        /// <summary>Describes every type in a library.</summary>
        /// <param name="root">The library's type folder.</param>
        /// <returns>One object per type.</returns>
        public static JsonArray Read(LibraryTypeFolder root)
        {
            return new JsonArray(All(root).Select(entry => (JsonNode)Describe(entry.Folder, entry.Type)).ToArray());
        }

        /// <summary>Describes one type.</summary>
        /// <param name="folder">Its folder path, empty at the top.</param>
        /// <param name="type">The type.</param>
        /// <returns><c>{ name, folder, kind, versions: [...] }</c>.</returns>
        public static JsonObject Describe(string folder, LibraryType type)
        {
            var kind = type.GetType().Name;
            var isPlain = kind == PlainTypeClass;

            return new JsonObject
            {
                ["name"] = type.Name,
                ["folder"] = folder,
                ["kind"] = kind,
                ["versions"] = new JsonArray(type.Versions.Select(version => (JsonNode)DescribeVersion(type.Name, version, isPlain)).ToArray())
            };
        }

        /// <summary>A type's versions as numbers and states.</summary>
        /// <param name="type">The type.</param>
        /// <returns>One entry per version.</returns>
        public static IReadOnlyList<LibraryVersionInfo> Versions(LibraryType type)
        {
            return type.Versions.Select(version => new LibraryVersionInfo(version.VersionNumber, IsReleased(version))).ToList();
        }

        /// <summary>Whether a version is released (committed) or in work.</summary>
        /// <param name="version">The version.</param>
        /// <returns>True when committed, false when in work.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> for any other state: a later TIA Portal's new state is refused, never read as one of these.</exception>
        public static bool IsReleased(LibraryTypeVersion version)
        {
            return version.State switch
            {
                LibraryTypeVersionState.Committed => true,
                LibraryTypeVersionState.InWork => false,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Version {LibraryVersionNumber.Format(version.VersionNumber)} of '{version.TypeObject.Name}' is in a state this server does not know: {version.State}")
            };
        }

        private static JsonObject DescribeVersion(string typeName, LibraryTypeVersion version, bool isPlain)
        {
            var isReleased = IsReleased(version);
            var described = new JsonObject
            {
                ["version"] = LibraryVersionNumber.Format(version.VersionNumber),
                ["state"] = isReleased ? "released" : "inWork",
                ["isDefault"] = version.IsDefault,
                ["author"] = version.Author,
                ["dependencies"] = new JsonArray(version.Dependencies.Select(dependency => (JsonNode)$"{dependency.TypeObject.Name} {LibraryVersionNumber.Format(dependency.VersionNumber)}").ToArray())
            };

            if (isPlain && isReleased)
            {
                described["faceplateReference"] = FaceplateTypeReference.Format(typeName, version.VersionNumber);
            }

            return described;
        }

        private static IEnumerable<(string Folder, LibraryType Type)> Walk(LibraryTypeComposition types, LibraryTypeUserFolderComposition folders, string path)
        {
            foreach (var type in types)
            {
                yield return (path, type);
            }

            foreach (var folder in folders)
            {
                foreach (var nested in Walk(folder.Types, folder.Folders, $"{path}{folder.Name}/"))
                {
                    yield return nested;
                }
            }
        }

        private static IEnumerable<(string Path, LibraryTypeUserFolder Folder)> WalkFolders(LibraryTypeUserFolderComposition folders, string path)
        {
            foreach (var folder in folders)
            {
                var folderPath = $"{path}{folder.Name}/";
                yield return (folderPath, folder);

                foreach (var nested in WalkFolders(folder.Folders, folderPath))
                {
                    yield return nested;
                }
            }
        }
    }
}
