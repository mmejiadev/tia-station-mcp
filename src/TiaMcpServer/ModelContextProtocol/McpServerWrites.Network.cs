using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Subnets, IO systems, addresses and PROFINET names: how the devices are connected.
    ///
    /// Part of McpServerWrites: every tool here changes something and goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "CreateIoSystem"), Description("Create a PROFINET IO system on a CPU so IO devices can be attached to it. The current network layout is recorded to the backup registry first, because this rewires the project; call ListBackups to find that copy.")]
        public static ResponseMessage CreateIoSystem(
            [Description("controllerPath: full path to the CPU that will act as IO controller, e.g. 'PLC_0'")] string controllerPath,
            [Description("ioSystemName: a name for the IO system, e.g. 'Cell_IO'")] string ioSystemName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(controllerPath);
                var backupDirectory = Backups.Allocate("CreateIoSystem", target);
                var request = new Governance.ChangeRequest("CreateIoSystem", target, ioSystemName)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var subnet = Portal.CreateIoSystem(controllerPath, ioSystemName, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"IO system '{ioSystemName}' created on '{controllerPath}', subnet '{subnet}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to create IO system '{ioSystemName}'");
            }
        }

        [McpServerTool(Name = "SetDeviceAddress"), Description("Set the address of one network node, for example a CPU's IP. Take devicePath and nodeName straight from GetNetworkTopology, which prints both for every interface in the project. The current layout is recorded to the backup registry first. The address the node holds afterwards is read back rather than echoed, because TIA normalises some of them.")]
        public static ResponseMessage SetDeviceAddress(
            [Description("devicePath: path of the device item owning the interface, as GetNetworkTopology prints it")] string devicePath,
            [Description("nodeName: the node on that interface, as GetNetworkTopology prints it")] string nodeName,
            [Description("address: the address to set, for example 192.168.0.1 on Ethernet or a station number on PROFIBUS")] string address)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(devicePath);
                var backupDirectory = Backups.Allocate("SetDeviceAddress", target);
                var request = new Governance.ChangeRequest("SetDeviceAddress", target, address)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var applied = Portal.SetNodeAddress(devicePath, nodeName, address, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"'{nodeName}' on '{devicePath}' now answers at '{applied}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["address"] = applied
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to set '{nodeName}' on '{devicePath}' to '{address}'");
            }
        }

        [McpServerTool(Name = "SetProfinetDeviceName"), Description("Set the PROFINET device name of one network node. Take devicePath and nodeName straight from GetNetworkTopology, which prints both plus the name each node holds now. This is not the address: an IO controller resolves the name over DCP at start-up, so a device whose project name differs from the name held by the hardware never joins its IO system, at any address. TIA generates the name from the interface by default and this turns that generation off. A PROFINET name is a DNS label: lowercase letters, digits and hyphens, no underscores and no spaces. The current layout is recorded to the backup registry first, and the stored name is read back rather than echoed.")]
        public static ResponseMessage SetProfinetDeviceName(
            [Description("devicePath: path of the device item owning the interface, as GetNetworkTopology prints it")] string devicePath,
            [Description("nodeName: the node on that interface, as GetNetworkTopology prints it")] string nodeName,
            [Description("deviceName: the PROFINET device name to set, for example 'et200sp-station-1'")] string deviceName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(devicePath);
                var backupDirectory = Backups.Allocate("SetProfinetDeviceName", target);
                var request = new Governance.ChangeRequest("SetProfinetDeviceName", target, deviceName)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var stored = Portal.SetProfinetDeviceName(devicePath, nodeName, deviceName, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"'{nodeName}' on '{devicePath}' is called '{stored}' on PROFINET",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["profinetDeviceName"] = stored
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to name '{nodeName}' on '{devicePath}' as '{deviceName}' on PROFINET");
            }
        }

        [McpServerTool(Name = "CreateSubnet"), Description("Create a subnet and connect one interface to it. Take devicePath and nodeName straight from GetNetworkTopology. The subnet's network type is not a parameter: it comes from the interface, so an Ethernet interface makes an Ethernet subnet. An interface already on a subnet is refused rather than moved. The current layout is recorded to the backup registry first.")]
        public static ResponseMessage CreateSubnet(
            [Description("devicePath: path of the device item owning the interface, as GetNetworkTopology prints it")] string devicePath,
            [Description("nodeName: the node on that interface, as GetNetworkTopology prints it")] string nodeName,
            [Description("subnetName: the name to give the new subnet, for example 'Cell_PN'")] string subnetName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(devicePath);
                var backupDirectory = Backups.Allocate("CreateSubnet", target);
                var request = new Governance.ChangeRequest("CreateSubnet", target, subnetName)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeSubnetAttachment(
                        Portal.CreateSubnet(devicePath, nodeName, subnetName, backupDirectory), devicePath, nodeName),
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to create subnet '{subnetName}' from '{devicePath}'");
            }
        }

        [McpServerTool(Name = "ConnectDeviceToSubnet"), Description("Connect one interface to a subnet that already exists. Take devicePath and nodeName from GetNetworkTopology and subnetName from GetSubnets. Connecting an interface to the subnet it is already on changes nothing and succeeds; moving one that is on a different subnet is refused, because that unwires whatever it talks to now. The current layout is recorded to the backup registry first.")]
        public static ResponseMessage ConnectDeviceToSubnet(
            [Description("devicePath: path of the device item owning the interface, as GetNetworkTopology prints it")] string devicePath,
            [Description("nodeName: the node on that interface, as GetNetworkTopology prints it")] string nodeName,
            [Description("subnetName: the subnet to attach it to, as GetSubnets prints it")] string subnetName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(devicePath);
                var backupDirectory = Backups.Allocate("ConnectDeviceToSubnet", target);
                var request = new Governance.ChangeRequest("ConnectDeviceToSubnet", target, subnetName)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeSubnetAttachment(
                        Portal.ConnectDeviceToSubnet(devicePath, nodeName, subnetName, backupDirectory), devicePath, nodeName),
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to connect '{devicePath}' to subnet '{subnetName}'");
            }
        }

        /// <remarks>
        /// The subnet named here is the one the node sits on afterwards, read back from the project
        /// rather than echoed from the argument — the same reason SetDeviceAddress reads its address
        /// back. A caller that trusted the echo would go on to build an IO system on a subnet whose
        /// name the project does not use.
        /// </remarks>
        private static ResponseMessage DescribeSubnetAttachment(string subnetName, string devicePath, string nodeName)
        {
            return new ResponseMessage
            {
                Message = $"'{nodeName}' on '{devicePath}' is on subnet '{subnetName}'",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["subnet"] = subnetName
                }
            };
        }

        [McpServerTool(Name = "AssignDeviceToIoSystem"), Description("Attach an IO device to an existing PROFINET IO system. The current network layout is recorded to the backup registry first. The CPU that owns the IO system cannot be attached to it: a controller is not one of its own devices.")]
        public static ResponseMessage AssignDeviceToIoSystem(
            [Description("devicePath: full path to the IO device to attach")] string devicePath,
            [Description("ioSystemName: the IO system to attach it to")] string ioSystemName)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(devicePath);
                var backupDirectory = Backups.Allocate("AssignDeviceToIoSystem", target);
                var request = new Governance.ChangeRequest("AssignDeviceToIoSystem", target, ioSystemName)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        Portal.AssignDeviceToIoSystem(devicePath, ioSystemName, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"'{devicePath}' attached to IO system '{ioSystemName}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to attach '{devicePath}' to IO system '{ioSystemName}'");
            }
        }
    }
}
