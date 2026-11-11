using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using System;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Archiving the open project into one <c>.zapNN</c> file: how a project is handed in, moved to
    /// another machine, or kept as a test fixture. <see cref="ProjectArchiveRule"/> names the file.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Archives the open project into a compressed <c>.zapNN</c> file.</summary>
        /// <param name="targetDirectory">The folder the archive is written to; created when missing.</param>
        /// <param name="archiveName">The archive's name, without extension.</param>
        /// <param name="discardRestorableData">Whether to leave out what TIA Portal can rebuild, which makes the file smaller.</param>
        /// <returns>The full path of the archive.</returns>
        /// <exception cref="PortalException">
        /// No project is open, a multiuser session is open, the name is invalid, a file of that name is
        /// already there, or TIA Portal failed to write it.
        /// </exception>
        public string ArchiveProject(string targetDirectory, string archiveName, bool discardRestorableData)
        {
            _logger?.LogInformation("Archiving the project as {ArchiveName} in {TargetDirectory}...", archiveName, targetDirectory);

            try
            {
                var (project, archivePath) = ValidateArchiveRequest(targetDirectory, archiveName);
                var mode = discardRestorableData ? ProjectArchivationMode.DiscardRestorableDataAndCompressed : ProjectArchivationMode.Compressed;

                var scratch = ArchiveThroughShortPath(project, mode, archivePath);
                TryRemoveScratch(scratch);

                return archivePath;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Archive failed: {ex.Message}", null, ex);

                pex.Data["targetDirectory"] = targetDirectory;
                pex.Data["archiveName"] = archiveName;

                _logger?.LogError(pex, "ArchiveProject failed for {ArchiveName} in {TargetDirectory}", archiveName, targetDirectory);
                throw pex;
            }
        }

        /// <remarks>
        /// Measured 2026-11-11 on V21. TIA Portal writes exactly the name it is given and adds no
        /// extension, so the name passed carries one. It refuses a path of 183 characters, as it refuses
        /// a library archive over 143 (see GlobalLibraryAccess): written to a short folder and moved, so
        /// any target works. The short folder is left behind only when the archive failed, to be looked at.
        /// </remarks>
        /// <returns>The short folder the archive was written to, empty once the archive is moved.</returns>
        private static string ArchiveThroughShortPath(Project project, ProjectArchivationMode mode, string archivePath)
        {
            var fileName = Path.GetFileName(archivePath);
            var scratch = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            Directory.CreateDirectory(scratch);
            project.Archive(new DirectoryInfo(scratch), fileName, mode);

            var written = Path.Combine(scratch, fileName);
            if (!File.Exists(written))
            {
                var found = string.Join(", ", Directory.GetFiles(scratch).Select(Path.GetFileName));
                throw new PortalException(PortalErrorCode.ExportFailed, $"TIA Portal reported the archive written, and '{fileName}' is not in '{scratch}' (found: {found})");
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
                File.Move(written, archivePath);
            }
            catch (Exception moveFailure) when (moveFailure is IOException || moveFailure is UnauthorizedAccessException)
            {
                throw new PortalException(PortalErrorCode.ExportFailed, $"The archive was written but could not be moved to '{archivePath}' ({moveFailure.Message.Trim()}); it is kept at '{written}'", null, moveFailure);
            }

            return scratch;
        }

        /// <remarks>
        /// The archive is in place by now, so an empty folder left in %TEMP% is logged rather than reported as a failed
        /// archive: that would ask for a retry, and the retry would be refused for the file just written.
        /// </remarks>
        private void TryRemoveScratch(string scratch)
        {
            try
            {
                Directory.Delete(scratch, recursive: true);
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                _logger?.LogWarning(failure, "The empty folder {Scratch} an archive was written through could not be removed", scratch);
            }
        }

        /// <remarks>A path that cannot be read is a mistake in the call, not an operation failure to retry.</remarks>
        private static string FullDirectoryPath(string targetDirectory)
        {
            try
            {
                return Path.GetFullPath(targetDirectory.Trim());
            }
            catch (Exception failure) when (failure is ArgumentException || failure is NotSupportedException || failure is PathTooLongException)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"targetDirectory '{targetDirectory}' is not a path: {failure.Message}", null, failure);
            }
        }

        private (Project Project, string ArchivePath) ValidateArchiveRequest(string targetDirectory, string archiveName)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "targetDirectory is required");
            }

            var fileName = ProjectArchiveRule.ArchiveFileName(archiveName, Engineering.TiaMajorVersion);

            if (_session != null || CurrentProject is not Project project)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a local project before archiving it; a multiuser session is not archived here");
            }

            var archivePath = Path.Combine(FullDirectoryPath(targetDirectory), fileName);
            if (File.Exists(archivePath))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Archive already exists, refusing to overwrite: {archivePath}");
            }

            return (project, archivePath);
        }
    }
}
