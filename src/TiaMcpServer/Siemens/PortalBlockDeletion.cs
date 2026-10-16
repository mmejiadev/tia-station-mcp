using Microsoft.Extensions.Logging;
using System;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Deleting a block from a PLC program.
    ///
    /// Apart from <c>PortalBlocks.cs</c> because it is the one write there that removes
    /// something instead of replacing it, and it takes stricter rules than the reads beside it.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>
        /// Deletes one block from a PLC program, after exporting the program's blocks and types to
        /// <paramref name="backupDirectory"/>.
        /// </summary>
        /// <param name="softwarePath">Full path to the PLC software, for example <c>PLC_0</c>.</param>
        /// <param name="blockPath">Full path of the block inside the program, for example <c>Group1/FC_Motor</c>.</param>
        /// <param name="backupDirectory">The directory the backup registry allocated for this change.</param>
        /// <returns>The path of the block that was deleted, spelled as the project spells it.</returns>
        /// <remarks>
        /// Blocks that call the deleted one are left as they are: the program stops compiling until
        /// they are fixed, which is what TIA Portal itself does when a block is deleted by hand.
        /// </remarks>
        /// <exception cref="PortalException">
        /// The path is a pattern or names no block, TIA Portal is online with the CPU, or the backup
        /// was incomplete so nothing was deleted.
        /// </exception>
        public string DeleteBlock(string softwarePath, string blockPath, string backupDirectory)
        {
            _logger?.LogInformation("Deleting block {BlockPath} from {SoftwarePath}...", blockPath, softwarePath);

            try
            {
                RequireExactBlockName(blockPath);

                var software = RequireOfflineSoftware(softwarePath);
                var block = FindBlock(softwarePath, blockPath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Block not found: '{blockPath}' in {softwarePath}");

                var deletedPath = GetBlockPath(block);

                ProgramBackup.Save(software, backupDirectory, _logger);
                block.Delete();

                return deletedPath;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"DeleteBlock failed: {ex.Message}", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["blockPath"] = blockPath;
                pex.Data["backupDirectory"] = backupDirectory;

                _logger?.LogError(pex, "DeleteBlock failed for {SoftwarePath} {BlockPath}", softwarePath, blockPath);
                throw pex;
            }
        }

        /// <remarks>
        /// <see cref="FindBlock"/> takes a name pattern and returns the first block that matches,
        /// which is right for reading and wrong for deleting: <c>FC_.*</c> would remove whichever FC
        /// happened to come first. A delete names exactly one block, so a pattern is refused.
        /// </remarks>
        private void RequireExactBlockName(string blockPath)
        {
            if (string.IsNullOrWhiteSpace(blockPath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "blockPath is required");
            }

            if (ProjectPath.Parse(blockPath).Name.IndexOfAny(_regexChars) >= 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{blockPath}' is a name pattern; a delete takes the exact name of one block");
            }
        }
    }
}
