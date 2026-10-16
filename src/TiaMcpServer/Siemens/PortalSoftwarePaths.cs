using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The paths the software of a project answers to, written so they can be handed straight back:
    /// <c>Group/Device/Item</c>.
    ///
    /// Needed because the software of a panel does not sit where its name suggests. A panel added as
    /// <c>HMI_1</c> keeps its HMI target on a device item TIA Portal names and numbers itself —
    /// <c>HMI_RT_2</c> when the project already had a panel (measured 2026-10-14) — so the bare device
    /// name finds nothing, and nobody can guess the number.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>The paths of every piece of software in a device, or in the project.</summary>
        /// <param name="deviceName">The device's name, or empty for every device.</param>
        /// <returns>
        /// One <c>Group/Device/Item</c> path per software container, PLC program or HMI target alike.
        /// Empty when no project is open.
        /// </returns>
        public IReadOnlyList<string> GetSoftwarePaths(string deviceName = "")
        {
            return SoftwareEntries()
                .Where(entry => deviceName.Length == 0 || entry.Device.Name.Equals(deviceName, System.StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Path)
                .ToList();
        }

        /// <summary>The paths of every classic WinCC HMI target in the project.</summary>
        /// <returns>One <c>Group/Device/Item</c> path per HMI target; empty when no project is open.</returns>
        public IReadOnlyList<string> GetHmiTargetPaths()
        {
            return SoftwareEntries()
                .Where(entry => entry.Software is global::Siemens.Engineering.Hmi.HmiTarget)
                .Select(entry => entry.Path)
                .ToList();
        }

        /// <remarks>
        /// The name a new device takes is also the name of the item inside it, so both are checked, in
        /// every group: a name already used anywhere would be renamed by TIA Portal without a word, or
        /// make a path that resolves to the first of two.
        /// </remarks>
        private bool IsDeviceNameTaken(string name)
        {
            return AllDevices().Any(device => IsSameName(device.Name, name) || device.DeviceItems.Any(item => IsSameName(item.Name, name)));
        }

        private IEnumerable<Device> AllDevices()
        {
            if (CurrentProject == null)
            {
                return Enumerable.Empty<Device>();
            }

            // Ungrouped devices — a distributed I/O station not yet assigned — are neither in Devices
            // nor in a user group, and a name they hold is as taken as any other.
            return CurrentProject.Devices
                .Concat(CurrentProject.DeviceGroups.SelectMany(DevicesIn))
                .Concat(CurrentProject.UngroupedDevicesGroup?.Devices ?? Enumerable.Empty<Device>());
        }

        private static IEnumerable<Device> DevicesIn(DeviceUserGroup group)
        {
            return group.Devices.Concat(group.Groups.SelectMany(DevicesIn));
        }

        private static bool IsSameName(string left, string right)
        {
            return string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<SoftwareEntry> SoftwareEntries()
        {
            if (CurrentProject == null)
            {
                return Enumerable.Empty<SoftwareEntry>();
            }

            return EntriesIn(CurrentProject.Devices, string.Empty)
                .Concat(CurrentProject.DeviceGroups.SelectMany(group => EntriesInGroup(group, string.Empty)))
                .ToList();
        }

        private static IEnumerable<SoftwareEntry> EntriesInGroup(DeviceUserGroup group, string prefix)
        {
            var groupPrefix = $"{prefix}{group.Name}/";

            return EntriesIn(group.Devices, groupPrefix)
                .Concat(group.Groups.SelectMany(subgroup => EntriesInGroup(subgroup, groupPrefix)));
        }

        /// <remarks>
        /// Only the device's own items, one level down: that is as deep as the path lookup in
        /// <c>GetSoftwareContainer</c> reads, so a path listed here is one it resolves.
        /// </remarks>
        private static IEnumerable<SoftwareEntry> EntriesIn(DeviceComposition devices, string prefix)
        {
            foreach (var device in devices)
            {
                foreach (var item in device.DeviceItems)
                {
                    var software = item.GetService<SoftwareContainer>()?.Software;

                    if (software != null)
                    {
                        yield return new SoftwareEntry(device, $"{prefix}{AddressableSegment(device)}{item.Name}", software);
                    }
                }
            }
        }

        /// <remarks>
        /// A hardware station's name can hold the path separator, <c>S7-1500/ET200MP-Station_1</c>, and
        /// then it cannot be a segment: the item is written without it, which the lookup also accepts.
        /// </remarks>
        private static string AddressableSegment(Device device)
        {
            var name = ProjectPath.AddressableDeviceName(device.Name);

            return name.Length == 0 ? string.Empty : name + "/";
        }

        private sealed class SoftwareEntry
        {
            public SoftwareEntry(Device device, string path, Software software)
            {
                Device = device;
                Path = path;
                Software = software;
            }

            public Device Device { get; }

            public string Path { get; }

            public Software Software { get; }
        }
    }
}
