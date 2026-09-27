using System.Collections.Generic;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseAttributes : ResponseMessage
    {
        public IEnumerable<ObjectAttribute>? Attributes { get; set; }
    }
}
