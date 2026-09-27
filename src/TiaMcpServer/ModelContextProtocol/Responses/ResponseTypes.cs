using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseTypes : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
    }
}
