using Microsoft.Extensions.Logging;
using Siemens.Engineering.Connection;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Online;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Going online with a CPU, and seeing what answers on a network: TIA Portal's "Accessible devices" and the
    /// Go online and Go offline buttons. See <see cref="PcInterfaceRule"/> for what was measured on V21, above all
    /// that TIA Portal offers only PLCSIM while any PLCSIM Advanced instance exists.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>The PC network interfaces TIA Portal offers for going online with a CPU.</summary>
        /// <param name="plcPath">The PLC's software, for example <c>PLC_1</c>.</param>
        /// <returns>Each interface of each connection mode.</returns>
        /// <exception cref="PortalException">No project is open, or the path is not a PLC with an online connection.</exception>
        /// <remarks>Reads TIA Portal's configuration only: no network is contacted.</remarks>
        public IReadOnlyList<PcInterfaceInfo> GetPcInterfaces(string plcPath)
        {
            _logger?.LogInformation("Reading the PC interfaces offered for {PlcPath}...", plcPath);

            try
            {
                return RequireOnlineProvider(plcPath).Configuration.Modes
                    .SelectMany(mode => mode.PcInterfaces.Select(pcInterface => new PcInterfaceInfo(mode.Name, pcInterface.Name, pcInterface.Number)))
                    .ToList();
            }
            catch (Exception ex)
            {
                throw DecorateOnlineFailure(ex, plcPath, "Reading the PC interfaces");
            }
        }

        /// <summary>Searches which devices answer through one PC interface, as TIA Portal's "Accessible devices" does.</summary>
        /// <param name="plcPath">The PLC whose connection settings name the interface.</param>
        /// <param name="pcInterfaceName">The interface, exactly as <see cref="GetPcInterfaces"/> names it.</param>
        /// <returns>Each device that answered.</returns>
        /// <exception cref="PortalException">No project, no such PLC, no such interface, or TIA Portal failed.</exception>
        /// <remarks>
        /// It sends a discovery request to the network behind the interface and changes nothing on any device, and it
        /// is still contact with that network: the caller asks the policy first.
        /// </remarks>
        public IReadOnlyList<AccessibleDeviceInfo> ScanAccessibleDevices(string plcPath, string pcInterfaceName)
        {
            _logger?.LogInformation("Searching accessible devices through {PcInterface}...", pcInterfaceName);

            try
            {
                return RequirePcInterface(RequireOnlineProvider(plcPath), pcInterfaceName).GetAccessibleDevices()
                    .Select(device => new AccessibleDeviceInfo(device.Name, device.Address, device.MACAddress, device.DeviceSeries))
                    .ToList();
            }
            catch (Exception ex)
            {
                throw DecorateOnlineFailure(ex, plcPath, $"Searching accessible devices through '{pcInterfaceName}'");
            }
        }

        /// <summary>Whether TIA Portal is online with a CPU.</summary>
        /// <param name="plcPath">The PLC's software.</param>
        /// <returns>The state as Openness names it, e.g. <c>Offline</c> or <c>Online</c>.</returns>
        /// <exception cref="PortalException">No project is open, or the path is not a PLC with an online connection.</exception>
        public string GetOnlineState(string plcPath)
        {
            try
            {
                return RequireOnlineProvider(plcPath).State.ToString();
            }
            catch (Exception ex)
            {
                throw DecorateOnlineFailure(ex, plcPath, "Reading the online state");
            }
        }

        /// <summary>Goes offline with a CPU.</summary>
        /// <param name="plcPath">The PLC's software.</param>
        /// <returns>The state afterwards, as Openness names it.</returns>
        /// <exception cref="PortalException">No project is open, the path is not a PLC, or TIA Portal failed.</exception>
        public string GoOffline(string plcPath)
        {
            _logger?.LogInformation("Going offline with {PlcPath}...", plcPath);

            try
            {
                var online = RequireOnlineProvider(plcPath);

                online.GoOffline();

                return online.State.ToString();
            }
            catch (Exception ex)
            {
                throw DecorateOnlineFailure(ex, plcPath, "Going offline");
            }
        }

        /// <summary>Goes online with a CPU, through a PLCSIM Advanced interface only.</summary>
        /// <param name="plcPath">The PLC's software.</param>
        /// <returns>The state afterwards, as Openness names it.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the path is not a PLC, no PLCSIM interface is on offer, the connection could not be applied, or
        /// TIA Portal could not go online.
        /// </exception>
        /// <remarks>
        /// Measured 2026-11-22 on V21, through the PLCSIM Virtual Ethernet Adapter to a downloaded CPU 1511: with
        /// <c>EnableLegacyCommunication</c> true on the online connection, its default, <c>GoOnline</c> killed TIA Portal
        /// (the first call, twice) or refused with "The connection cannot be established"; with it false it went online,
        /// every time. The interface is chosen and applied here rather than left to whatever was configured last, and it is
        /// PLCSIM or nothing: this never goes online with a controller on a real network.
        /// </remarks>
        public string GoOnline(string plcPath)
        {
            _logger?.LogInformation("Going online with {PlcPath}...", plcPath);

            try
            {
                var online = RequireOnlineProvider(plcPath);

                if (online.State == OnlineState.Online)
                {
                    return online.State.ToString();
                }

                ConnectThroughPlcsim(online);
                online.GoOnline();

                return online.State.ToString();
            }
            catch (Exception ex)
            {
                throw DecorateOnlineFailure(ex, plcPath, "Going online");
            }
        }

        private static void ConnectThroughPlcsim(OnlineProvider online)
        {
            var simulated = online.Configuration.Modes
                .SelectMany(mode => mode.PcInterfaces)
                .FirstOrDefault(pcInterface => PcInterfaceRule.IsSimulation(pcInterface.Name))
                ?? throw new PortalException(
                    PortalErrorCode.InvalidState,
                    "No PLCSIM interface is on offer, and going online is allowed through PLCSIM only. GetPcInterfaces lists what TIA Portal offers.");

            if (!ApplyPlcsimInterface(online.Configuration, simulated) || !online.Configuration.IsConfigured)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"The connection through '{simulated.Name}' could not be applied, so TIA Portal is not asked to go online");
            }

            online.Configuration.EnableLegacyCommunication = false;
        }

        private static bool ApplyPlcsimInterface(ConnectionConfiguration configuration, ConfigurationPcInterface pcInterface)
        {
            var target = pcInterface.TargetInterfaces.FirstOrDefault();
            if (target != null)
            {
                return configuration.ApplyConfiguration(target);
            }

            var address = pcInterface.Subnets.SelectMany(subnet => subnet.Addresses).FirstOrDefault()
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{pcInterface.Name}' offers no target interface and no subnet address to connect through");

            return configuration.ApplyConfiguration(address);
        }

        private OnlineProvider RequireOnlineProvider(string plcPath)
        {
            var software = RequireSoftware(plcPath);
            var cpu = (software.Parent as SoftwareContainer)?.Parent as DeviceItem;

            return cpu?.GetService<OnlineProvider>()
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{plcPath}' has no online connection to offer");
        }

        private static ConfigurationPcInterface RequirePcInterface(OnlineProvider online, string pcInterfaceName)
        {
            var offered = online.Configuration.Modes.SelectMany(mode => mode.PcInterfaces).ToList();

            return offered.FirstOrDefault(pcInterface => string.Equals(pcInterface.Name, (pcInterfaceName ?? string.Empty).Trim(), StringComparison.Ordinal))
                ?? throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"No PC interface '{pcInterfaceName}' is offered. Offered: {string.Join(", ", offered.Select(pcInterface => $"'{pcInterface.Name}'"))}");
        }

        private PortalException DecorateOnlineFailure(Exception ex, string plcPath, string operation)
        {
            var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"{operation} failed: {ex.Message}", null, ex);

            pex.Data["plcPath"] = plcPath;

            _logger?.LogError(pex, "{Operation} failed for {PlcPath}", operation, plcPath);

            return pex;
        }
    }
}
