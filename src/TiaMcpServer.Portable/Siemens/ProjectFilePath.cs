using System;

namespace TiaMcpServer.Siemens
{
    /// <summary>Compares the file paths of TIA Portal projects.</summary>
    /// <remarks>
    /// A project used to be recognised by its name, and two projects called <c>Tolva</c> in two
    /// folders are not the same project: the server would have taken the one already open for the
    /// one asked for, and written into it. A path is compared the way Windows reads it, without
    /// regard to case or to which slash was typed. Both sides are expected to be full paths, as
    /// TIA Portal reports them; the caller makes the requested one full before asking.
    /// </remarks>
    public static class ProjectFilePath
    {
        /// <summary>Whether two paths name the same project file.</summary>
        /// <param name="first">One path.</param>
        /// <param name="second">The other path.</param>
        /// <returns>True when both name the same file; false when either is empty.</returns>
        public static bool AreSame(string first, string second)
        {
            var left = Normalise(first);
            var right = Normalise(second);

            if (left.Length == 0 || right.Length == 0)
            {
                return false;
            }

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalise(string path)
        {
            return (path ?? string.Empty).Trim().Replace('/', '\\').TrimEnd('\\');
        }
    }
}
