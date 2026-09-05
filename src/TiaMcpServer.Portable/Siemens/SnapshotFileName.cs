using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Turns the name of a block, type or tag table into a file name a snapshot can hold.
    /// </summary>
    /// <remarks>
    /// TIA names are freer than file names. A block can be called <c>"FC: v2"</c> or
    /// <c>"Motor/Feeder"</c> when quoted, and neither of those can be a file on Windows, so the
    /// characters a file system refuses become underscores.
    ///
    /// **That mapping loses information, and it has to be treated as lossy rather than assumed
    /// safe.** <c>"FC: v2"</c> and <c>"FC* v2"</c> both become <c>FC__v2</c>. Before 2026-09-05 the
    /// exporter deleted whatever was already at that path and wrote over it, so the second block
    /// replaced the first and the report said both had been exported \u2014 a snapshot missing a block
    /// while claiming to be complete, which the response documentation calls precisely not a backup.
    ///
    /// The name is not made unique here on purpose. Appending a disambiguator would change the file
    /// name of every block whose name contains such a character, and a snapshot is a thing that gets
    /// diffed against the last one. Instead <see cref="SnapshotReportBuilder"/> refuses the second
    /// claim on a path and reports it, so the collision is visible and nothing is lost silently.
    /// </remarks>
    public static class SnapshotFileName
    {
        private const char Replacement = '_';

        /// <summary>Makes a name a file system will accept.</summary>
        /// <param name="name">The name as TIA Portal spells it.</param>
        /// <returns>The same name with every character a file name cannot carry replaced.</returns>
        public static string For(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            var sanitised = name;

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                sanitised = sanitised.Replace(invalid, Replacement);
            }

            return sanitised;
        }
    }
}
