using Microsoft.Extensions.Logging;
using System;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Who uses what. This is the question to ask before rewriting a block, and it is the reason
    /// the read exists at all: a model handed a block's source sees what the block does and
    /// nothing about what depends on it, so the one change it cannot judge on its own is exactly
    /// the one it is most likely to make -- renaming a parameter, dropping an output, changing the
    /// order of an interface.
    ///
    /// Blocks only, deliberately. Tags and types offer the same service and are worth the same
    /// tool, but each is its own measurement and this one is about calls.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Reads the cross references of one block.</summary>
        /// <param name="softwarePath">Path to the PLC software in the project.</param>
        /// <param name="blockPath">Full path to the block, <c>Group/Subgroup/Name</c>.</param>
        /// <returns>What uses it and what it uses, one entry per place the two meet.</returns>
        /// <exception cref="PortalException">
        /// No project is open, there is no such block, or TIA Portal offers no cross-reference
        /// service for it.
        /// </exception>
        /// <remarks>
        /// An empty list is an answer, not a failure: a block nobody calls is a real and useful
        /// finding, and it is the one a caller about to delete something wants.
        /// </remarks>
        public CrossReferenceReport GetCrossReferences(string softwarePath, string blockPath)
        {
            _logger?.LogInformation("Reading the cross references of {BlockPath}...", blockPath);

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project before reading cross references");
                }

                var block = FindBlock(softwarePath, blockPath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Block not found: {blockPath}");

                return CrossReferenceReport.Of(CrossReferenceReader.Read(block, blockPath));
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Reading the cross references failed: {ex.Message}", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["blockPath"] = blockPath;

                _logger?.LogError(pex, "GetCrossReferences failed for {SoftwarePath} {BlockPath}", softwarePath, blockPath);
                throw pex;
            }
        }
    }
}
