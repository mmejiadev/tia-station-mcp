using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseExportBlocks : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
        public IEnumerable<ResponseBlockInfo>? Inconsistent { get; set; }
    }
}
