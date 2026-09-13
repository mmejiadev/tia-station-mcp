using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Building the station itself: the slots of a rack, and plugging a module into one. Everything
    /// before this edited a station somebody else had built — a device could be created from an
    /// order number and nothing could be done to it afterwards.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Reads the slots of the rack a device item sits in.</summary>
        /// <param name="deviceItemPath">
        /// A device item in that rack, for example <c>PLC_0</c>. Its neighbours are the slots.
        /// </param>
        /// <returns>Every slot, free and occupied, in position order.</returns>
        /// <exception cref="PortalException">No project is open, or the path does not resolve.</exception>
        public IReadOnlyList<PlugLocationInfo> GetPlugLocations(string deviceItemPath)
        {
            _logger?.LogInformation("Reading the slots around {DeviceItem}...", deviceItemPath);

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project before reading a rack");
                }

                var deviceItem = FindDeviceItem(deviceItemPath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Device item not found: {deviceItemPath}");

                return new PlugLocationReader(_logger).Read(deviceItem);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Reading the rack failed: {ex.Message}", null, ex);

                pex.Data["deviceItemPath"] = deviceItemPath;

                _logger?.LogError(pex, "GetPlugLocations failed for {DeviceItemPath}", deviceItemPath);
                throw pex;
            }
        }

        /// <summary>Plugs a module into one slot of the rack a device item sits in.</summary>
        /// <param name="deviceItemPath">A device item in that rack, for example <c>PLC_0</c>.</param>
        /// <param name="module">What to plug, and where.</param>
        /// <param name="backupDirectory">Where the current layout is recorded first. Required.</param>
        /// <returns>The name the module ended up with.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the path does not resolve, the slot is taken by something else, or
        /// the rack does not accept the module there.
        /// </exception>
        /// <remarks>
        /// The slot and the order number both come from <see cref="GetPlugLocations"/>, which
        /// prints the free positions and the identifier of every module already in the rack. A
        /// write whose arguments cannot be obtained from a read is a write nobody can aim.
        ///
        /// The backup recorded here is the module layout, not the network table: a backup that does
        /// not contain the thing about to change is a receipt, not a record.
        /// </remarks>
        public string PlugModule(string deviceItemPath, ModuleToPlug module, string backupDirectory)
        {
            _logger?.LogInformation("Plugging a module into the rack holding {DeviceItem}...", deviceItemPath);

            try
            {
                var deviceItem = RequireDeviceItemForHardwareWrite(deviceItemPath, backupDirectory);

                return new ModulePlugger(_logger).Plug(deviceItem, module);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"Plugging the module failed: {ex.Message}", null, ex);

                pex.Data["deviceItemPath"] = deviceItemPath;
                pex.Data["backupDirectory"] = backupDirectory;

                _logger?.LogError(pex, "PlugModule failed for {DeviceItemPath}", deviceItemPath);
                throw pex;
            }
        }

        /// <summary>Unplugs a module from its rack.</summary>
        /// <param name="modulePath">The module, as GetPlugLocations names it.</param>
        /// <param name="backupDirectory">Where the layout is recorded first. Required.</param>
        /// <returns>What was removed, in the shape needed to plug it back.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the path does not resolve, the item is built in or is the CPU, or
        /// TIA Portal refused.
        /// </exception>
        /// <remarks>
        /// The one operation here that destroys something. What the backup restores and what it
        /// does not is stated in <see cref="ModuleRemover"/>, and the same record comes back to the
        /// caller so that putting the module back needs no file at all.
        /// </remarks>
        public ModuleInfo UnplugModule(string modulePath, string backupDirectory)
        {
            _logger?.LogInformation("Unplugging {Module}...", modulePath);

            try
            {
                var deviceItem = RequireDeviceItemForHardwareWrite(modulePath, backupDirectory);

                return new ModuleRemover(_logger).Remove(deviceItem, modulePath);
            }
            catch (Exception ex)
            {
                throw DecorateHardwareFailure(ex, modulePath, backupDirectory, "UnplugModule");
            }
        }

        /// <summary>Moves a module to another slot of the rack it is in.</summary>
        /// <param name="modulePath">The module, as GetPlugLocations names it.</param>
        /// <param name="positionNumber">The slot to move it to.</param>
        /// <param name="backupDirectory">Where the layout is recorded first. Required.</param>
        /// <returns>The name the module carries afterwards.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the path does not resolve, the slot is taken, or the rack will not
        /// take the module there.
        /// </exception>
        public string MoveModule(string modulePath, int positionNumber, string backupDirectory)
        {
            _logger?.LogInformation("Moving {Module} to slot {Slot}...", modulePath, positionNumber);

            try
            {
                var deviceItem = RequireDeviceItemForHardwareWrite(modulePath, backupDirectory);

                return new ModulePlugger(_logger).Move(deviceItem, modulePath, positionNumber);
            }
            catch (Exception ex)
            {
                throw DecorateHardwareFailure(ex, modulePath, backupDirectory, "MoveModule");
            }
        }

        /// <summary>Copies a module into a free slot of the rack it is in.</summary>
        /// <param name="modulePath">The module to copy, as GetPlugLocations names it.</param>
        /// <param name="positionNumber">The slot to copy it into.</param>
        /// <param name="backupDirectory">Where the layout is recorded first. Required.</param>
        /// <returns>The name TIA gave the copy, read back rather than chosen.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the path does not resolve, the slot is taken, or the rack will not
        /// take the copy there.
        /// </exception>
        public string CopyModule(string modulePath, int positionNumber, string backupDirectory)
        {
            _logger?.LogInformation("Copying {Module} into slot {Slot}...", modulePath, positionNumber);

            try
            {
                var deviceItem = RequireDeviceItemForHardwareWrite(modulePath, backupDirectory);

                return new ModulePlugger(_logger).Copy(deviceItem, modulePath, positionNumber);
            }
            catch (Exception ex)
            {
                throw DecorateHardwareFailure(ex, modulePath, backupDirectory, "CopyModule");
            }
        }

        /// <remarks>
        /// The single decoration point for the hardware writes, in the shape the error model asks
        /// for: context attached once, right before the rethrow, and never at the throw site.
        /// </remarks>
        private PortalException DecorateHardwareFailure(Exception ex, string modulePath, string backupDirectory, string operation)
        {
            var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"{operation} failed: {ex.Message}", null, ex);

            pex.Data["modulePath"] = modulePath;
            pex.Data["backupDirectory"] = backupDirectory;

            _logger?.LogError(pex, "{Operation} failed for {ModulePath}", operation, modulePath);

            return pex;
        }

        /// <summary>
        /// Adds the module layout to a program snapshot.
        /// </summary>
        /// <remarks>
        /// The third of the three things a snapshot has to hold. The program says what runs, the
        /// network table says where it talks, and this says what it runs on: the same blocks on a
        /// rack with a different input card are a different system, and until now nothing in the
        /// export would have shown it.
        /// </remarks>
        private SnapshotResult WithHardwareLayout(SnapshotResult program, string targetDirectory)
        {
            var exported = new List<string>(program.Exported)
            {
                HardwareLayoutWriter.Write(targetDirectory, new HardwareLayoutReader(_logger).Read(FindDevices()))
            };

            return new SnapshotResult(exported, program.Inconsistent, program.Unsupported, program.Failed);
        }

        /// <summary>
        /// The device item a hardware write is aimed at, with the layout recorded before anything
        /// changes.
        /// </summary>
        private DeviceItem RequireDeviceItemForHardwareWrite(string deviceItemPath, string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this changes what the station is made of");
            }

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before changing the hardware");
            }

            var deviceItem = FindDeviceItem(deviceItemPath)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"Device item not found: {deviceItemPath}");

            HardwareLayoutWriter.Write(backupDirectory, new HardwareLayoutReader(_logger).Read(FindDevices()));

            return deviceItem;
        }
    }
}
