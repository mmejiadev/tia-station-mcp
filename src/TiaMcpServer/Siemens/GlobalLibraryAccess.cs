using Siemens.Engineering;
using Siemens.Engineering.Library;
using System;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>Uses a global library opened read-only for one call, and closes it again unless it was already open.</summary>
    /// <remarks>
    /// Read-only, because nothing this server does writes to a global library: it is the user's file, often
    /// shared with a class, and copying a type out of it was measured to work from a read-only one
    /// (2026-10-30). A library TIA Portal already has open is used as it is and left open, since closing it
    /// would close it for whoever opened it.
    /// </remarks>
    internal static class GlobalLibraryAccess
    {
        /// <summary>Opens a global library read-only, or finds it open, runs <paramref name="use"/> on it, and closes it if opened here.</summary>
        /// <typeparam name="T">What <paramref name="use"/> gives back.</typeparam>
        /// <param name="portal">The TIA Portal instance.</param>
        /// <param name="libraryPath">The library file, e.g. <c>C:\Libraries\Faceplates\Faceplates.al20</c>.</param>
        /// <param name="use">What to do with the library.</param>
        /// <returns>
        /// What <paramref name="use"/> gave back, and a warning when the library could not be closed afterwards:
        /// what <paramref name="use"/> did is done, so a failed close is reported beside it, not instead of it.
        /// </returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when the path is empty or no file is there. When the library cannot be
        /// closed after <paramref name="use"/> failed, the failure keeps its own code and message and says so.
        /// </exception>
        /// <remarks>
        /// When TIA Portal ends the session inside <paramref name="use"/>, nothing is left to close the library with;
        /// the failure says the project must be opened again, and TIA Portal closing ends the library too.
        /// </remarks>
        public static (T Result, string? Warning) Use<T>(TiaPortal portal, string libraryPath, Func<GlobalLibrary, T> use)
        {
            var file = RequireFile(libraryPath);
            var open = portal.GlobalLibraries.FirstOrDefault(library => IsTheFile(library, file));

            if (open != null)
            {
                return (use(open), null);
            }

            var opened = portal.GlobalLibraries.Open(file, OpenMode.ReadOnly);
            T result;

            try
            {
                result = use(opened);
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                var closeFailure = CloseAfter(opened, failure);
                if (closeFailure == null)
                {
                    throw;
                }

                throw closeFailure;
            }

            return (result, TryClose(opened));
        }

        /// <remarks>
        /// Both sides through <see cref="Path.GetFullPath(string)"/>, and the library's folder counts as well as its
        /// file, as which of the two Openness gives as <c>Path</c> for an open library was not measured.
        /// </remarks>
        private static bool IsTheFile(GlobalLibrary library, FileInfo file)
        {
            var path = library.Path?.FullName;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var normalised = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

            return string.Equals(normalised, Path.GetFullPath(file.FullName), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalised, Path.GetFullPath(file.DirectoryName!).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }

        private static string? TryClose(UserGlobalLibrary opened)
        {
            try
            {
                opened.Close();
                return null;
            }
            catch (Exception closeFailure) when (!UnifiedRollback.IsSessionEnded(closeFailure))
            {
                // Not swallowed: given back to be reported beside the result.
                return $"The global library '{opened.Name}' could not be closed afterwards ({closeFailure.Message.Trim()}); close it in TIA Portal.";
            }
        }

        private static FileInfo RequireFile(string libraryPath)
        {
            if (string.IsNullOrWhiteSpace(libraryPath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "globalLibraryPath is required: the global library file, e.g. C:\\Libraries\\Faceplates\\Faceplates.al20");
            }

            var file = new FileInfo(libraryPath.Trim());

            return file.Exists
                ? file
                : throw new PortalException(PortalErrorCode.InvalidParams, $"There is no global library file at '{file.FullName}'; give the .al20 file inside the library's folder");
        }

        /// <returns>Null when the library closed; otherwise the failure to throw, naming both.</returns>
        /// <remarks>A close that fails must not replace the failure that explains what went wrong; compare RemoveStationAfter.</remarks>
        private static PortalException? CloseAfter(UserGlobalLibrary opened, Exception failure)
        {
            try
            {
                opened.Close();
                return null;
            }
            catch (Exception closeFailure) when (!UnifiedRollback.IsSessionEnded(closeFailure))
            {
                return new PortalException(
                    (failure as PortalException)?.Code ?? PortalErrorCode.WriteFailed,
                    $"{failure.Message.Trim()} Closing the global library '{opened.Name}' afterwards failed too ({closeFailure.Message.Trim()}); close it in TIA Portal.",
                    null,
                    new AggregateException(failure, closeFailure));
            }
        }
    }
}
