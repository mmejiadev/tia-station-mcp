using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Result of writing SCL into a PLC program.</summary>
    public sealed class ResponseWriteScl : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="generatedBlocks">Names of the blocks the source produced.</param>
        public ResponseWriteScl(IReadOnlyList<string> generatedBlocks)
        {
            GeneratedBlocks = generatedBlocks;
        }

        /// <summary>
        /// Names of the blocks the source produced. Generating a block does not mean it compiles:
        /// call CompileSoftware to find that out.
        /// </summary>
        public IReadOnlyList<string> GeneratedBlocks { get; }
    }
}
