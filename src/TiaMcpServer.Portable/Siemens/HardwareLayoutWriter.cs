using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Writes what a project is built from — every module, its slot and its order number — as text
    /// built for diffing.
    /// </summary>
    /// <remarks>
    /// The same argument as <see cref="NetworkTopologyWriter"/>, one layer down. Openness V20 has
    /// no hardware export, so what a station is made of is reconstructed from what can be read.
    /// Until there was a tool that plugs modules this mattered little; now that there is one, a
    /// snapshot without it would record a program running on a rack nobody wrote down, and the
    /// backup taken before plugging would be a record of something else entirely.
    ///
    /// The rows are sorted, for the reason the network table gives: a file whose line order follows
    /// the order TIA happened to enumerate devices produces phantom diffs, and phantom diffs train
    /// everyone to ignore real ones.
    /// </remarks>
    internal static class HardwareLayoutWriter
    {
        private const string FileName = "hardware/modules.txt";
        private const string Separator = " | ";
        private const string BuiltIn = "built-in";
        private const string Plugged = "plugged";

        /// <summary>Writes the module layout under a snapshot root.</summary>
        /// <param name="rootDirectory">The snapshot root.</param>
        /// <param name="modules">The modules to record.</param>
        /// <returns>The path written, relative to the root, using forward slashes.</returns>
        internal static string Write(string rootDirectory, IReadOnlyList<ModuleInfo> modules)
        {
            var path = Path.Combine(rootDirectory, FileName.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            File.WriteAllText(path, Render(modules), new UTF8Encoding(false));

            return FileName;
        }

        private static string Render(IReadOnlyList<ModuleInfo> modules)
        {
            var builder = new StringBuilder();

            builder.AppendLine("# module | slot | type | how");

            // Grouped by station and then by slot, which is the order somebody reads a rack in.
            // Sorting by the module's own path instead would interleave DI_1, DI_10 and DI_2, and
            // a module moved to another slot would not move in the file at all.
            var ordered = modules
                .OrderBy(module => ProjectPath.Parse(module.DevicePath).Parent, System.StringComparer.Ordinal)
                .ThenBy(module => module.PositionNumber)
                .ThenBy(module => module.DevicePath, System.StringComparer.Ordinal);

            foreach (var module in ordered)
            {
                builder.AppendLine(string.Join(
                    Separator,
                    module.DevicePath,
                    module.PositionNumber.ToString(CultureInfo.InvariantCulture),
                    module.TypeIdentifier,
                    module.IsBuiltIn ? BuiltIn : Plugged));
            }

            builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "# {0} module(s)", modules.Count));

            return builder.ToString();
        }
    }
}
