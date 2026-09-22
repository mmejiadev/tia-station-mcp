using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// What a device is set to, as opposed to what it is made of: cycle, start-up behaviour,
    /// protection level and everything else Openness keeps in a device item's attribute bag.
    ///
    /// This is also what closes the gap <see cref="ModuleRemover"/> had to admit to: a module's
    /// parameters can now be read into something that outlives the project, so the backup taken
    /// before a module is unplugged contains them.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Reads the parameters of a device item that can be changed.</summary>
        /// <param name="deviceItemPath">The device item, for example <c>PLC_0</c>.</param>
        /// <returns>Its writable parameters, with the values they hold.</returns>
        /// <exception cref="PortalException">No project is open, or the path does not resolve.</exception>
        /// <remarks>
        /// The writable ones only. <c>GetDeviceItemInfo</c> prints the whole bag for looking
        /// around, and most of it describes the device rather than configures it; a caller aiming
        /// a write needs the subset it can actually aim at.
        /// </remarks>
        public IReadOnlyList<ObjectAttribute> GetDeviceParameters(string deviceItemPath)
        {
            _logger?.LogInformation("Reading the parameters of {DeviceItem}...", deviceItemPath);

            try
            {
                var deviceItem = RequireDeviceItem(deviceItemPath);

                return DeviceParameterConfigurator.ReadWritable(deviceItem);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Reading the parameters failed: {ex.Message}", null, ex);

                pex.Data["deviceItemPath"] = deviceItemPath;

                _logger?.LogError(pex, "GetDeviceParameters failed for {DeviceItemPath}", deviceItemPath);
                throw pex;
            }
        }

        /// <summary>Sets one parameter of a device item.</summary>
        /// <param name="deviceItemPath">The device item.</param>
        /// <param name="parameterName">The parameter, as GetDeviceParameters names it.</param>
        /// <param name="value">The value, as text; it is converted to the type already in place.</param>
        /// <param name="backupDirectory">Where the parameters are recorded first. Required.</param>
        /// <returns>The parameter as it stands afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// No project is open, the path does not resolve, the parameter does not exist or cannot be
        /// written, or TIA Portal refused the value.
        /// </exception>
        /// <remarks>
        /// The backup here is the item's own parameters rather than the module layout: the layout
        /// says what the station is made of and this changes what one of those things does, so
        /// recording the layout would again be a receipt for something else.
        /// </remarks>
        public ObjectAttribute SetDeviceParameter(string deviceItemPath, string parameterName, string value, string backupDirectory)
        {
            _logger?.LogInformation("Setting {Parameter} of {DeviceItem} to {Value}...", parameterName, deviceItemPath, value);

            try
            {
                if (string.IsNullOrWhiteSpace(backupDirectory))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this changes how the device behaves");
                }

                var deviceItem = RequireDeviceItem(deviceItemPath);

                RecordParameters(deviceItem, deviceItemPath, backupDirectory);

                return new DeviceParameterConfigurator(_logger).Set(deviceItem, parameterName, value);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"Setting the parameter failed: {ex.Message}", null, ex);

                pex.Data["deviceItemPath"] = deviceItemPath;
                pex.Data["parameterName"] = parameterName;
                pex.Data["backupDirectory"] = backupDirectory;

                _logger?.LogError(pex, "SetDeviceParameter failed for {DeviceItemPath}", deviceItemPath);
                throw pex;
            }
        }

        /// <summary>Records one device item's parameters under a backup root.</summary>
        /// <param name="deviceItem">The item, already resolved by the caller.</param>
        /// <param name="deviceItemPath">Its path, which names the record.</param>
        /// <param name="backupDirectory">Where to record them.</param>
        /// <remarks>
        /// Used by the parameter write and by the removal, which is the one operation that makes a
        /// parameter unreachable afterwards. It takes the item rather than looking it up again: a
        /// second lookup that found nothing used to return quietly, and a backup that silently was
        /// not taken is the failure this rule exists to prevent.
        /// </remarks>
        private static void RecordParameters(DeviceItem deviceItem, string deviceItemPath, string backupDirectory)
        {
            DeviceParameterWriter.Write(backupDirectory, deviceItemPath, EngineeringAttributeReader.Read(deviceItem));
        }

        private DeviceItem RequireDeviceItem(string deviceItemPath)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before reading or setting parameters");
            }

            return FindDeviceItem(deviceItemPath)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"Device item not found: {deviceItemPath}");
        }
    }
}
