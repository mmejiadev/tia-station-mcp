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
    ///
    /// **The handle is the controller's lifetime.** Measured on 2026-08-17: a virtual controller
    /// created through the API and then left with no open handle unregisters itself within fifteen
    /// seconds, with nothing touching it. Every failure this project chased from August onwards —
    /// downloads reporting "Connect to module failed", pings that answered on one run and timed
    /// out on the next, device scans finding nothing — was that controller no longer existing.
    /// It also explains why downloading by hand worked: the PLCSIM Advanced GUI keeps its own
    /// handle open, so an instance created there survives.
    ///
    /// So handles for controllers this object created are **held** for as long as they exist, and
    /// released in <see cref="Dispose"/> or <see cref="DeleteInstance"/>. CA2000 correctly spotted
    /// that <c>IInstance</c> is disposable; the conclusion drawn from it — dispose immediately —
    /// was the opposite of what this API requires. Controllers created elsewhere are somebody
    /// else's to keep alive, so those are opened transiently, the same ownership distinction
    /// <see cref="Portal"/> makes about the TIA Portal process.
    /// </remarks>
    public sealed class SimulationRuntime : IDisposable
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

#if PLCSIM_AVAILABLE
        // Keyed by instance name. Holding the handle is what keeps the virtual controller
        // registered; see the class remarks.
        private readonly Dictionary<string, IInstance> _heldInstances = new Dictionary<string, IInstance>(StringComparer.Ordinal);
