using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseDevices : ResponseMessage
    {
        public IEnumerable<ResponseDeviceInfo>? Items { get; set; }
    }
}
