using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Vci
{
    /// <summary>
    /// The folder inside a VCI workspace an object is exported to, spelled the way Openness takes it.
    /// </summary>
    /// <remarks>
    /// Measured 2026-11-13 on TIA Portal V21: <c>Workspace.ExportObject</c> refused an absolute folder inside the
    /// workspace ("Relative Directory Path is Invalid") and a bare relative one ("cannot be a relative path"), and took
    /// one relative to the workspace's root written from a leading backslash, <c>\PLC_1\Blocks</c>, once that folder
    /// existed: on a new workspace the same text was refused until the folder was made. The folders mirror
    /// the project: the PLC, then Blocks, PLC data types or PLC tags, then the groups. A name becomes a folder, so a
    /// character a folder cannot hold is replaced.
    /// </remarks>
    public static class VciWorkspacePath
    {
        /// <summary>The folder for a program's blocks.</summary>
        public const string Blocks = "Blocks";

        /// <summary>The folder for a program's PLC data types.</summary>
        public const string Types = "PLC data types";

        /// <summary>The folder for a program's tag tables.</summary>
        public const string Tags = "PLC tags";

        private const char Separator = '\\';
        private const char Replacement = '_';

        /// <summary>The folder as Openness takes it.</summary>
        /// <param name="segments">The folder's names from the workspace's root down, e.g. PLC_1, Blocks, Motors.</param>
        /// <returns>The folder, e.g. <c>\PLC_1\Blocks\Motors</c>.</returns>
        /// <exception cref="ArgumentException">No segment was given.</exception>
        public static string ForOpenness(IEnumerable<string> segments)
        {
            var names = (segments ?? Array.Empty<string>()).Select(SafeName).ToList();

            if (names.Count == 0)
            {
                throw new ArgumentException("A folder needs at least one name", nameof(segments));
            }

            return Separator + string.Join(Separator.ToString(), names);
        }

        /// <summary>A mapped object's folder relative to the workspace's root, with forward slashes.</summary>
        /// <param name="rootFolder">The workspace's root, with or without a closing separator.</param>
        /// <param name="objectFolder">The object's folder.</param>
        /// <returns>The folder below the root, e.g. <c>PLC_1/Blocks</c>; empty at the root; the whole folder when it is not below it.</returns>
        /// <remarks>
        /// Compared segment by segment rather than cut at the root's length: a root given with a closing separator is
        /// longer than an object's folder at the root itself, and a folder TIA Portal reports outside it must not be cut.
        /// </remarks>
        public static string RelativeFolder(string rootFolder, string objectFolder)
        {
            var root = (rootFolder ?? string.Empty).Replace('/', Separator).TrimEnd(Separator);
            var folder = (objectFolder ?? string.Empty).Replace('/', Separator).TrimEnd(Separator);

            if (string.Equals(folder, root, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var below = folder.StartsWith(root + Separator, StringComparison.OrdinalIgnoreCase) ? folder.Substring(root.Length + 1) : folder;

            return below.Replace(Separator, '/');
        }

        /// <summary>A name as a folder or file name can hold it.</summary>
        /// <param name="name">The name in the project.</param>
        /// <returns>The name with every character a file name cannot hold replaced by an underscore.</returns>
        public static string SafeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string((name ?? string.Empty).Trim().Select(character => invalid.Contains(character) ? Replacement : character).ToArray());

            return safe.Length == 0 || safe.All(character => character == '.') ? Replacement.ToString() : safe;
        }
    }
}