#endif

        /// <summary>Creates a simulation runtime facade.</summary>
        /// <param name="logger">Optional logger.</param>
        public SimulationRuntime(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Releases every controller handle this object holds.</summary>
        /// <remarks>
        /// The controllers unregister themselves once their handle is gone, so disposing this is
        /// equivalent to shutting the virtual controllers down. That is the intended behaviour for
        /// a server shutting down, and the reason this object must be shared rather than created
        /// per operation.
        /// </remarks>
        public void Dispose()
        {
#if PLCSIM_AVAILABLE
            foreach (var instance in _heldInstances.Values)
            {
                instance.Dispose();
            }

            _heldInstances.Clear();
#endif
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
        /// <param name="cpuTypeName">
        /// CPU to emulate, as the runtime names it, for example <c>CPU1511</c>. Null creates the
        /// unspecified controller Siemens documents as the normal choice.
        /// </param>
        /// <returns>The instance as it stands after powering on.</returns>
        /// <exception cref="PortalException">The runtime is unavailable, the name is taken, or the CPU type is unknown.</exception>
        public SimulationInstanceInfo CreateInstance(string instanceName, string? cpuTypeName = null)
        {
            RequireRuntime();
            RequireName(instanceName);

#if PLCSIM_AVAILABLE
            var cpuType = ParseCpuType(cpuTypeName);

            return Execute("CreateInstance", instanceName, () =>
            {
                if (SimulationRuntimeManager.RegisteredInstanceInfo.Any(info => info.Name == instanceName))
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"A simulation instance named '{instanceName}' already exists");
                }

                // Deliberately not disposed here: this handle is the controller's lifetime, and
                // releasing it unregisters the controller within seconds. See the class remarks.
                //
                // Created as a named CPU type rather than the unspecified one Siemens documents as
                // the normal choice. Unspecified is fine for the hardware configuration, which
                // downloads successfully either way, but the text libraries are tied to the device
                // identity and fail with InvalidAID when the controller does not know what it is.
                var instance = cpuType == null
                    ? SimulationRuntimeManager.RegisterInstance(instanceName)
                    : SimulationRuntimeManager.RegisterInstance(cpuType.Value, instanceName);

                try
                {
                    // Registering only reserves the name. Until it is powered on the instance
                    // cannot accept a download, which is the whole reason to create one.
                    instance.PowerOn(TransitionTimeoutMilliseconds);
                }
                catch (Exception)
                {
                    // Do not leave a half-created controller behind for the next run.
                    instance.UnregisterInstance();
                    instance.Dispose();
                    throw;
                }

                _heldInstances[instanceName] = instance;

                _logger?.LogInformation("Simulation instance {Name} created, powered on and held", instanceName);

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

        /// <summary>Powers a virtual controller off and on again.</summary>
        /// <param name="instanceName">The instance name.</param>
        /// <returns>The instance as it stands afterwards.</returns>
        /// <remarks>
        /// <see cref="SetInstanceAddress"/> is accepted by a controller that is already powered
        /// on, and the instance then reports the new address — but reporting it and having the
        /// virtual interface bound to it on the adapter are not the same thing. This exists so
        /// that difference can be measured rather than assumed.
        /// </remarks>
        /// <exception cref="PortalException">The runtime is unavailable or the instance is unknown.</exception>
        public SimulationInstanceInfo PowerCycleInstance(string instanceName)
        {
            return WithInstance(instanceName, instance =>
            {
                // Powering off an instance that is already off throws InvalidOperatingState.
                if (instance.OperatingState != EOperatingState.Off)
                {
                    instance.PowerOff(TransitionTimeoutMilliseconds);
                }

                instance.PowerOn(TransitionTimeoutMilliseconds);
            });
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
                UseInstance(instanceName, instance =>
                {
                    // Powering off an instance that is already off throws InvalidOperatingState,
                    // so a cleanup path that always calls PowerOff turns "nothing to do" into a
                    // failure — which is exactly how this was found.
                    if (instance.OperatingState != EOperatingState.Off)
                    {
                        instance.PowerOff(TransitionTimeoutMilliseconds);
                    }

                    instance.UnregisterInstance();
                });

                ReleaseHeldInstance(instanceName);

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
                UseInstance(instanceName, action);

                return Describe(instanceName);
            });
        }

        /// <summary>
        /// Runs an action against a controller, without ever disposing a handle we are holding.
        /// </summary>
        /// <remarks>
        /// Disposing a held handle would unregister the controller mid-operation. A controller
        /// somebody else created is theirs to keep alive, so that handle is transient and is
        /// released here.
        /// </remarks>
        private void UseInstance(string instanceName, Action<IInstance> action)
        {
            if (_heldInstances.TryGetValue(instanceName, out var held))
            {
                action(held);

                return;
            }

            using var transient = Open(instanceName);

            action(transient);
        }

        /// <summary>Drops the handle for a controller that no longer exists.</summary>
        private void ReleaseHeldInstance(string instanceName)
        {
            if (!_heldInstances.TryGetValue(instanceName, out var held))
            {
                return;
            }

            held.Dispose();
            _heldInstances.Remove(instanceName);
        }

        /// <summary>Turns a CPU type name into the runtime's enum, or null when none was asked for.</summary>
        private static ECPUType? ParseCpuType(string? cpuTypeName)
        {
            if (string.IsNullOrWhiteSpace(cpuTypeName))
            {
                return null;
            }

            if (!Enum.TryParse<ECPUType>(cpuTypeName, ignoreCase: true, out var parsed))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{cpuTypeName}' is not a CPU type this runtime knows. Try one of: {string.Join(", ", Enum.GetNames(typeof(ECPUType)))}");
            }

            return parsed;
        }

        private static IInstance Open(string instanceName)
        {
            if (!SimulationRuntimeManager.RegisteredInstanceInfo.Any(info => info.Name == instanceName))
            {
                throw new PortalException(PortalErrorCode.NotFound, $"No simulation instance named '{instanceName}'");
            }

            return SimulationRuntimeManager.CreateInterface(instanceName);
        }

        private SimulationInstanceInfo Describe(string instanceName)
        {
            if (_heldInstances.TryGetValue(instanceName, out var held))
            {
                return Snapshot(held);
            }

            using var instance = SimulationRuntimeManager.CreateInterface(instanceName);

            return Snapshot(instance);
        }

        private static SimulationInstanceInfo Snapshot(IInstance instance)
        {
            return new SimulationInstanceInfo(
                instance.Name,
                instance.OperatingState.ToString(),
                instance.CPUType.ToString(),
                instance.ControllerIPSuite4.Select(suite => suite.IPAddress.ToString()).ToList(),
                instance.LicenseStatus.ToString());
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
