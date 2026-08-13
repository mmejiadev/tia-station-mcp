using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
#if PLCSIM_AVAILABLE
using Siemens.Simatic.Simulation.Runtime;
#endif

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Drives PLCSIM Advanced: the virtual controllers generated code is tested against.
    /// </summary>
    /// <remarks>
    /// Compiled conditionally. Without the PLCSIM Advanced runtime API on the machine the project
    /// still builds, and every call here reports that simulation is unavailable rather than
    /// failing at load time — a developer without PLCSIM must still be able to work on everything
    /// else.
    ///
    /// Instance handles never leave this class. They hold a live connection to a virtual PLC, so a
    /// caller keeping one alive would keep the controller alive with it.
    /// </remarks>
    public sealed class SimulationRuntime
    {
        private const string UnavailableMessage =
            "The PLCSIM Advanced runtime is not available. Install PLCSIM Advanced, or set PlcSimApiPath if it is installed somewhere unusual.";

        // State changes are not instantaneous, and the parameterless overloads do not wait for
        // them. Passing an explicit timeout is what makes a create-run-stop-delete sequence
        // deterministic instead of racing the controller.
        private const uint TransitionTimeoutMilliseconds = 60000;

        // A simulated controller sits on an isolated virtual adapter, so it has nowhere to route to.
        private const string DefaultGateway = "0.0.0.0";

        private readonly ILogger? _logger;

        /// <summary>Creates a simulation runtime facade.</summary>
        /// <param name="logger">Optional logger.</param>
        public SimulationRuntime(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Whether the PLCSIM Advanced runtime is installed and reachable.</summary>
        public static bool IsAvailable
        {
            get
            {
#if PLCSIM_AVAILABLE
                try
                {
                    return ProbeRuntime();
                }
                catch (Exception exception) when (IsMissingRuntime(exception))
                {
                    return false;
                }
#else
                return false;
#endif
            }
        }

#if PLCSIM_AVAILABLE
        /// <remarks>
        /// Kept in its own non-inlined method on purpose. The CLR loads an assembly when it
        /// compiles the method that mentions its types, not when the line runs, so touching
        /// SimulationRuntimeManager directly inside the property above throws
        /// <see cref="System.IO.FileNotFoundException"/> *before* entering the try block, and the
        /// graceful degradation this class promises would not happen at all. Found the hard way.
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ProbeRuntime()
        {
            return SimulationRuntimeManager.IsRuntimeManagerAvailable;
        }
#endif

        private static bool IsMissingRuntime(Exception exception)
        {
            return exception is System.IO.FileNotFoundException
                || exception is TypeLoadException
                || exception is BadImageFormatException
                || exception is System.IO.FileLoadException;
        }

        /// <summary>
        /// How the runtime is reachable: <c>Softbus</c>, <c>TCPIPSingleAdapter</c>,
        /// <c>TCPIPMultipleAdapter</c>, or <c>Unavailable</c> when there is no runtime.
        /// </summary>
        /// <remarks>
        /// This decides whether a download can connect at all. In softbus mode instances have no
        /// IP address and are reached over the PLCSIM bus; a download aimed at the virtual
        /// Ethernet adapter then fails with "Connect to module failed", which says nothing about
        /// the real cause. Reporting the mode is what turns that into a diagnosable problem.
        /// </remarks>
        public static string NetworkMode
        {
            get
            {
#if PLCSIM_AVAILABLE
                try
                {
                    return ReadNetworkMode();
                }
                catch (Exception exception) when (IsMissingRuntime(exception))
                {
                    return "Unavailable";
                }
#else
                return "Unavailable";
#endif
            }
        }

#if PLCSIM_AVAILABLE
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadNetworkMode()
        {
            return SimulationRuntimeManager.NetworkMode.ToString();
        }
#endif

        /// <summary>
        /// Switches the runtime to TCP/IP so downloads can reach an instance over the virtual
        /// Ethernet adapter. Returns the mode in force afterwards.
        /// </summary>
        /// <remarks>
        /// Environment setup, not a server capability, and deliberately not exposed as an MCP
        /// tool: letting an agent reconfigure PLCSIM is what phase 7 exists to prevent.
        ///
        /// The setting does **not** persist across processes — setting it from a separate script
        /// looks like it worked and then has no effect on the next run, which cost one wasted
        /// diagnosis here. It has to be set in the process that will use it, before any instance
        /// is registered.
        /// </remarks>
        /// <returns>The network mode after the attempt.</returns>
        public static string UseTcpIpNetworkMode()
        {
#if PLCSIM_AVAILABLE
            try
            {
                return SetTcpIpNetworkMode();
            }
            catch (Exception exception) when (IsMissingRuntime(exception))
            {
                return "Unavailable";
            }
#else
            return "Unavailable";
#endif
        }

#if PLCSIM_AVAILABLE
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string SetTcpIpNetworkMode()
        {
            if (SimulationRuntimeManager.NetworkMode != ENetworkMode.TCPIPSingleAdapter)
            {
                SimulationRuntimeManager.NetworkMode = ENetworkMode.TCPIPSingleAdapter;
            }

            return SimulationRuntimeManager.NetworkMode.ToString();
        }
#endif

        /// <summary>Lists the virtual controllers currently registered with the runtime.</summary>
        /// <returns>One entry per registered instance.</returns>
        /// <exception cref="PortalException">The runtime is not available.</exception>
        public IReadOnlyList<SimulationInstanceInfo> ListInstances()
        {
            RequireRuntime();

#if PLCSIM_AVAILABLE
            return SimulationRuntimeManager.RegisteredInstanceInfo
                .Select(info => Describe(info.Name))
                .ToList();
#else
            return Array.Empty<SimulationInstanceInfo>();
#endif
        }

        /// <summary>
        /// Registers a virtual controller and powers it on, so it is ready to be downloaded to.
        /// </summary>
        /// <param name="instanceName">Name for the instance, unique within the runtime.</param>
        /// <returns>The instance as it stands after powering on.</returns>
        /// <exception cref="PortalException">The runtime is unavailable or the name is taken.</exception>
        public SimulationInstanceInfo CreateInstance(string instanceName)
        {
            RequireRuntime();
            RequireName(instanceName);

#if PLCSIM_AVAILABLE
            return Execute("CreateInstance", instanceName, () =>
            {
                if (SimulationRuntimeManager.RegisteredInstanceInfo.Any(info => info.Name == instanceName))
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"A simulation instance named '{instanceName}' already exists");
                }

                // Every IInstance holds a live connection to the virtual controller, so each one
                // is released as soon as its work is done — the same rule as TIA Portal objects.
                using (var instance = SimulationRuntimeManager.RegisterInstance(instanceName))
                {
                    try
                    {
                        // Registering only reserves the name. Until it is powered on the instance
                        // cannot accept a download, which is the whole reason to create one.
                        instance.PowerOn(TransitionTimeoutMilliseconds);

                        _logger?.LogInformation("Simulation instance {Name} created and powered on", instanceName);
                    }
                    catch (Exception)
                    {
                        // Do not leave a half-created controller behind for the next run.
                        instance.UnregisterInstance();
                        throw;
                    }
                }

                return Describe(instanceName);
            });
#else
            throw new PortalException(PortalErrorCode.InvalidState, UnavailableMessage);
#endif
        }

        /// <summary>
        /// Gives a virtual controller an address, so TIA Portal can find it.
        /// </summary>
        /// <param name="instanceName">The instance name.</param>
        /// <param name="ipAddress">Address to assign. Must match the CPU's address in the project.</param>
        /// <param name="subnetMask">Subnet mask, typically <c>255.255.255.0</c>.</param>
        /// <returns>The instance as it stands afterwards.</returns>
        /// <remarks>
        /// Required before a download, and the reason a download otherwise fails with the useless
        /// "Connect to module failed". In TCP/IP mode a freshly created instance reports
        /// <c>0.0.0.0</c>: it has no address until one is assigned. The download is what would
        /// normally configure the hardware, so waiting for it to provide the address is circular —
        /// the address has to come first.
        /// </remarks>
        /// <exception cref="PortalException">The runtime is unavailable or the instance is unknown.</exception>
        public SimulationInstanceInfo SetInstanceAddress(string instanceName, string ipAddress, string subnetMask)
        {
            RequireRuntime();
            RequireName(instanceName);

            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(subnetMask))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "ipAddress and subnetMask are required");
            }

