using Microsoft.Extensions.Logging;
using Siemens.Engineering.Connection;
using Siemens.Engineering.Download;
using Siemens.Engineering.Download.Configurations;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Downloads a PLC program to a PLCSIM Advanced virtual controller.
    /// </summary>
    /// <remarks>
    /// The safety property this class relies on is that the **PC interface decides where the
    /// download goes**. TIA Portal offers the machine's real network adapters alongside the
    /// PLCSIM virtual one; picking a real adapter would push code at physical hardware. This class
    /// only ever selects the PLCSIM adapter and refuses outright when it is missing, so
    /// "download to simulation" is enforced by construction rather than by remembering to.
    ///
    /// Downloading to real hardware deliberately has no implementation here at all. Adding one is
    /// a decision with a confirmation flow attached, not a parameter.
    /// </remarks>
    public sealed class SimulationDownloader
    {
        // Matched rather than compared. PLCSIM Advanced exposes a different PC interface depending
        // on its network mode: "PLCSIM" over the softbus, "Siemens PLCSIM Virtual Ethernet
        // Adapter" over TCP/IP. Both contain this marker and no physical network card does, which
        // is what keeps the rule safe while covering both modes.
        private const string PlcSimInterfaceMarker = "PLCSIM";
        private const string ConfigurationModeName = "PN/IE";

        private readonly DeviceItem _deviceItem;
        private readonly ILogger? _logger;

        // Prompts the answer table has no entry for, recorded so the failure can name them.
        private readonly List<string> _unansweredPrompts = new List<string>();

        /// <summary>Creates a downloader for one CPU.</summary>
        /// <param name="deviceItem">The CPU device item that owns the download service.</param>
        /// <param name="logger">Optional logger.</param>
        public SimulationDownloader(DeviceItem deviceItem, ILogger? logger = null)
        {
            _deviceItem = deviceItem ?? throw new ArgumentNullException(nameof(deviceItem));
            _logger = logger;
        }

        /// <summary>Downloads hardware and software to the PLCSIM virtual controller.</summary>
        /// <returns>What the download reported, in the same shape as a compile.</returns>
        /// <exception cref="PortalException">
        /// The CPU exposes no download service, or the PLCSIM virtual adapter is not present, in
        /// which case the download is refused rather than sent somewhere else.
        /// </exception>
        public CompilationReport Download()
        {
            // The download service belongs to the CPU device item, not to the PlcSoftware.
            // Asking the software for it silently returns null.
            var provider = _deviceItem.GetService<DownloadProvider>()
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{_deviceItem.Name}' exposes no download service");

            var pcInterface = ResolveSimulationInterface(provider);
            var connection = ResolveConnection(pcInterface);

            _logger?.LogInformation(
                "IsConfigured before applying: {IsConfigured}", provider.Configuration.IsConfigured);

            ApplyConnection(provider, connection, pcInterface);

            _logger?.LogInformation("Downloading {Device} through '{Interface}'...", _deviceItem.Name, pcInterface.Name);

            // DownloadOptions.None is rejected outright with "Invalid download option": the call
            // has to say what to download. A virtual controller starts empty, so its hardware
            // configuration has to go with the software or there is nothing for the blocks to
            // run on.
            const DownloadOptions options = DownloadOptions.Hardware | DownloadOptions.Software;

            // Connection and target address are separate things, and the five-argument overload is
            // where that shows: the dialog fills "connection to interface/subnet" at the top and
            // picks the device from the table below. This pairing was tried in August and failed,
            // but that was before ApplyConfiguration was ever called, so it was never a fair test.
            //
            // ConfigurationAccessibleDevice is deliberately not used here. It is the third
            // implementer of IConfiguration and looks like the obvious thing to pass, and measured
            // on 2026-08-17 it kills the process: not an exception, not an error result — the host
            // dies after roughly 28 seconds and reports nothing. Do not retry without isolation.
            var address = ResolveSubnetAddress(pcInterface);

            try
            {
                var result = address == null
                    ? provider.Download(connection, Answer, Answer, options)
                    : provider.Download(connection, address, Answer, Answer, options);

                return DownloadResultReader.Read(result);
            }
            catch (Exception exception) when (_unansweredPrompts.Count > 0)
            {
                // Openness wraps whatever the delegate threw and drops its message, so refusing an
                // unknown prompt produced "Error when executing delegate" and nothing else — the
                // one detail needed to fix it. Naming the types here is what turns that into a
                // one-line change in the answer table.
                throw new PortalException(
                    PortalErrorCode.SimulationFailed,
                    $"The download asked something this server cannot answer: {string.Join(", ", _unansweredPrompts)}. " +
                    "Add it to the answer table in SimulationDownloader.",
                    null,
                    exception);
            }
        }

        /// <summary>
        /// The PC interface a download would go through, without downloading anything.
        /// </summary>
        /// <remarks>
        /// Worth having on its own: "where would this go?" is a question worth answering before
        /// acting, and it is the only way to check the simulation-only guarantee without
        /// performing a download.
        /// </remarks>
        /// <returns>The interface name.</returns>
        /// <exception cref="PortalException">No PLCSIM interface is on offer.</exception>
        public string ResolveTargetName()
        {
            var provider = _deviceItem.GetService<DownloadProvider>()
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{_deviceItem.Name}' exposes no download service");

            var pcInterface = ResolveSimulationInterface(provider);
            var subnet = pcInterface.Subnets.FirstOrDefault();

            return subnet == null ? pcInterface.Name : $"{pcInterface.Name} → {subnet.Name}";
        }

        /// <summary>
        /// The connection a download goes through.
        /// </summary>
        /// <remarks>
        /// The subnet's address, not the target interface. TIA Portal's own download dialog fills
        /// "Connection to interface/subnet" with the subnet — <c>PN/IE_1</c> for this project — and
        /// finds the CPU at the address listed underneath it. Measured here: the target interface
        /// (<c>1 X1</c>) carries no addresses at all, while the subnet carries 192.168.0.1.
        ///
        /// <c>ConfigurationSubnet</c> is deliberately not what gets passed: only
        /// <c>ConfigurationAddress</c>, <c>ConfigurationTargetInterface</c> and
        /// <c>ConfigurationAccessibleDevice</c> implement <c>IConfiguration</c>, which is what
        /// <c>Download</c> accepts. The target interface stays as a fallback for a CPU that
        /// exposes no subnet at all.
        /// </remarks>
        private static IConfiguration ResolveConnection(ConfigurationPcInterface pcInterface)
        {
            var target = pcInterface.TargetInterfaces.FirstOrDefault();

            if (target != null)
            {
                return target;
            }

            return ResolveSubnetAddress(pcInterface)
                ?? throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{pcInterface.Name}' exposes neither a target interface nor a subnet address to connect through");
        }

        /// <summary>The address published by the subnet, or null when the CPU is on none.</summary>
        private static ConfigurationAddress? ResolveSubnetAddress(ConfigurationPcInterface pcInterface)
        {
            return pcInterface.Subnets.SelectMany(subnet => subnet.Addresses).FirstOrDefault();
        }

        /// <summary>
        /// Establishes the connection, rather than merely describing it.
        /// </summary>
        /// <remarks>
        /// The step that was missing for seven attempts. A connection has to be **applied**:
        /// <c>ConnectionConfiguration</c> exposes <c>ApplyConfiguration</c> for an address and for
        /// a target interface, and until one of them is called <c>IsConfigured</c> stays false and
        /// every <c>Download</c> fails with the uninformative "Connect to module failed".
        ///
        /// Five earlier attempts varied *which* object was passed to <c>Download</c>. None of them
        /// could have worked, because the missing step was never an argument. Verified by
        /// reflecting over the real Siemens.Engineering.dll rather than by reasoning about it.
        /// </remarks>
        private static void ApplyConnection(
            DownloadProvider provider,
            IConfiguration connection,
            ConfigurationPcInterface pcInterface)
        {
            // V19 onwards defaults a CPU to "only allow secure PG/PC and HMI communication", and a
            // PLCSIM Advanced controller does not speak it. The symptom is a connection that
            // configures cleanly, finds the device, and then refuses — which is what this project
            // chased for days. Opting into legacy communication is what the download dialog does
            // for a simulated target.
            provider.Configuration.EnableLegacyCommunication = true;

            var isApplied = connection switch
            {
                ConfigurationAddress address => provider.Configuration.ApplyConfiguration(address),
                ConfigurationTargetInterface target => provider.Configuration.ApplyConfiguration(target),

                // Not a default: ConfigurationAccessibleDevice also implements IConfiguration and
                // has no ApplyConfiguration overload. Refusing names the gap instead of hiding it.
                _ => throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"A connection of type {connection.GetType().Name} cannot be applied")
            };

            if (isApplied && provider.Configuration.IsConfigured)
            {
                return;
            }

            throw new PortalException(
                PortalErrorCode.SimulationFailed,
                $"The connection through '{pcInterface.Name}' could not be established. " +
                $"Accessible devices: {DescribeAccessibleDevices(pcInterface)}");
        }

        /// <summary>
        /// Everything a failing download needs explained, without downloading anything.
        /// </summary>
        /// <remarks>
        /// "Connect to module failed" names neither the cause nor the layer it happened in. This
        /// applies the connection exactly as <see cref="Download"/> does and then reports what
        /// answered, which is the difference between a measurement and a guess.
        /// </remarks>
        /// <returns>A multi-line report.</returns>
        /// <exception cref="PortalException">
        /// The CPU exposes no download service, no PLCSIM interface is on offer, or the connection
        /// cannot be applied.
        /// </exception>
        public string DescribeConnection()
        {
            var provider = _deviceItem.GetService<DownloadProvider>()
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{_deviceItem.Name}' exposes no download service");

            var pcInterface = ResolveSimulationInterface(provider);
            var connection = ResolveConnection(pcInterface);

            ApplyConnection(provider, connection, pcInterface);

            return string.Join(
                Environment.NewLine,
                $"pc interface: '{pcInterface.Name}' number={pcInterface.Number}",
                "subnets: " + DescribeAddressed(pcInterface.Subnets.Select(subnet => (subnet.Name, subnet.Addresses.Select(address => address.Address)))),
                "target interfaces: " + DescribeAddressed(pcInterface.TargetInterfaces.Select(target => (target.Name, target.Addresses.Select(address => address.Address)))),
                $"applied {connection.GetType().Name}, IsConfigured={provider.Configuration.IsConfigured}",
                "cpu identity: " + DescribeCpuIdentity(),
                "accessible devices: " + DescribeAccessibleDevices(pcInterface));
        }

        /// <summary>Renders named things and the addresses they carry, or "none".</summary>
        private static string DescribeAddressed(IEnumerable<(string Name, IEnumerable<string> Addresses)> items)
        {
            var rendered = string.Join(", ", items.Select(item => $"'{item.Name}' addresses=[{string.Join(", ", item.Addresses)}]"));

            return string.IsNullOrEmpty(rendered) ? "none" : rendered;
        }

        /// <summary>Whether the CPU permits simulation at all.</summary>
        /// <remarks>
        /// "Support simulation during block compilation" is a protection setting on the CPU, and
        /// Siemens documents it as a prerequisite for downloading to a virtual controller. With it
        /// off, TIA Portal refuses the download and reports a connection failure rather than the
        /// configuration problem it actually is — which is indistinguishable, from the API, from a
        /// controller that cannot be reached.
        /// </remarks>
        private string DescribeCpuIdentity()
        {
            return string.Join(", ", IdentityAttributes.Select(ReadAttribute));
        }

        // What a device has to be created as, when building a project from scratch rather than
        // inheriting one.
        private static readonly string[] IdentityAttributes =
        {
            "OrderNumber",
            "FirmwareVersion",
            "TypeIdentifier",
            "TypeName"
        };

        private string ReadAttribute(string name)
        {
            try
            {
                return $"{name}={_deviceItem.GetAttribute(name)}";
            }
            catch (Exception exception)
            {
                // "This object does not answer to that name" is an answer, not a failure. A probe
                // that cannot tell that from "asked and got nothing" has already cost days here.
                return $"{name}=<absent: {exception.GetType().Name}>";
            }
        }

        /// <summary>What actually answered on the wire, so a failure can be acted on.</summary>
        /// <remarks>
        /// This is the API behind the download dialog's "n compatible devices of m accessible
        /// devices found". Only called on failure: it scans the network and is not free.
        /// </remarks>
        private static string DescribeAccessibleDevices(ConfigurationPcInterface pcInterface)
        {
            var devices = pcInterface.GetAccessibleDevices();

            return devices.Count == 0
                ? "none answered on this interface"
                : string.Join(", ", devices.Select(device => $"'{device.Name}' at {device.Address} (MAC {device.MACAddress}, {device.DeviceSeries})"));
        }

        private static ConfigurationPcInterface ResolveSimulationInterface(DownloadProvider provider)
        {
            var mode = provider.Configuration.Modes.FirstOrDefault(candidate => candidate.Name == ConfigurationModeName)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"The CPU offers no '{ConfigurationModeName}' connection mode");

            return mode.PcInterfaces.FirstOrDefault(
                    candidate => candidate.Name.IndexOf(PlcSimInterfaceMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                ?? throw new PortalException(
                    PortalErrorCode.InvalidState,
                    "No PLCSIM interface is offered by this CPU, so the only targets available are real network adapters. " +
                    $"Refusing to download. Available: {DescribeInterfaces(mode)}");
        }

        private static string DescribeInterfaces(ConfigurationMode mode)
        {
            return string.Join(", ", mode.PcInterfaces.Select(pcInterface => $"'{pcInterface.Name}'"));
        }

        /// <summary>
        /// The answer to each prompt TIA Portal can raise during a download.
        /// </summary>
        /// <remarks>
        /// Each prompt type carries its own selection enum, so there is no generic way to answer
        /// one. A strategy table keyed by type is what CLAUDE.md prescribes over a large switch.
        ///
        /// The values are chosen, not defaulted. Taking the first enum member would have been
        /// wrong: <c>StopModulesSelections</c> starts with <c>NoAction</c>, and a download that
        /// does not stop the modules fails. These answers are safe **because the target is always
        /// a virtual controller** — stopping, overwriting and reinitialising one costs nothing.
        /// The same answers against hardware would not be acceptable, which is a further reason
        /// downloading there is not implemented.
        /// </remarks>
        private static readonly Dictionary<Type, Action<DownloadConfiguration>> Answers =
            new Dictionary<Type, Action<DownloadConfiguration>>
            {
                [typeof(StopModules)] = c => ((StopModules)c).CurrentSelection = StopModulesSelections.StopAll,
                [typeof(StartModules)] = c => ((StartModules)c).CurrentSelection = StartModulesSelections.StartModule,
                [typeof(AllBlocksDownload)] = c => ((AllBlocksDownload)c).CurrentSelection = AllBlocksDownloadSelections.DownloadAllBlocks,
                [typeof(ConsistentBlocksDownload)] = c => ((ConsistentBlocksDownload)c).CurrentSelection = ConsistentBlocksDownloadSelections.ConsistentDownload,
                [typeof(OverwriteSystemData)] = c => ((OverwriteSystemData)c).CurrentSelection = OverwriteSystemDataSelections.Overwrite,
                [typeof(DataBlockReinitialization)] = c => ((DataBlockReinitialization)c).CurrentSelection = DataBlockReinitializationSelections.StopPlcAndReinitialize,

                // Asked whenever the CPU carries user management or access control settings, which
                // V19 onwards configures by default. A virtual controller starts empty, so there is
                // no online data worth keeping and the project is the only truth there is —
                // KeepOnlineUserManagementData would preserve nothing over the project's own
                // configuration. Against hardware this choice would need thought, which is one more
                // reason downloading there is not implemented.
                [typeof(UserManagementDownload)] = c => ((UserManagementDownload)c).CurrentSelection = UserManagementPreDownloadSelections.DownloadAllUserManagementDataResetToProject,

                // ConsistentDownload, and NoAction is not the safe-looking alternative it appears
                // to be: measured on 2026-08-17, skipping the text libraries makes the hardware
                // configuration itself fail to load ("0013 -32 0 0"), because they are part of it.
                // Both options were tried; this one at least gets the hardware in.
                [typeof(AlarmTextLibrariesDownload)] = c => ((AlarmTextLibrariesDownload)c).CurrentSelection = AlarmTextLibrariesDownloadSelections.ConsistentDownload,

                // Asked on 2026-08-21, the first time a download went to a controller created as
                // the project's own CPU rather than as the unspecified one — the harness of phase 3
                // was what got that far. The options are NoAction and DeleteAll, and DeleteAll is
                // the only one that means anything here: a virtual controller starts empty, so
                // there is nothing on it to lose, and NoAction leaves whatever is on the module
                // beside what is being written. Against hardware this is the answer that would
                // erase a machine's program, which is one more reason downloading there is not
                // implemented.
                [typeof(ResetModule)] = c => ((ResetModule)c).CurrentSelection = ResetModuleSelections.DeleteAll
            };

        private void Answer(DownloadConfiguration configuration)
        {
            if (configuration is DownloadPasswordConfiguration)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    "The download needs a password. Protected downloads are not supported yet.");
            }

            if (Answers.TryGetValue(configuration.GetType(), out var answer))
            {
                answer(configuration);

                return;
            }

            // Failing is the safe outcome, not leaving it at its default. An unanswered prompt
            // blocks the download forever — a run was lost to a thirteen-hour hang here — and a
            // caller stuck with no output and no error has nothing to act on.
            //
            // Recorded as well as thrown: Openness discards the message of anything a delegate
            // throws, so without this the caller gets "Error when executing delegate" and no way
            // to know which prompt it was.
            _unansweredPrompts.Add($"{configuration.GetType().Name} ('{configuration.Message}')");

            _logger?.LogError(
                "Unhandled download prompt {ConfigurationType}: '{Message}'",
                configuration.GetType().Name,
                configuration.Message);

            throw new PortalException(
                PortalErrorCode.SimulationFailed,
                $"The download asked something this server cannot answer: {configuration.GetType().Name} — '{configuration.Message}'. " +
                "Add it to the answer table in SimulationDownloader.");
        }
    }
}
