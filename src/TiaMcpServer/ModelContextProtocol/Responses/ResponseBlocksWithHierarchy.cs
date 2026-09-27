using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseBlocksWithHierarchy : ResponseMessage
    {
        public BlockGroupDescription? Root { get; set; }
    }
}
