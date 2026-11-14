using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// A PLC left the way the class uses it, without passwords. See <see cref="PlcAccessOpener"/> for what was
    /// measured: on TIA Portal V21 a new S7-1500 does not compile until this is done or passwords are set; and
    /// <see cref="AnonymousUserAccess"/> for what an S7-1200, whose access goes through users and roles, needs as well.
    /// </remarks>
    public partial class Portal
    {
        private const string ConfidentialDataRecord = "ConfidentialDataProtection";
        private const string AccessLevelRecord = "AccessLevel";
        private const string RecordedNotWritable = "Recorded";

        /// <summary>Leaves every PLC in a device item at full access with its confidential data not protected.</summary>
        /// <param name="deviceItemPath">The PLC's device item, for example <c>PLC_1/PLC_1</c>, or an item holding PLCs.</param>
        /// <param name="backupDirectory">Where each PLC's parameters and protection are recorded first. Required.</param>
        /// <returns>Each PLC's protection before and after, and the anonymous user's where one needed it.</returns>
        /// <exception cref="PortalException">
        /// No project is open, no backup directory, the path does not resolve or holds no PLC, a PLC's confidential
        /// data has a password or its protection is one this does not open, or TIA Portal refused.
        /// </exception>
        /// <remarks>
        /// Every PLC is checked before any is changed, so a refusal leaves all of them as they were. TIA Portal failing
        /// part-way cannot be undone that way, so the failure names the PLCs already changed.
        /// </remarks>
        public PlcAccessReport SetPlcFullAccess(string deviceItemPath, string backupDirectory)
        {
            _logger?.LogInformation("Leaving the PLCs of {DeviceItemPath} at full access...", deviceItemPath);

            try
            {
                if (string.IsNullOrWhiteSpace(backupDirectory))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this changes how the PLC is protected");
                }

                var item = RequireDeviceItem(deviceItemPath);
                var plcs = RequirePlcs(item, deviceItemPath);

                foreach (var plc in plcs)
                {
                    RecordProtection(plc, PlcPathFor(deviceItemPath, item, plc), backupDirectory);
                }

                return OpenWithUsers(plcs, backupDirectory);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"Leaving the PLC at full access failed: {ex.Message}", null, ex);

                pex.Data["deviceItemPath"] = deviceItemPath;
                pex.Data["backupDirectory"] = backupDirectory;

                _logger?.LogError(pex, "SetPlcFullAccess failed for {DeviceItemPath}", deviceItemPath);
                throw pex;
            }
        }

        /// <summary>Leaves the PLCs of a station just added at full access, as <see cref="SetPlcFullAccess"/> does.</summary>
        /// <param name="deviceName">The station <see cref="AddDevice"/> just created.</param>
        /// <param name="backupDirectory">Where the anonymous user's state is recorded, if it is changed. Required.</param>
        /// <returns>Each PLC's protection before and after, empty for a station with no PLC, and the anonymous user's.</returns>
        /// <exception cref="PortalException">No project is open, the station is not there, or TIA Portal refused.</exception>
        /// <remarks>
        /// The station did not exist a moment ago, so there is no earlier state of it to keep; the anonymous user did,
        /// and belongs to the whole project, so its state is recorded before it is changed.
        /// </remarks>
        public PlcAccessReport SetFullAccessOfAddedDevice(string deviceName, string backupDirectory)
        {
            _logger?.LogInformation("Leaving the PLCs of the new station {DeviceName} at full access...", deviceName);

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project first");
                }

                var device = CurrentProject!.Devices.Find(deviceName)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Station not found: {deviceName}");

                var plcs = AllDeviceItems(device.DeviceItems).Where(PlcAccessOpener.IsPlc).ToList();

                // All checked before any is changed, as SetPlcFullAccess does: a station must not be left half open.
                plcs.ForEach(PlcAccessOpener.RequireOpenable);

                return OpenWithUsers(plcs, backupDirectory);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"Leaving the new PLC at full access failed: {ex.Message}", null, ex);

                pex.Data["deviceName"] = deviceName;

                _logger?.LogError(pex, "SetFullAccessOfAddedDevice failed for {DeviceName}", deviceName);
                throw pex;
            }
        }

        /// <remarks>
        /// Checked for every PLC before any is opened: <see cref="PlcAccessOpener.Open"/> refuses one it does not open,
        /// and finding that on the second PLC after changing the first would leave the station half done.
        /// </remarks>
        private static List<DeviceItem> RequirePlcs(DeviceItem deviceItem, string deviceItemPath)
        {
            var plcs = AllDeviceItems(new[] { deviceItem }).Where(PlcAccessOpener.IsPlc).ToList();

            if (plcs.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{deviceItemPath}' holds no PLC with protection settings");
            }

            foreach (var plc in plcs)
            {
                PlcAccessOpener.RequireOpenable(plc);
            }

            return plcs;
        }

        /// <remarks>
        /// The item's own parameters with its protection added, in the same file: neither setting is in the
        /// attribute bag, so without these two lines the record would not hold what is about to change.
        /// </remarks>
        private static void RecordProtection(DeviceItem plc, string plcPath, string backupDirectory)
        {
            var protection = PlcAccessOpener.Read(plc);
            var record = EngineeringAttributeReader.Read(plc).ToList();

            record.Add(new ObjectAttribute(ConfidentialDataRecord, protection.ConfidentialData, RecordedNotWritable));
            record.Add(new ObjectAttribute(AccessLevelRecord, protection.AccessLevel, RecordedNotWritable));

            DeviceParameterWriter.Write(backupDirectory, plcPath, record);
        }

        /// <remarks>
        /// A PLC with no access level goes through users and roles, and compiles only once some user has full access:
        /// the anonymous user is given it then, and only then. What that needs is checked before any PLC is opened,
        /// so a project that cannot give it leaves every PLC as it was.
        /// </remarks>
        private PlcAccessReport OpenWithUsers(IReadOnlyList<DeviceItem> plcs, string backupDirectory)
        {
            var anonymous = RequireAnonymousUserIfNeeded(plcs, backupDirectory);
            var changes = OpenEach(plcs);

            if (anonymous == null)
            {
                return new PlcAccessReport(changes, string.Empty);
            }

            try
            {
                return new PlcAccessReport(changes, anonymous.GrantFullAccess(backupDirectory));
            }
            catch (Exception ex)
            {
                throw new PortalException(
                    (ex as PortalException)?.Code ?? PortalErrorCode.WriteFailed,
                    $"Giving the project's Anonymous user full access failed ({ex.Message}); {string.Join(", ", changes.Select(change => change.PlcName))} had already been left without passwords.",
                    null,
                    ex);
            }
        }

        private AnonymousUserAccess? RequireAnonymousUserIfNeeded(IReadOnlyList<DeviceItem> plcs, string backupDirectory)
        {
            if (!plcs.Any(PlcAccessOpener.GoesThroughUsers))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: the project's anonymous user is about to change");
            }

            return AnonymousUserAccess.Require(CurrentProject!);
        }

        private static List<PlcAccessChange> OpenEach(IReadOnlyList<DeviceItem> plcs)
        {
            var changes = new List<PlcAccessChange>();

            foreach (var plc in plcs)
            {
                try
                {
                    changes.Add(PlcAccessOpener.Open(plc));
                }
                catch (Exception ex) when (changes.Count > 0)
                {
                    var changed = string.Join(", ", changes.Select(change => change.PlcName));

                    throw new PortalException(
                        (ex as PortalException)?.Code ?? PortalErrorCode.WriteFailed,
                        $"Leaving '{plc.Name}' at full access failed ({ex.Message}); {changed} had already been left without passwords.",
                        null,
                        ex);
                }
            }

            return changes;
        }

        /// <remarks>
        /// The record is named after the PLC's whole path below the item asked for, so two PLCs of the same name in
        /// different stations of one item do not write the same file. <c>Equals</c>, which Openness objects override, and
        /// not reference identity: one proxy per object is not promised. If it did not match, the walk would stop at the
        /// station and the name would only be longer, never shared.
        /// </remarks>
        private static string PlcPathFor(string deviceItemPath, DeviceItem item, DeviceItem plc)
        {
            var below = new List<string>();

            for (var current = plc; current != null && !current.Equals(item); current = current.Parent as DeviceItem)
            {
                below.Insert(0, current.Name);
            }

            return below.Count == 0 ? deviceItemPath : deviceItemPath + "/" + string.Join("/", below);
        }
    }
}
