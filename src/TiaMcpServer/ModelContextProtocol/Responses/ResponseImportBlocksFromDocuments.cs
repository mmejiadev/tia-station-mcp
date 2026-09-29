using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseImportBlocksFromDocuments : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }

        /// <summary>
        /// One line per document that was not imported, as <c>DocumentName: reason</c>. A document
        /// whose block is missing from <see cref="Items"/> is named here with the reason, rather than
        /// left for the caller to notice by counting.
        /// </summary>
        public IEnumerable<string>? Failed { get; set; }
    }
}
