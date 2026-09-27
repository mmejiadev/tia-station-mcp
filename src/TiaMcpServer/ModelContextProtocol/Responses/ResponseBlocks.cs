using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseBlocks : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }
}
