using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// PLCSIM Advanced: the runtime's network mode, its instances and their tags.
    ///
    /// Part of McpServerWrites: every tool here changes something and goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        // Both halves of what a caller has to do next, and the difference between them is the point:
        // turning the setting on invalidates the compiled hardware configuration, while finding it
        // already on invalidates nothing.
        private const string SimulationSupportTurnedOn =
            "Simulation during block compilation is now on. Compile the software AND the hardware again: " +
            "the setting governs compilation, and turning it on invalidates the compiled hardware configuration.";

        private const string SimulationSupportAlreadyOn =
            "Simulation during block compilation was already on; nothing changed and nothing needs recompiling.";

        [McpServerTool(Name = "UseTcpIpNetworkMode"), Description("Put the PLCSIM Advanced runtime on the virtual Ethernet adapter, which a download needs: over the default Softbus a virtual controller is reachable only by PLCSIM itself and TIA Portal cannot find it. Call this BEFORE creating any instance. It is machine-wide and affects every PLCSIM user on this computer.")]
        public static ResponseMessage UseTcpIpNetworkMode()
        {
            try
            {
                // The runtime, not a controller: this is machine-wide, so it is its own target and a
                // policy allowing simulation/* deliberately does not allow it. See ChangeTarget.
                var request = new Governance.ChangeRequest("UseTcpIpNetworkMode", ChangeTarget.SimulationRuntime, "TCPIP");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        // In this process, and before any instance exists. Measured on 2026-08-17:
                        // setting it from a separate process reads back as applied and then has no
                        // effect on the process that creates the controllers, which is why this is a
                        // tool rather than something the caller does with a script.
                        var mode = TiaMcpServer.Siemens.SimulationRuntime.UseTcpIpNetworkMode();

                        // The mode after the attempt, not the fact that the attempt was made. This
                        // reported success unconditionally at first, and a runtime left on Softbus
                        // then let a caller go all the way to a download that failed with "Connect
                        // to module PLC_0 failed" — the one symptom this project has spent the most
                        // time on. A setting that did not take is a failure, and saying so here is
                        // the difference between one clear message and that diagnosis again.
                        if (!mode.StartsWith("TCPIP", StringComparison.Ordinal))
                        {
                            throw new TiaMcpServer.Siemens.PortalException(
                                TiaMcpServer.Siemens.PortalErrorCode.InvalidState,
                                $"The PLCSIM Advanced runtime is in {mode} mode and would not switch to TCP/IP. " +
                                "A download cannot reach a controller over Softbus. This has to be set before any " +
                                "instance is registered, so remove any existing instance and try again.");
                        }

                        return new ResponseMessage
                        {
                            Message = $"The PLCSIM Advanced runtime is now in {mode} mode",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["networkMode"] = mode
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed setting the PLCSIM Advanced network mode");
            }
        }

        [McpServerTool(Name = "EnableSimulationSupport"), Description("Turn on 'support simulation during block compilation' for the open project, which downloading to PLCSIM Advanced requires. Do this BEFORE compiling: the setting governs compilation, so blocks built without it stay unsimulatable however many times they are downloaded. It also invalidates the compiled hardware configuration, so compile the hardware again afterwards.")]
        public static ResponseMessage EnableSimulationSupport()
        {
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // A change to the project's own properties, so the target is the project — the same
                // name that governs saving and closing it. Guarded because without this setting no
                // program can run on a virtual controller and with it every program can: it is a
                // precondition for a download, not a diagnostic.
                var request = new Governance.ChangeRequest("EnableSimulationSupport", ChangeTarget.Project, "Enable");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var changed = Portal.EnableSimulationSupport();

                        return new ResponseMessage
                        {
                            // The distinction matters to the caller: if it was already on, nothing
                            // was invalidated and the program does not need compiling again.
                            Message = changed ? SimulationSupportTurnedOn : SimulationSupportAlreadyOn,
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["changed"] = changed
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, "Failed enabling simulation support");
            }
        }

        [McpServerTool(Name = "CreateSimulationInstance"), Description("Create a PLCSIM Advanced virtual controller and give it an address. The address must match the CPU's address in the project, otherwise TIA Portal cannot download to it. Pass cpuType matching the project's CPU: without it the controller is an unspecified one, and downloading text libraries to it fails with 'InvalidAID'.")]
        public static ResponseSimulationInstance CreateSimulationInstance(
            [Description("instanceName: a name for the virtual controller, unique within the runtime")] string instanceName,
            [Description("ipAddress: the address to assign, matching the CPU in the project, e.g. '192.168.0.1'")] string ipAddress,
            [Description("subnetMask: usually '255.255.255.0'")] string subnetMask = "255.255.255.0",
            [Description("cpuType: the CPU to emulate, e.g. 'CPU1511'. Omit for an unspecified controller, which cannot receive text libraries.")] string cpuType = "")
        {
            try
            {
                var request = new Governance.ChangeRequest("CreateSimulationInstance", ChangeTarget.Simulation(instanceName), ipAddress);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var runtime = Simulation;

                        // As the project's CPU when the caller says which, and not as the
                        // unspecified controller. Measured on 2026-08-21 by a harness that could
                        // not pass one: the hardware download succeeds either way, and then the
                        // text libraries fail with "Download of text libraries to device failed due
                        // to unknown reasons. (error code: InvalidAID)". Text libraries are tied to
                        // device identity, so an unspecified controller has no identity to match.
                        runtime.CreateInstance(instanceName, string.IsNullOrWhiteSpace(cpuType) ? null : cpuType);

                        return Describe(runtime.SetInstanceAddress(instanceName, ipAddress, subnetMask), $"Instance '{instanceName}' created at {ipAddress}");
                    },
                    () => new ResponseSimulationInstance(instanceName, string.Empty, string.Empty, Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to create simulation instance '{instanceName}'");
            }
        }

        [McpServerTool(Name = "StartSimulationInstance"), Description("Put a virtual controller into RUN. It must have a program: a controller that has never been downloaded to cannot start.")]
        public static ResponseSimulationInstance StartSimulationInstance(
            [Description("instanceName: the virtual controller to start")] string instanceName)
        {
            try
            {
                var request = new Governance.ChangeRequest("StartSimulationInstance", ChangeTarget.Simulation(instanceName), "Run");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var started = Simulation.StartInstance(instanceName);

                        return Describe(started, $"Instance '{instanceName}' is {started.OperatingState}");
                    },
                    () => new ResponseSimulationInstance(instanceName, string.Empty, string.Empty, Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to start simulation instance '{instanceName}'");
            }
        }

        [McpServerTool(Name = "StopSimulationInstance"), Description("Put a virtual controller into STOP.")]
        public static ResponseSimulationInstance StopSimulationInstance(
            [Description("instanceName: the virtual controller to stop")] string instanceName)
        {
            try
            {
                var request = new Governance.ChangeRequest("StopSimulationInstance", ChangeTarget.Simulation(instanceName), "Stop");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var stopped = Simulation.StopInstance(instanceName);

                        return Describe(stopped, $"Instance '{instanceName}' is {stopped.OperatingState}");
                    },
                    () => new ResponseSimulationInstance(instanceName, string.Empty, string.Empty, Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to stop simulation instance '{instanceName}'");
            }
        }

        [McpServerTool(Name = "WriteSimulationTag"), Description("Write one tag of a running virtual controller: how an input is driven so a program can be exercised. The value is text and is parsed as the tag's declared type, and what the controller holds afterwards is read back rather than echoed — a tag the program assigns every scan will not keep what you write.")]
        public static ResponseSimulationTagValue WriteSimulationTag(
            [Description("instanceName: the virtual controller to write to")] string instanceName,
            [Description("tagName: the tag name, spelled as ListSimulationTags reports it")] string tagName,
            [Description("value: the value as text — 'true', '17', '1.5'. A decimal point, never a comma.")] string value)
        {
            try
            {
                // The target is the controller, the same name that governs starting and stopping
                // it, because a policy author decides about controllers rather than about
                // individual tags — and stopping one is at least as consequential as driving an
                // input on it. Which tag and which value are the change's value, so the audit line
                // names them even though no rule matches on them.
                var request = new Governance.ChangeRequest(
                    "WriteSimulationTag",
                    ChangeTarget.Simulation(instanceName),
                    $"{tagName} := {value}");

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var written = Simulation.WriteTag(instanceName, tagName, value);

                        return new ResponseSimulationTagValue(written.Name, written.DataType, written.Value)
                        {
                            Message = $"'{written.Name}' on '{instanceName}' now holds {written.Value}",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseSimulationTagValue(tagName, string.Empty, null));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to write '{tagName}' on '{instanceName}'");
            }
        }

        [McpServerTool(Name = "DeleteSimulationInstance"), Description("Power off a virtual controller and remove it from the runtime.")]
        public static ResponseMessage DeleteSimulationInstance(
            [Description("instanceName: the virtual controller to remove")] string instanceName)
        {
            try
            {
                var request = new Governance.ChangeRequest("DeleteSimulationInstance", ChangeTarget.Simulation(instanceName));

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        Simulation.DeleteInstance(instanceName);

                        return new ResponseMessage
                        {
                            Message = $"Instance '{instanceName}' removed",
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
                throw ToMcpException(pex, $"Failed to remove simulation instance '{instanceName}'");
            }
        }
    }
}
