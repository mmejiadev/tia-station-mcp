using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// The configured connections between two PLCs. Part of McpServerWrites.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreatePlcConnection"), Description("Make a configured connection from one PLC to another, the kind the Connections table of Devices & networks holds: S7 (for PUT, GET, BSEND and BRCV), TCP or ISO-on-TCP (for TSEND and TRCV). plcPath establishes it. Both PLCs must already be on one Ethernet subnet: CreateSubnet, ConnectDeviceToSubnet and SetDeviceAddress first, GetNetworkTopology to check. TIA Portal names it and gives it its ID (S7_Connection_1, ID 100 at both ends for the first S7 one); the answer gives both, and GetPlcConnections lists them later. A second connection between the same PLCs is allowed, as a program may use several. On an S7-1500, PUT and GET from a remote partner also need ProtectionEnablePutGetCommunication set to true on the partner CPU (SetDeviceParameter; the parameter appears once SetPlcFullAccess has run). An S7-1200 V4.7 does not offer that setting to Openness: it is ticked by hand in the CPU's properties, Protection & Security > Connection mechanisms. Needs TIA Portal V21. The network is recorded to the backup registry first.")]
        public static ResponseMessage CreatePlcConnection(
            [Description("plcPath: the PLC's software that establishes the connection, e.g. 'PLC_1'")] string plcPath,
            [Description("partnerPlcPath: the PLC's software at the other end, e.g. 'PLC_2'")] string partnerPlcPath,
            [Description("connectionType: S7, TCP or ISO-on-TCP")] string connectionType)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            if (!TiaMcpServer.Siemens.Portal.CanUsePlcConnections)
            {
                throw new McpException(
                    "Making connections between PLCs needs TIA Portal V21; on V20 they are made by hand in the Devices & networks view. Nothing was changed.",
                    McpErrorCode.InvalidParams);
            }

            try
            {
                var kind = PlcConnectionKinds.Parse(connectionType);

                // It changes both PLCs' configuration, so a policy has to allow both.
                var target = GuardedWrites.TargetToPropose(ChangeTarget.Program(plcPath), ChangeTarget.Program(partnerPlcPath));
                var backupDirectory = Backups.Allocate("CreatePlcConnection", target);
                var request = new Governance.ChangeRequest("CreatePlcConnection", target, $"{PlcConnectionKinds.Name(kind)} connection from {plcPath} to {partnerPlcPath}")
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeCreatedPlcConnection(Portal.CreatePlcConnection(plcPath, partnerPlcPath, kind, backupDirectory), plcPath, partnerPlcPath),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"connecting '{plcPath}' to '{partnerPlcPath}'");
            }
        }

        private static ResponseMessage DescribeCreatedPlcConnection(PlcConnectionSummary connection, string plcPath, string partnerPlcPath)
        {
            var local = connection.Ends.Local;
            var partner = connection.Ends.Partner;
            var validity = connection.IsValid
                ? string.Empty
                : " TIA Portal reports it NOT valid: compile both stations (CompileHardware) to see why before writing code that uses it.";

            return new ResponseMessage
            {
                Message = $"'{plcPath}' ({local.Address}) is connected to '{partnerPlcPath}' ({partner.Address}) by '{connection.Name}', "
                    + $"ID {local.ConnectionId} on '{plcPath}' and {partner.ConnectionId} on '{partnerPlcPath}'.{validity} Save the project to keep it.",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["connection"] = connection.Name,
                    ["kind"] = connection.Kind,
                    ["valid"] = connection.IsValid,
                    ["localId"] = local.ConnectionId,
                    ["partnerId"] = partner.ConnectionId,
                    ["localAddress"] = local.Address,
                    ["partnerAddress"] = partner.Address
                }
            };
        }
    }
}
