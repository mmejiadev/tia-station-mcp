using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseGetProjects : ResponseMessage
    {
        public IEnumerable<ResponseProjectInfo>? Items { get; set; }
    }
}
