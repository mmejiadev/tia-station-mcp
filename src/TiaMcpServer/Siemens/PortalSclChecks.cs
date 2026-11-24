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
        /// <param name="softwarePath">The PLC software the source is written into, whose data blocks it may name by number.</param>
        /// <param name="sclCode">The SCL source.</param>
        /// <returns>
        /// The findings; read only, nothing is changed. The project is read only when the source names a data block by a
        /// constant number in a PEEK or POKE call.
        /// </returns>
        /// <exception cref="PortalException">The project has to be read, and no project is open or there is no PLC software at the path.</exception>
        public IReadOnlyList<SclFinding> CheckSclMemoryAccess(string softwarePath, string sclCode)
        {
            if (!SclMemoryAccessRules.HasCalls(sclCode))
            {
                return Array.Empty<SclFinding>();
            }

            var named = SclMemoryAccessRules.NamedDataBlocks(sclCode);
            if (named.Count == 0)
            {
                return SclMemoryAccessRules.CheckText(sclCode);
            }

            try
            {
                return SclMemoryAccessRules.Check(sclCode, ReadDataBlocks(softwarePath, named));
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Checking the SCL's memory access failed", null, ex);
                pex.Data["softwarePath"] = softwarePath;

                _logger?.LogError(pex, "CheckSclMemoryAccess failed for {SoftwarePath}", softwarePath);
                throw pex;
            }
        }

        /// <remarks>
        /// Every DB's number, for the ones that do not exist, and the memory layout of only those the source names: reading
        /// an attribute is a call into TIA Portal, and a program can hold hundreds of DBs. Measured 2026-11-18: an optimized
        /// DB, or one that does not exist, read by number returns 0 with ENO FALSE.
        /// </remarks>
        private ProgramDataBlocks ReadDataBlocks(string softwarePath, IReadOnlyCollection<long> named)
        {
            var numbered = FindBlocks(softwarePath).OfType<DataBlock>().Select(block => (Block: block, Number: block.Number)).ToList();
            var optimized = numbered
                .Where(entry => named.Contains(entry.Number) && entry.Block.MemoryLayout == MemoryLayout.Optimized)
                .Select(entry => entry.Number);

            return ProgramDataBlocks.Of(numbered.Select(entry => entry.Number), optimized);
        }
    }
}
