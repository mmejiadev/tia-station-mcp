using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// The rack: plugging, unplugging, moving and copying modules, and their addresses and parameters.
    ///
    /// Part of McpServerWrites: every tool here changes something and goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "SetModuleAddress"), Description("Move one address range of a module to another start byte, so the program can address it at a known place. Take the module path and the range from GetIoAddresses. Only the start moves: the length belongs to the module. An address overlapping another module's is refused, and setting the one it already has changes nothing. The module layout, addresses included, is recorded to the backup registry first.")]
        public static ResponseMessage SetModuleAddress(
            [Description("modulePath: the module, as GetIoAddresses names it")] string modulePath,
            [Description("ioType: which range to move, 'Input' or 'Output'")] string ioType,
            [Description("startAddress: the byte the range should start at, e.g. 0 for %I0.0")] int startAddress)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(modulePath);
                var backupDirectory = Backups.Allocate("SetModuleAddress", target);
                var request = new Governance.ChangeRequest("SetModuleAddress", target, $"{ioType} at {startAddress}")
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var moved = Portal.SetModuleAddress(modulePath, ioType, startAddress, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"'{modulePath}' {moved.IoType} occupies {moved.Span}",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["startAddress"] = moved.StartAddress
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to move the {ioType} range of '{modulePath}' to {startAddress}");
            }
        }

        [McpServerTool(Name = "SetDeviceParameter"), Description("Set one parameter of a device item: cycle time, start-up behaviour, protection level, whatever GetDeviceParameters lists. Write the value as text - it is converted to the type the parameter already holds, and an enumeration is refused with the words it accepts. A read-only parameter is refused before TIA is asked. The item's parameters are recorded to the backup registry first. Compile the hardware afterwards: a parameter change that is not compiled is not downloaded.")]
        public static ResponseMessage SetDeviceParameter(
            [Description("deviceItemPath: the device item, e.g. 'PLC_0'")] string deviceItemPath,
            [Description("parameterName: the parameter, spelled as GetDeviceParameters prints it")] string parameterName,
            [Description("value: the new value as text, e.g. 'true', '150', or a word from the list the refusal prints")] string value)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(deviceItemPath);
                var backupDirectory = Backups.Allocate("SetDeviceParameter", target);
                var request = new Governance.ChangeRequest("SetDeviceParameter", target, $"{parameterName}={value}")
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var applied = Portal.SetDeviceParameter(deviceItemPath, parameterName, value, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"'{applied.Name}' of '{deviceItemPath}' is {TiaMcpServer.Siemens.ParameterValueFormatter.Format(applied.Value)} now",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["value"] = TiaMcpServer.Siemens.ParameterValueFormatter.Format(applied.Value)
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to set '{parameterName}' of '{deviceItemPath}'");
            }
        }

        [McpServerTool(Name = "UnplugModule"), Description("Remove a module from its rack. This destroys it: the backup records the slot, the order number and the addresses it occupied - enough for PlugModule and SetModuleAddress to put an identical card back - and its parameters, which SetDeviceParameter can set again on the new card. A setting Openness does not expose as a writable parameter is lost, and the recorded parameters show which those were. The slot, order number and addresses come back in the answer. A built-in item cannot be unplugged, and the CPU is refused: removing it would take the program with it.")]
        public static ResponseMessage UnplugModule(
            [Description("modulePath: the module to remove, as GetPlugLocations names it")] string modulePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(modulePath);
                var backupDirectory = Backups.Allocate("UnplugModule", target);
                var request = new Governance.ChangeRequest("UnplugModule", target, modulePath)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeRemoval(Portal.UnplugModule(modulePath, backupDirectory), modulePath),
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to unplug '{modulePath}'");
            }
        }

        /// <remarks>
        /// The answer carries the recipe for putting the module back, not just the news that it is
        /// gone. The backup file holds the same record, and a caller that has to go and find a file
        /// to undo the last thing it did will not find it.
        /// </remarks>
        private static ResponseMessage DescribeRemoval(TiaMcpServer.Siemens.ModuleInfo removed, string modulePath)
        {
            var putItBack = removed.Addresses.Length == 0
                ? "."
                : $", then SetModuleAddress to {removed.Addresses}.";

            return new ResponseMessage
            {
                Message = $"'{modulePath}' removed from slot {removed.PositionNumber}. To put it back: " +
                          $"PlugModule with '{removed.TypeIdentifier}' in slot {removed.PositionNumber}" + putItBack,
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["typeIdentifier"] = removed.TypeIdentifier,
                    ["positionNumber"] = removed.PositionNumber,
                    ["addresses"] = removed.Addresses
                }
            };
        }

        [McpServerTool(Name = "MoveModule"), Description("Move a module to another slot of the rack it is in, keeping the module and everything set on it - which is what makes this better than unplugging and plugging again. Nothing is displaced: a slot with something in it is refused. Addresses do not follow a module, so read GetIoAddresses afterwards. The layout is recorded to the backup registry first.")]
        public static ResponseMessage MoveModule(
            [Description("modulePath: the module to move, as GetPlugLocations names it")] string modulePath,
            [Description("positionNumber: the slot to move it to, free, as GetPlugLocations numbers them")] int positionNumber)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(modulePath);
                var backupDirectory = Backups.Allocate("MoveModule", target);
                var request = new Governance.ChangeRequest("MoveModule", target, DescribeSlot(positionNumber))
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var moved = Portal.MoveModule(modulePath, positionNumber, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"'{moved}' is in slot {positionNumber} now",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["moduleName"] = moved
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to move '{modulePath}' to slot {positionNumber}");
            }
        }

        [McpServerTool(Name = "CopyModule"), Description("Copy a module into a free slot of the rack it is in. The copy carries the original's parameters, which is the point: a card configured once can be repeated. TIA chooses the copy's name and it is read back rather than asked for. A slot with something in it is refused. The layout is recorded to the backup registry first.")]
        public static ResponseMessage CopyModule(
            [Description("modulePath: the module to copy, as GetPlugLocations names it")] string modulePath,
            [Description("positionNumber: the free slot to copy it into, as GetPlugLocations numbers them")] int positionNumber)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(modulePath);
                var backupDirectory = Backups.Allocate("CopyModule", target);
                var request = new Governance.ChangeRequest("CopyModule", target, DescribeSlot(positionNumber))
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var copy = Portal.CopyModule(modulePath, positionNumber, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"'{modulePath}' copied into slot {positionNumber} as '{copy}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["moduleName"] = copy
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to copy '{modulePath}' into slot {positionNumber}");
            }
        }

        /// <remarks>
        /// A plan records what a change was aimed at, and a slot number is that for these two. The
        /// culture is fixed because an audit trail read on another machine must say the same thing.
        /// </remarks>
        private static string DescribeSlot(int positionNumber)
        {
            return "slot " + positionNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        [McpServerTool(Name = "PlugModule"), Description("Plug a module into one slot of the rack a device item sits in: an IO card, a power supply, a communications processor. Take the slot and the order number from GetPlugLocations. Nothing is replaced: a slot holding something else is refused, and plugging the same module into the same slot twice reports the one that is there. The module layout is recorded to the backup registry first. Compile the hardware afterwards, or a download writes a configuration that no longer matches the station.")]
        public static ResponseMessage PlugModule(
            [Description("deviceItemPath: a device item in the rack, e.g. 'PLC_0'. Its neighbours are the slots.")] string deviceItemPath,
            [Description("typeIdentifier: what to plug, as Openness names it, e.g. 'OrderNumber:6ES7 521-1BL00-0AB0/V2.1'")] string typeIdentifier,
            [Description("moduleName: name for the module in the project, e.g. 'DI 32x24VDC'")] string moduleName,
            [Description("positionNumber: the slot, as GetPlugLocations numbers them")] int positionNumber)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var module = new TiaMcpServer.Siemens.ModuleToPlug(typeIdentifier, moduleName, positionNumber);

                var target = ChangeTarget.Program(deviceItemPath);
                var backupDirectory = Backups.Allocate("PlugModule", target);
                var request = new Governance.ChangeRequest("PlugModule", target, module.TypeIdentifier)
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var plugged = Portal.PlugModule(deviceItemPath, module, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"'{plugged}' is in slot {positionNumber} of the rack holding '{deviceItemPath}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["moduleName"] = plugged
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to plug '{typeIdentifier}' into slot {positionNumber} of the rack holding '{deviceItemPath}'");
            }
        }
    }
}
