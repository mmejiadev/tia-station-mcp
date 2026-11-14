using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// The integrated connection between a WinCC Unified HMI and a PLC. Part of McpServerWrites.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateHmiConnection"), Description("Make the integrated connection between a WinCC Unified HMI and a PLC, the one the Devices & networks view draws between them: it is what lets the HMI's tags link to the PLC's tags (CreateUnifiedTags with plcTag). Needs TIA Portal V21 (V20's Openness cannot make one; it is done by hand there). Both must already be on one Ethernet subnet: CreateSubnet, ConnectDeviceToSubnet and SetDeviceAddress first, and GetNetworkTopology to check. TIA Portal names the connection itself (HMI_Connection_1, _2...) and the answer gives the name and both addresses. A second connection between the same HMI and PLC is refused. The network is recorded to the backup registry first. The PLC must compile for the HMI to compile: on V21 a new PLC asks for passwords (SetPlcFullAccess).")]
        public static ResponseMessage CreateHmiConnection(
            [Description("hmiPath: the WinCC Unified HMI, e.g. 'PC-System_1/HMI_RT_1'")] string hmiPath,
            [Description("plcPath: the PLC's software, e.g. 'PLC_1'")] string plcPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            if (!TiaMcpServer.Siemens.Portal.CanCreateHmiConnections)
            {
                throw new McpException(
                    "Making an integrated HMI connection needs TIA Portal V21; on V20 it is made by hand in the Devices & networks view. Nothing was changed.",
                    McpErrorCode.InvalidParams);
            }

            try
            {
                // It writes the HMI and takes up a connection of the PLC, and a policy has to allow both.
                var target = GuardedWrites.TargetToPropose(ChangeTarget.Program(hmiPath), ChangeTarget.Program(plcPath));
                var backupDirectory = Backups.Allocate("CreateHmiConnection", target);
                var request = new Governance.ChangeRequest("CreateHmiConnection", target, $"integrated connection from {hmiPath} to {plcPath}")
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeHmiConnection(Portal.CreateHmiConnection(hmiPath, plcPath, backupDirectory), hmiPath, plcPath),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"connecting '{hmiPath}' to '{plcPath}'");
            }
        }

        private static ResponseMessage DescribeHmiConnection(HmiConnectionSummary connection, string hmiPath, string plcPath)
        {
            return new ResponseMessage
            {
                Message = $"'{hmiPath}' is connected to '{plcPath}' by '{connection.Name}' ({connection.LocalAddress} to {connection.PartnerAddress}). "
                    + "Save the project to keep it.",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["connection"] = connection.Name,
                    ["localAddress"] = connection.LocalAddress,
                    ["partnerAddress"] = connection.PartnerAddress
                }
            };
        }
    }
}
