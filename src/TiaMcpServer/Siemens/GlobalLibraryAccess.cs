using Siemens.Engineering;
using Siemens.Engineering.Library;
using System;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>Uses a global library for one call: read-only unless a tool means to change it, and closed again unless it was already open.</summary>
    /// <remarks>
    /// Read-only by default: it is the user's file, often shared with a class, and copying a type out of it
    /// was measured to work from a read-only one (2026-10-30). A library TIA Portal already has open is used
    /// as it is and left open, since closing it would close it for whoever opened it. The one way to write is
    /// <see cref="UseForWriting{T}"/>, which refuses a library already open.
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
            var result = RunOrClose(opened, () => use(opened));

            return (result, TryClose(opened));
        }

        /// <summary>Opens a global library to change it, archives it first, runs <paramref name="use"/>, then saves and closes it.</summary>
        /// <typeparam name="T">What <paramref name="use"/> gives back.</typeparam>
        /// <param name="portal">The TIA Portal instance.</param>
        /// <param name="libraryPath">The library file.</param>
        /// <param name="backupDirectory">Where the library is archived before it changes.</param>
        /// <param name="use">What to do with the library.</param>
        /// <returns>
        /// What <paramref name="use"/> gave back, and a warning when the library could not be closed afterwards:
        /// the file is saved by then, so a failed close is reported beside the result, not instead of it.
        /// </returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidParams"/> when no file is there or no backup directory is given;
        /// <see cref="PortalErrorCode.InvalidState"/> when the library is already open.
        /// </exception>
        /// <remarks>
        /// A library already open is refused: saving it would also save what whoever opened it has not chosen to save.
        /// When <paramref name="use"/> fails the library is closed unsaved, so the file keeps what it had.
        /// </remarks>
        public static (T Result, string? Warning) UseForWriting<T>(TiaPortal portal, string libraryPath, string backupDirectory, Func<UserGlobalLibrary, T> use)
        {
            var file = RequireFile(libraryPath);

            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: the global library is archived before it changes");
            }

            if (portal.GlobalLibraries.Any(library => IsTheFile(library, file)))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The global library '{file.Name}' is open in TIA Portal; close it first, as saving it would save what is pending there too");
            }

            var opened = portal.GlobalLibraries.Open(file, OpenMode.ReadWrite);
            var result = RunOrClose(opened, () =>
            {
                Archive(opened, file, backupDirectory);
                var done = use(opened);
                opened.Save();

                return done;
            });

            return (result, TryClose(opened));
        }

        /// <summary>Closes a library, reporting rather than throwing when TIA Portal refuses.</summary>
        /// <param name="opened">The library.</param>
        /// <returns>Null when it closed; otherwise the warning to give beside what was already done.</returns>
        public static string? TryClose(UserGlobalLibrary opened)
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

        private static T RunOrClose<T>(UserGlobalLibrary opened, Func<T> run)
        {
            try
            {
                return run();
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
        }

        /// <remarks>
        /// Archived to a short folder and moved: TIA Portal refuses an archive path over 143 characters (measured
        /// 2026-11-02), and a backup folder under the registry's default root is often longer than that on its own.
        /// A full path, as Openness refuses a relative one (see CreateGlobalLibrary). A file already at the backup
        /// fails the move, before anything is written to the library.
        /// </remarks>
        private static void Archive(UserGlobalLibrary opened, FileInfo file, string backupDirectory)
        {
            var directory = Path.GetFullPath(backupDirectory);
            var scratch = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(scratch);
            opened.Archive(new DirectoryInfo(scratch), Path.GetFileNameWithoutExtension(file.Name), LibraryArchivationMode.Compressed);

            foreach (var archive in Directory.GetFiles(scratch))
            {
                File.Move(archive, Path.Combine(directory, Path.GetFileName(archive)));
            }

            Directory.Delete(scratch, recursive: true);
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
