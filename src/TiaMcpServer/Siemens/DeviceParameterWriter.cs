using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Writes the parameters of one device item as text, for a backup or a snapshot.
    /// </summary>
    /// <remarks>
    /// This is the file that closes a debt written into `ModuleRemover` earlier the same day: until
    /// something could read a module's parameters in a form that outlives the project, a backup
    /// taken before a module was unplugged could not contain them, and the tool had to say so.
    ///
    /// One file per item rather than one for the whole project, because that is the shape of the
    /// need: a parameter write touches one device item, and a file holding every attribute of every
    /// item in a station would be thousands of lines of which two mattered.
    ///
    /// Values are written invariant, as <see cref="ParameterValueFormatter"/> prints them. A backup
    /// that says 0,5 on the machine that took it says five on the next one.
    /// </remarks>
    internal static class DeviceParameterWriter
    {
        private const string ParametersDirectory = "hardware/parameters";
        private const string Separator = " | ";
        private const string NoValue = "<none>";

        /// <summary>Writes one item's parameters under a backup or snapshot root.</summary>
        /// <param name="rootDirectory">The root to write under.</param>
        /// <param name="devicePath">The item the parameters belong to.</param>
        /// <param name="parameters">What was read.</param>
        /// <returns>The path written, relative to the root, using forward slashes.</returns>
        internal static string Write(string rootDirectory, string devicePath, IReadOnlyList<ObjectAttribute> parameters)
        {
            var relativePath = ParametersDirectory + "/" + FileNameFor(devicePath);
            var path = Path.Combine(rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            File.WriteAllText(path, Render(devicePath, parameters), new UTF8Encoding(false));

            return relativePath;
        }

        /// <remarks>
        /// A device path contains separators and a file name cannot, so the separators become
        /// hyphens — both of them are among the invalid file-name characters. It is a file name and
        /// not an identity: the path itself is the first line of the file, where nothing has to be
        /// decoded to read it.
        /// </remarks>
        private static string FileNameFor(string devicePath)
        {
            var safe = devicePath;

            foreach (var forbidden in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(forbidden, '-');
            }

            return safe + ".txt";
        }

        private static string Render(string devicePath, IReadOnlyList<ObjectAttribute> parameters)
        {
            var builder = new StringBuilder();

            builder.AppendLine("# " + devicePath);
            builder.AppendLine("# parameter | value | access");

            foreach (var parameter in parameters.OrderBy(one => one.Name, StringComparer.Ordinal))
            {
                builder.AppendLine(string.Join(
                    Separator,
                    parameter.Name,
                    ParameterValueFormatter.Format(parameter.Value) ?? NoValue,
                    parameter.AccessMode ?? NoValue));
            }

            builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "# {0} parameter(s)", parameters.Count));

            return builder.ToString();
        }
    }
}
