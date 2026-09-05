using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// PROFINET: the topology as it stands, and the IO system that binds devices to a controller.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>
        /// Reads the project's network layout: every device interface, its address and its subnet.
        /// </summary>
        /// <returns>One entry per interface, whether or not it is attached to a subnet.</returns>
        /// <exception cref="PortalException">No project is open.</exception>
        public IReadOnlyList<NetworkNodeInfo> GetNetworkTopology()
        {
            _logger?.LogInformation("Reading network topology...");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project before reading the network topology");
                }

                return new NetworkTopologyReader(_logger).Read(FindDevices());
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Reading the network topology failed: {ex.Message}", null, ex);

                _logger?.LogError(pex, "GetNetworkTopology failed");
                throw pex;
            }
        }

        /// <summary>
        /// Adds the network layout to a program snapshot.
        /// </summary>
        /// <remarks>
        /// A snapshot that records the program but not the network it runs on describes half a
        /// project: the same blocks addressing a device at a different address are a different
        /// system, and nothing in the export would show it.
        /// </remarks>
        private SnapshotResult WithNetworkTopology(SnapshotResult program, string targetDirectory)
        {
            var exported = new List<string>(program.Exported)
            {
                NetworkTopologyWriter.Write(targetDirectory, new NetworkTopologyReader(_logger).Read(FindDevices()))
            };

            return new SnapshotResult(exported, program.Inconsistent, program.Unsupported, program.Failed);
        }

        /// <summary>
        /// Creates a PROFINET IO system on a CPU, so IO devices can be attached to it.
        /// </summary>
        /// <param name="controllerPath">Full path to the CPU, for example <c>PLC_0</c>.</param>
        /// <param name="ioSystemName">Name for the IO system.</param>
        /// <param name="backupDirectory">
        /// Where the network layout is recorded before the change. Required: this rewires the
        /// project, and the repository rule is that every write is preceded by an export.
        /// </param>
        /// <returns>The subnet the IO system was created on.</returns>
        /// <exception cref="PortalException">
        /// The arguments are invalid, no project is open, the path does not resolve, or the device
        /// cannot act as an IO controller.
        /// </exception>
        public string CreateIoSystem(string controllerPath, string ioSystemName, string backupDirectory)
        {
            _logger?.LogInformation("Creating IO system {IoSystem} on {Controller}...", ioSystemName, controllerPath);

            try
            {
                var controller = RequireDeviceItemForWrite(controllerPath, backupDirectory);

                return new NetworkConfigurator(_logger).CreateIoSystem(controller, ioSystemName);
            }
            catch (Exception ex)
            {
                throw DecorateNetworkFailure(ex, controllerPath, backupDirectory, "CreateIoSystem");
            }
        }

        /// <summary>Attaches a device to an existing PROFINET IO system.</summary>
        /// <param name="devicePath">Full path to the IO device.</param>
        /// <param name="ioSystemName">The IO system to attach it to.</param>
        /// <param name="backupDirectory">Where the network layout is recorded before the change.</param>
        /// <exception cref="PortalException">
        /// The arguments are invalid, no project is open, the path does not resolve, the device has
        /// no IO connector, or the IO system is unknown.
        /// </exception>
        public void AssignDeviceToIoSystem(string devicePath, string ioSystemName, string backupDirectory)
        {
            _logger?.LogInformation("Attaching {Device} to IO system {IoSystem}...", devicePath, ioSystemName);

            try
            {
                var device = RequireDeviceItemForWrite(devicePath, backupDirectory);

                new NetworkConfigurator(_logger).AssignToIoSystem(device, ioSystemName);
            }
            catch (Exception ex)
            {
                throw DecorateNetworkFailure(ex, devicePath, backupDirectory, "AssignDeviceToIoSystem");
            }
        }

        private DeviceItem RequireDeviceItemForWrite(string devicePath, string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this rewires the project");
            }

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before changing the network");
            }

            var deviceItem = FindDeviceItem(devicePath)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"Device item not found: {devicePath}");

            NetworkTopologyWriter.Write(backupDirectory, new NetworkTopologyReader(_logger).Read(FindDevices()));

            return deviceItem;
        }

        private PortalException DecorateNetworkFailure(Exception ex, string devicePath, string backupDirectory, string operation)
        {
            var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"{operation} failed: {ex.Message}", null, ex);

            pex.Data["devicePath"] = devicePath;
            pex.Data["backupDirectory"] = backupDirectory;

            _logger?.LogError(pex, "{Operation} failed for {DevicePath}", operation, devicePath);

            return pex;
        }
    }
}
