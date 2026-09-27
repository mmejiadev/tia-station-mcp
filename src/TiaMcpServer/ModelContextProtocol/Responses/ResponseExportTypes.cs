using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseExportTypes : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
        public IEnumerable<ResponseTypeInfo>? Inconsistent { get; set; }
    }
}
