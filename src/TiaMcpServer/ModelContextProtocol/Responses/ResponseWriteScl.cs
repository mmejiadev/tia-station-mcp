using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Result of writing SCL into a PLC program.</summary>
    public sealed class ResponseWriteScl : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="generatedBlocks">Names of the blocks the source produced.</param>
        /// <param name="generatedTypes">Names of the PLC data types the source produced.</param>
        public ResponseWriteScl(IReadOnlyList<string> generatedBlocks, IReadOnlyList<string> generatedTypes)
        {
            GeneratedBlocks = generatedBlocks;
            GeneratedTypes = generatedTypes;
        }

        /// <summary>
        /// Names of the blocks the source produced. Generating a block does not mean it compiles:
        /// call CompileSoftware to find that out.
        /// </summary>
        public IReadOnlyList<string> GeneratedBlocks { get; }

        /// <summary>
        /// Names of the PLC data types the source produced. Types are looked up with the type tools
        /// (GetTypeInfo, ExportType), not the block ones.
        /// </summary>
        public IReadOnlyList<string> GeneratedTypes { get; }
    }
}
