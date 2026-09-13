using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Where a module's data lands in the process image. This is the half of plugging a card that
    /// the program can see: a tag bound to <c>%I0.0</c> reads whichever module starts at byte 0,
    /// so a card whose address nobody set is a card addressed by luck.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Reads the address ranges of every module in the rack a device item sits in.</summary>
        /// <param name="deviceItemPath">A device item in that rack, for example <c>PLC_0</c>.</param>
        /// <returns>One entry per range, module by module.</returns>
        /// <exception cref="PortalException">No project is open, or the path does not resolve.</exception>
        /// <remarks>
        /// The whole rack rather than one module, because the question this answers is never about
        /// one card. "Where can this one go" is answered by what the others already occupy, and a
        /// read that showed a single module would need calling once per slot to say anything.
        /// </remarks>
        public IReadOnlyList<IoAddressInfo> GetIoAddresses(string deviceItemPath)
        {
            _logger?.LogInformation("Reading the addresses around {DeviceItem}...", deviceItemPath);

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project before reading addresses");
                }

                var deviceItem = FindDeviceItem(deviceItemPath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Device item not found: {deviceItemPath}");

                return RackAddresses(deviceItem, deviceItemPath);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Reading the addresses failed: {ex.Message}", null, ex);

                pex.Data["deviceItemPath"] = deviceItemPath;

                _logger?.LogError(pex, "GetIoAddresses failed for {DeviceItemPath}", deviceItemPath);
                throw pex;
            }
        }

        /// <summary>Moves one address range of a module to another start byte.</summary>
        /// <param name="modulePath">The module, as GetIoAddresses names it.</param>
        /// <param name="ioType"><c>Input</c> or <c>Output</c>.</param>
        /// <param name="startAddress">The byte the range should start at.</param>
        /// <param name="backupDirectory">Where the current layout is recorded first. Required.</param>
        /// <returns>The range as it stands afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the path does not resolve, the module has no such range, or TIA
        /// Portal refused the address.
        /// </exception>
        public IoAddressInfo SetModuleAddress(string modulePath, string ioType, int startAddress, string backupDirectory)
        {
            _logger?.LogInformation("Moving the {IoType} range of {Module} to {Start}...", ioType, modulePath, startAddress);

            try
            {
                var deviceItem = RequireDeviceItemForHardwareWrite(modulePath, backupDirectory);

                return new IoAddressConfigurator(_logger).Move(deviceItem, modulePath, ioType, startAddress);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"Setting the address failed: {ex.Message}", null, ex);

                pex.Data["modulePath"] = modulePath;
                pex.Data["backupDirectory"] = backupDirectory;

                _logger?.LogError(pex, "SetModuleAddress failed for {ModulePath}", modulePath);
                throw pex;
            }
        }

        /// <summary>The address ranges of everything plugged into the rack an item sits in.</summary>
        /// <remarks>
        /// Aimed the way <see cref="GetPlugLocations"/> is, and through the same collection — see
        /// <c>PlugLocationReader.Occupied</c> for why it is <c>Items</c> and not <c>DeviceItems</c>.
        /// The two reads describe one rack, so they enumerate it once between them.
        /// </remarks>
        private static List<IoAddressInfo> RackAddresses(DeviceItem anchor, string anchorPath)
        {
            var container = PlugLocationReader.RequireContainer(anchor);
            var rackPath = ProjectPath.Parse(anchorPath).Parent;

            return container.Items
                .SelectMany(module => IoAddressReader.Read(module, ProjectPath.Join(rackPath, module.Name)))
                .ToList();
        }
    }
}
