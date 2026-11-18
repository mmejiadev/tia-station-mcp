using Microsoft.Extensions.Logging;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Scl;

namespace TiaMcpServer.Siemens
{
    /// <remarks>Checks of SCL against the program it is written into; see <see cref="SclMemoryAccessRules"/>.</remarks>
    public partial class Portal
    {
        /// <summary>Finds PEEK and POKE calls in SCL that TIA Portal compiles and that fail quietly when they run.</summary>
        /// <param name="softwarePath">The PLC software the source is written into, whose optimized data blocks it may name by number.</param>
        /// <param name="sclCode">The SCL source.</param>
        /// <returns>The findings; read only, nothing is changed. The project is read only when the source calls PEEK or POKE.</returns>
        /// <exception cref="PortalException">No project is open, or there is no PLC software at the path.</exception>
        public IReadOnlyList<SclFinding> CheckSclMemoryAccess(string softwarePath, string sclCode)
        {
            if (!SclMemoryAccessRules.HasCalls(sclCode))
            {
                return Array.Empty<SclFinding>();
            }

            try
            {
                // Measured 2026-11-18: an optimized DB read by number returns 0 with ENO FALSE.
                var optimized = FindBlocks(softwarePath).OfType<DataBlock>().Where(block => block.MemoryLayout == MemoryLayout.Optimized).Select(block => block.Number).ToList();

                return SclMemoryAccessRules.Check(sclCode, optimized);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Checking the SCL's memory access failed", null, ex);
                pex.Data["softwarePath"] = softwarePath;

                _logger?.LogError(pex, "CheckSclMemoryAccess failed for {SoftwarePath}", softwarePath);
                throw pex;
            }
        }
    }
}
