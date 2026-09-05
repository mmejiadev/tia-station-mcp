using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The project as text: SCL in, and a full source snapshot out.
    ///
    /// These two are the point of the whole repository -- versioning a TIA project in Git means
    /// getting its code out as text and back in again. WriteScl is also the one write that runs
    /// on every harness iteration, so it takes a backup before it overwrites anything.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>
        /// Exports a PLC program to plain text under <paramref name="targetDirectory"/>, laid out
        /// for version control. See <see cref="SourceSnapshotExporter"/> for what text means here
        /// and why some blocks cannot be included.
        /// </summary>
        /// <param name="softwarePath">Full path to the PLC software, for example <c>Group1/PLC_1</c>.</param>
        /// <param name="targetDirectory">Root of the snapshot.</param>
        /// <param name="cancellationToken">Cancels between items.</param>
        /// <returns>What was written and what was left out, with reasons.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the software path does not resolve, or the arguments are invalid.
        /// </exception>
        public SnapshotResult ExportSourceSnapshot(string softwarePath, string targetDirectory, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Exporting source snapshot of {SoftwarePath} to {TargetDirectory}...", softwarePath, targetDirectory);

            try
            {
                if (string.IsNullOrWhiteSpace(softwarePath))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "softwarePath is required");
                }

                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project before exporting a snapshot");
                }

                var software = FindPlcSoftware(softwarePath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"PLC software not found: {softwarePath}");

                var program = new SourceSnapshotExporter(software, _logger).ExportSnapshot(targetDirectory, cancellationToken);

                return WithNetworkTopology(program, targetDirectory);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Snapshot export failed: {ex.Message}", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["targetDirectory"] = targetDirectory;

                _logger?.LogError(pex, "ExportSourceSnapshot failed for {SoftwarePath} -> {TargetDirectory}", softwarePath, targetDirectory);
                throw pex;
            }
        }

        /// <summary>
        /// Writes SCL into a PLC program, generating the blocks it declares.
        /// </summary>
        /// <param name="softwarePath">Full path to the PLC software, for example <c>Group1/PLC_1</c>.</param>
        /// <param name="sclCode">The SCL source. May declare more than one block.</param>
        /// <param name="backupDirectory">
        /// Where the current blocks are exported before anything is written. Required: this
        /// operation overwrites blocks that already carry the same names, and the repository rule
        /// is that every write is preceded by an export of the previous state.
        /// </param>
        /// <returns>The names of the blocks that were generated.</returns>
        /// <exception cref="PortalException">
        /// The arguments are invalid, no project is open, the software path does not resolve, the
        /// backup could not be taken, or the SCL produced no blocks.
        /// </exception>
        public IReadOnlyList<string> WriteScl(string softwarePath, string sclCode, string backupDirectory)
        {
            _logger?.LogInformation("Writing SCL into {SoftwarePath}...", softwarePath);

            try
            {
                if (string.IsNullOrWhiteSpace(backupDirectory))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: SCL generation overwrites blocks");
                }

                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project before writing SCL");
                }

                var software = FindPlcSoftware(softwarePath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"PLC software not found: {softwarePath}");

                // Deliberately the full XML export rather than the text snapshot: a snapshot cannot
                // represent LAD, and a backup that silently omits half the program is not a backup.
                ExportBlocks(softwarePath, backupDirectory, string.Empty, preservePath: true);

                return new SclBlockGenerator(software, _logger).Generate(sclCode);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"Writing SCL failed: {ex.Message}", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["backupDirectory"] = backupDirectory;

                _logger?.LogError(pex, "WriteScl failed for {SoftwarePath}", softwarePath);
                throw pex;
            }
        }
    }
}
