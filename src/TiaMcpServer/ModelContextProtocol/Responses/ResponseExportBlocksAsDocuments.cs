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

        /// <summary>
        /// One line per problem met, as <c>BlockName: reason</c>. A selected block missing from
        /// <see cref="Items"/> is named here with the reason, rather than left for the caller to
        /// notice by counting.
        /// </summary>
        public IEnumerable<string>? Failed { get; set; }
    }
}
