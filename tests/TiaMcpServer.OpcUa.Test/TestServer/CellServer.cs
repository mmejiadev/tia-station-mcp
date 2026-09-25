using Opc.Ua;
using Opc.Ua.Server;

namespace TiaMcpServer.OpcUa.Test.TestServer
{
    /// <summary>
    /// A standard OPC UA server with the test cell's address space added to it.
    /// </summary>
    internal sealed class CellServer : StandardServer
    {
        protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            var cell = new CellNodeManager(server, configuration);

            return new MasterNodeManager(server, configuration, null, cell);
        }
    }
}