#if PLCSIM_AVAILABLE
            return Execute("SetInstanceAddress", instanceName, () =>
            {
                using (var instance = Open(instanceName))
                {
                    instance.SetIPSuite(0, new SIPSuite4(ipAddress, subnetMask, DefaultGateway), true);
                }

                _logger?.LogInformation("Simulation instance {Name} addressed at {Address}", instanceName, ipAddress);

                return Describe(instanceName);
            });
#else
            throw new PortalException(PortalErrorCode.InvalidState, UnavailableMessage);
#endif
        }

        /// <summary>Puts a virtual controller into RUN.</summary>
        /// <param name="instanceName">The instance name.</param>
        /// <returns>The instance as it stands afterwards.</returns>
        /// <exception cref="PortalException">The runtime is unavailable or the instance is unknown.</exception>
        public SimulationInstanceInfo StartInstance(string instanceName)
        {
            return WithInstance(instanceName, instance => instance.Run(TransitionTimeoutMilliseconds));
        }

        /// <summary>Puts a virtual controller into STOP.</summary>
        /// <param name="instanceName">The instance name.</param>
        /// <returns>The instance as it stands afterwards.</returns>
        /// <exception cref="PortalException">The runtime is unavailable or the instance is unknown.</exception>
        public SimulationInstanceInfo StopInstance(string instanceName)
        {
            return WithInstance(instanceName, instance => instance.Stop(TransitionTimeoutMilliseconds));
        }

        /// <summary>Powers off a virtual controller and removes it from the runtime.</summary>
        /// <param name="instanceName">The instance name.</param>
        /// <exception cref="PortalException">The runtime is unavailable or the instance is unknown.</exception>
        public void DeleteInstance(string instanceName)
        {
            RequireRuntime();
            RequireName(instanceName);

#if PLCSIM_AVAILABLE
            Execute("DeleteInstance", instanceName, () =>
            {
                using (var instance = Open(instanceName))
                {
                    // Powering off an instance that is already off throws InvalidOperatingState,
                    // so a cleanup path that always calls PowerOff turns "nothing to do" into a
                    // failure — which is exactly how this was found.
                    if (instance.OperatingState != EOperatingState.Off)
                    {
                        instance.PowerOff(TransitionTimeoutMilliseconds);
                    }

                    instance.UnregisterInstance();
                }

                _logger?.LogInformation("Simulation instance {Name} removed", instanceName);

                return true;
            });
#endif
        }

