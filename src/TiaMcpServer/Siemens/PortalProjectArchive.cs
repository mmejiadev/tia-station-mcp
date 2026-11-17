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
        private static string FullDirectoryPath(string targetDirectory, string parameterName = "targetDirectory")
        {
            try
            {
                return Path.GetFullPath(targetDirectory.Trim());
            }
            catch (Exception failure) when (failure is ArgumentException || failure is NotSupportedException || failure is PathTooLongException)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{parameterName} '{targetDirectory}' is not a path: {failure.Message}", null, failure);
            }
        }

        /// <summary>Archives the open project into a backup folder, before a change Openness cannot undo.</summary>
        /// <param name="backupDirectory">The backup folder the archive is written to.</param>
        /// <param name="label">What the archive is named after, e.g. the type about to be deleted.</param>
        /// <exception cref="PortalException">No local project is open, or it has unsaved changes.</exception>
        /// <remarks>
        /// For deleting a library version or type, discarding one in work, or cleaning the library up: a faceplate's
        /// content has no export (measured 2026-11-14), so the archive is the only copy that brings it back. Measured
        /// 2026-11-15 on V21: <c>Archive</c> refuses a project with unsaved changes ("please save the project before
        /// performing 'Archive'"), so such a project is refused before anything is touched, rather than saved on the
        /// caller's behalf.
        /// </remarks>
        private void ArchiveProjectToBackup(string backupDirectory, string label)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: the project is archived into it first");
            }

            var project = RequireLocalProject("the project is archived before this change, and a multiuser session is not archived here");

            if (project.IsModified)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "The project has unsaved changes, and TIA Portal archives only a saved project, which this change needs as its backup: save it first (SaveProject). Nothing was changed.");
            }

            var fileName = ProjectArchiveRule.ArchiveFileName(SnapshotFileName.For(label), Engineering.TiaMajorVersion);

            TryRemoveScratch(ArchiveThroughShortPath(project, ProjectArchivationMode.DiscardRestorableDataAndCompressed, Path.Combine(FullDirectoryPath(backupDirectory, "backupDirectory"), fileName)));
        }

        /// <summary>The open local project, for an operation a multiuser session cannot do here.</summary>
        /// <param name="why">Why a local project is needed, ending the refusal.</param>
        /// <returns>The project.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> when none is open, or a multiuser session is.</exception>
        private Project RequireLocalProject(string why)
        {
            return _session == null && CurrentProject is Project project
                ? project
                : throw new PortalException(PortalErrorCode.InvalidState, $"Open a local project first: {why}");
        }

        private (Project Project, string ArchivePath) ValidateArchiveRequest(string targetDirectory, string archiveName)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "targetDirectory is required");
            }

            var fileName = ProjectArchiveRule.ArchiveFileName(archiveName, Engineering.TiaMajorVersion);

            var project = RequireLocalProject("a multiuser session is not archived here");
            var archivePath = Path.Combine(FullDirectoryPath(targetDirectory), fileName);
            if (File.Exists(archivePath))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Archive already exists, refusing to overwrite: {archivePath}");
            }

            return (project, archivePath);
        }
    }
}
