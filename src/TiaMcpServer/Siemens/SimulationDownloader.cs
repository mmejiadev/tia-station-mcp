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

            var target = ResolveSimulationTarget(provider);

            _logger?.LogInformation("Downloading {Device} through '{Interface}'...", _deviceItem.Name, ResolveTargetName());

            // DownloadOptions.None is rejected outright with "Invalid download option": the call
            // has to say what to download. A virtual controller starts empty, so its hardware
            // configuration has to go with the software or there is nothing for the blocks to
            // run on.
            const DownloadOptions options = DownloadOptions.Hardware | DownloadOptions.Software;

            // Connection and target address are two different things, and the five-argument
            // overload is where that shows. Measured on this project: the target interface
            // ('1 X1') carries no addresses at all, while the subnet ('PN/IE_1') carries
            // 192.168.0.1 — which is the split the download dialog displays, connection at the
            // top and target device in the table below.
            var address = ResolveSimulationAddress(provider);

            var result = address == null
                ? provider.Download(target, Answer, Answer, options)
                : provider.Download(target, address, Answer, Answer, options);

            return DownloadResultReader.Read(result);
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
        /// The subnet, not the target interface. TIA Portal's own download dialog fills
        /// "Connection to interface/subnet" with the subnet — <c>PN/IE_1</c> for this project — and
        /// then finds the CPU. Passing the target interface (<c>1 X1</c>) instead is accepted by
        /// the API and fails at connection time with "Connect to module failed", which is what
        /// cost seven attempts here. The target interface is kept as a fallback for a CPU that
        /// exposes no subnet.
        /// </remarks>
        private static ConfigurationTargetInterface ResolveSimulationTarget(DownloadProvider provider)
        {
            var pcInterface = ResolveSimulationInterface(provider);

            return pcInterface.TargetInterfaces.FirstOrDefault()
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{pcInterface.Name}' exposes no target interface");
        }

        /// <summary>The address of the controller to reach, or null when none is published.</summary>
        private static ConfigurationAddress? ResolveSimulationAddress(DownloadProvider provider)
        {
            var pcInterface = ResolveSimulationInterface(provider);

            return pcInterface.Subnets.SelectMany(subnet => subnet.Addresses).FirstOrDefault()
                ?? pcInterface.TargetInterfaces.SelectMany(target => target.Addresses).FirstOrDefault();
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
                [typeof(DataBlockReinitialization)] = c => ((DataBlockReinitialization)c).CurrentSelection = DataBlockReinitializationSelections.StopPlcAndReinitialize
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
            // caller stuck with no output and no error has nothing to act on. Naming the type
            // turns that into a one-line fix in the table above.
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
