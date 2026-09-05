using Microsoft.Extensions.Logging;
using System;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// PLCSIM Advanced: the download target every deployment uses by default.
    ///
    /// The download to simulation took six chained failures to unblock, and the thing that
    /// unblocked it was better diagnosis rather than better reasoning. That is why these methods
    /// describe what they found -- DescribeSimulationConnection exists to be read by a person,
    /// not by the loop.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>
        /// Downloads a PLC program to a PLCSIM Advanced virtual controller.
        /// </summary>
        /// <param name="softwarePath">
        /// Full path to the CPU, for example <c>PLC_0</c>. The download service belongs to the
        /// device item rather than to the software.
        /// </param>
        /// <returns>What the download reported, in the same shape as a compile.</returns>
        /// <remarks>
        /// There is no counterpart for physical hardware, and that is deliberate: see
        /// <see cref="SimulationDownloader"/>.
        /// </remarks>
        /// <exception cref="PortalException">
        /// No project is open, the path does not resolve, or the PLCSIM virtual adapter is absent.
        /// </exception>
        public CompilationReport DownloadToSimulation(string softwarePath)
        {
            _logger?.LogInformation("Downloading {SoftwarePath} to simulation...", softwarePath);

            try
            {
                if (string.IsNullOrWhiteSpace(softwarePath))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "softwarePath is required");
                }

                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project before downloading");
                }

                var deviceItem = FindDeviceItem(softwarePath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Device item not found: {softwarePath}");

                return new SimulationDownloader(deviceItem, _logger).Download();
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.SimulationFailed, $"Download failed: {ex.Message}", null, ex);

                pex.Data["softwarePath"] = softwarePath;

                _logger?.LogError(pex, "DownloadToSimulation failed for {SoftwarePath}", softwarePath);
                throw pex;
            }
        }

        /// <summary>
        /// Reports which PC interface a download would go through, without downloading anything.
        /// </summary>
        /// <param name="softwarePath">Full path to the CPU, for example <c>PLC_0</c>.</param>
        /// <returns>The interface name. It is always a PLCSIM one, or the call throws.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the path does not resolve, or the only interfaces on offer are real
        /// network adapters.
        /// </exception>
        public string GetSimulationTargetName(string softwarePath)
        {
            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project first");
                }

                var deviceItem = FindDeviceItem(softwarePath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Device item not found: {softwarePath}");

                return new SimulationDownloader(deviceItem, _logger).ResolveTargetName();
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.SimulationFailed, $"Could not resolve a download target: {ex.Message}", null, ex);

                pex.Data["softwarePath"] = softwarePath;

                throw pex;
            }
        }

        /// <summary>
        /// Describes what a download would connect through and what answers there, without
        /// downloading.
        /// </summary>
        /// <param name="softwarePath">Full path to the CPU, for example <c>PLC_0</c>.</param>
        /// <returns>A multi-line report of the interface, the applied connection and the devices found.</returns>
        /// <remarks>
        /// The counterpart to <see cref="DownloadToSimulation"/> for when it fails: TIA Portal
        /// reports "Connect to module failed" and nothing else, so the state behind that message
        /// has to be readable on its own.
        /// </remarks>
        /// <exception cref="PortalException">
        /// No project is open, the path does not resolve, or the connection cannot be applied.
        /// </exception>
        public string DescribeSimulationConnection(string softwarePath)
        {
            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project first");
                }

                var deviceItem = FindDeviceItem(softwarePath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Device item not found: {softwarePath}");

                return new SimulationDownloader(deviceItem, _logger).DescribeConnection()
                    + Environment.NewLine
                    + "software: " + DescribeSoftwareSimulationSupport(softwarePath);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.SimulationFailed, $"Could not describe the download connection: {ex.Message}", null, ex);

                pex.Data["softwarePath"] = softwarePath;

                _logger?.LogError(pex, "DescribeSimulationConnection failed for {SoftwarePath}", softwarePath);
                throw pex;
            }
        }

        /// <summary>
        /// Whether blocks are compiled with simulation support.
        /// </summary>
        /// <returns>True when the open project compiles blocks that PLCSIM can run.</returns>
        /// <exception cref="PortalException">No project is open.</exception>
        public bool IsSimulationSupportEnabled()
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project first");
            }

            return (bool)_project!.GetAttribute(SimulationSupportAttribute);
        }

        /// <summary>
        /// Compiles blocks with simulation support, so PLCSIM can run them.
        /// </summary>
        /// <returns>True when the setting had to be changed, false when it was already on.</returns>
        /// <remarks>
        /// Without this a download reaches the controller, writes the hardware configuration
        /// successfully, and then refuses every block with *"'Main [OB1]' cannot be simulated"*.
        /// It cost this project five days, because the failure before the diagnostics were fixed
        /// looked like a connection problem rather than a compilation setting.
        ///
        /// It lives on the **project**, not on the CPU or the PLC software — TIA's own message
        /// says "in the project properties", and probing the other two objects finds nothing,
        /// which is exactly how the first search for it was abandoned.
        ///
        /// **Blocks compiled before this was on stay unsimulatable**: the flag governs
        /// compilation, so the program has to be compiled again afterwards.
        /// </remarks>
        /// <exception cref="PortalException">No project is open.</exception>
        public bool EnableSimulationSupport()
        {
            if (IsSimulationSupportEnabled())
            {
                return false;
            }

            _project!.SetAttribute(SimulationSupportAttribute, true);

            _logger?.LogInformation("Simulation during block compilation enabled; the program must be recompiled");

            return true;
        }

        // Named by TIA in the download failure it produces when the option is off.
        private const string SimulationSupportAttribute = "IsSimulationDuringBlockCompilationEnabled";

        /// <summary>
        /// Whether the PLC program permits simulation, which downloading to PLCSIM requires.
        /// </summary>
        /// <remarks>
        /// Siemens documents "support simulation during block compilation" as a prerequisite for
        /// downloading to a virtual controller, and it is not on the CPU device item — that one
        /// exposes no attribute matching "Simul" at all. Reporting what the object does expose is
        /// deliberate: a probe that cannot tell "asked and got nothing" from "asked the wrong
        /// object" has already cost this project days.
        /// </remarks>
        private string DescribeSoftwareSimulationSupport(string softwarePath)
        {
            var software = FindPlcSoftware(softwarePath);

            if (software == null)
            {
                return $"no PLC software at '{softwarePath}'";
            }

            return $"software '{software.Name}', simulation during block compilation: {IsSimulationSupportEnabled()}";
        }
    }
}
