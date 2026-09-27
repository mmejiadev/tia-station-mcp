using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseExportBlocksAsDocuments : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }

        /// <summary>
        /// The exported blocks that do not compile. Unlike the SimaticML export, these are exported,
        /// not skipped; they are listed so nobody mistakes the document of a broken block for a
        /// working one.
        /// </summary>
        public IEnumerable<ResponseBlockInfo>? Inconsistent { get; set; }
    }
}
