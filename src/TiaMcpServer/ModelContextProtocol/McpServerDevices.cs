using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Hardware and the software on it: devices, device items, and the PLC software tree.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetDeviceInfo"), Description("Get info from a device from the current project/session")]
        public static ResponseDeviceInfo GetDeviceInfo(
            [Description("devicePath: defines the path in the project structure to the device")] string devicePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var device = Portal.GetDevice(devicePath);

                if (device != null)
                {
                    return new ResponseDeviceInfo
                    {
                        Message = $"Device info retrieved from '{devicePath}'",
                        Name = device.Name,
                        Attributes = device.Attributes,
                        Description = device.Description,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Device not found at '{devicePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving device info from '{devicePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetPlugLocations"), Description("List the slots of the rack a device item sits in: which are free and what is plugged into the rest, with each module's order number. Read this before plugging anything. A free slot is not the same as a slot that accepts a given module, and the order number to copy is the one printed here for a module like it.")]
        public static ResponseNetworkTopology GetPlugLocations(
            [Description("deviceItemPath: a device item in the rack, e.g. 'PLC_0'. Its neighbours are the slots.")] string deviceItemPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var slots = Portal.GetPlugLocations(deviceItemPath);

                var lines = slots.Select(DescribeSlot).ToList();

                var free = slots.Count(slot => slot.IsFree);

                return new ResponseNetworkTopology(lines)
                {
                    Message = free == 0
                        ? $"{slots.Count} slot(s) around '{deviceItemPath}', none free"
                        : $"{slots.Count} slot(s) around '{deviceItemPath}', {free} free",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the rack around '{deviceItemPath}'");
            }
        }

        /// <remarks>
        /// A free slot prints its label rather than an empty column, because the label is what the
        /// rack calls the position and is often the only hint that it is reserved for a power
        /// supply or an interface module.
        /// </remarks>
        private static string DescribeSlot(TiaMcpServer.Siemens.PlugLocationInfo slot)
        {
            return slot.IsFree
                ? $"{slot.PositionNumber} | <free> | {(slot.Label.Length == 0 ? "<unlabelled>" : slot.Label)}"
                : $"{slot.PositionNumber} | {slot.OccupantName} | {slot.OccupantTypeIdentifier}";
        }

        [McpServerTool(Name = "GetDeviceItemInfo"), Description("Get info from a device item from the current project/session")]
        public static ResponseDeviceItemInfo GetDeviceItemInfo(
            [Description("deviceItemPath: defines the path in the project structure to the device item")] string deviceItemPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var deviceItem = Portal.GetDeviceItem(deviceItemPath);

                if (deviceItem != null)
                {
                    return new ResponseDeviceItemInfo
                    {
                        Message = $"Device item info retrieved from '{deviceItemPath}'",
                        Name = deviceItem.Name,
                        Attributes = deviceItem.Attributes,
                        Description = deviceItem.Description,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Device item not found at '{deviceItemPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving device item info from '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetDevices"), Description("Get a list of all devices in the project/session")]
        public static ResponseDevices GetDevices()
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var list = Portal.GetDevices();
                var responseList = new List<ResponseDeviceInfo>();

                if (list != null)
                {
                    foreach (var device in list)
                    {
                        responseList.Add(new ResponseDeviceInfo
                        {
                            Name = device.Name,
                            Attributes = device.Attributes,
                            Description = device.Description
                        });
                    }

                    return new ResponseDevices
                    {
                        Message = "Devices retrieved",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving devices", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving devices: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetSoftwareInfo"), Description("Get plc software info")]
        public static ResponseSoftwareInfo GetSoftwareInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var software = Portal.GetPlcSoftware(softwarePath);
                if (software != null)
                {
                    return new ResponseSoftwareInfo
                    {
                        Message = $"Software info retrieved from '{softwarePath}'",
                        Name = software.Name,
                        Attributes = software.Attributes,
                        Description = software.Description,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Software not found at '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving software info from '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetSoftwareTree"), Description("Get the structure/tree of a given PLC software showing blocks, types, and external sources")]
        public static ResponseSoftwareTree GetSoftwareTree(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var tree = Portal.GetSoftwareTree(softwarePath);

                if (!string.IsNullOrEmpty(tree))
                {
                    return new ResponseSoftwareTree
                    {
                        Message = $"Software tree retrieved from '{softwarePath}'",
                        Tree = "```\n" + tree + "\n```",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving software tree from '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving software tree from '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }
    }
}
