using Siemens.Engineering.SW.Blocks;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>Which program items have a text source, and the file extension TIA Portal gives it.</summary>
    /// <remarks>
    /// One table for the two places that write sources with <c>GenerateSource</c>: the snapshot and
    /// the backup taken before a write. Kept apart, they had already come to disagree on which
    /// languages have a source at all.
    /// </remarks>
    internal static class SourceFileExtension
    {
        /// <summary>The extension of a PLC data type's source.</summary>
        public const string Type = ".udt";

        private static readonly Dictionary<ProgrammingLanguage, string> BlockExtensions =
            new Dictionary<ProgrammingLanguage, string>
            {
                [ProgrammingLanguage.SCL] = ".scl",
                [ProgrammingLanguage.DB] = ".db",
                [ProgrammingLanguage.STL] = ".awl"
            };

        /// <summary>The extension of a block's source, if its language has one.</summary>
        /// <param name="language">The block's programming language.</param>
        /// <param name="extension">The extension, with its dot; empty when there is none.</param>
        /// <returns>False for a graphic language (LAD, FBD, GRAPH), which has no text source.</returns>
        public static bool TryFor(ProgrammingLanguage language, out string extension)
        {
            if (BlockExtensions.TryGetValue(language, out var found))
            {
                extension = found;
                return true;
            }

            extension = string.Empty;
            return false;
        }
    }
}