#if PLCSIM_AVAILABLE
        private SimulationInstanceInfo WithInstance(string instanceName, Action<IInstance> action)
        {
            RequireRuntime();
            RequireName(instanceName);

            return Execute("Instance state change", instanceName, () =>
            {
                using (var instance = Open(instanceName))
                {
                    action(instance);
                }

                return Describe(instanceName);
            });
        }

        private static IInstance Open(string instanceName)
        {
            if (!SimulationRuntimeManager.RegisteredInstanceInfo.Any(info => info.Name == instanceName))
            {
                throw new PortalException(PortalErrorCode.NotFound, $"No simulation instance named '{instanceName}'");
            }

            return SimulationRuntimeManager.CreateInterface(instanceName);
        }

        private static SimulationInstanceInfo Describe(string instanceName)
        {
            using (var instance = SimulationRuntimeManager.CreateInterface(instanceName))
            {
                return new SimulationInstanceInfo(
                    instance.Name,
                    instance.OperatingState.ToString(),
                    instance.CPUType.ToString(),
                    instance.ControllerIPSuite4.Select(suite => suite.IPAddress.ToString()).ToList());
            }
        }
#else
        private SimulationInstanceInfo WithInstance(string instanceName, Action<object> action)
        {
            throw new PortalException(PortalErrorCode.InvalidState, UnavailableMessage);
        }

        private static SimulationInstanceInfo Describe(string instanceName)
        {
            throw new PortalException(PortalErrorCode.InvalidState, UnavailableMessage);
        }
#endif

        /// <summary>
        /// Single decoration point for this class: the PLCSIM API throws its own
        /// <c>SimulationRuntimeException</c>, and letting that escape would mean callers face two
        /// unrelated error models depending on which part of the server they reached.
        /// </summary>
        private static T Execute<T>(string operation, string instanceName, Func<T> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException
                    ?? new PortalException(PortalErrorCode.SimulationFailed, $"{operation} failed: {ex.Message}", null, ex);

                pex.Data["instanceName"] = instanceName;

                throw pex;
            }
        }

        private static void RequireRuntime()
        {
            if (!IsAvailable)
            {
                throw new PortalException(PortalErrorCode.InvalidState, UnavailableMessage);
            }
        }

        private static void RequireName(string instanceName)
        {
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "instanceName is required");
            }
        }
    }
}
