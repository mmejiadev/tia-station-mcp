using Siemens.Engineering.HW;
using System;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Devices created inside a device group at the top of the project, which is what a device group template is made
    /// from. Measured 2026-11-24 on TIA Portal V21: <c>DeviceUserGroupComposition.Create</c> makes the group and
    /// <c>DeviceUserGroup.Devices.CreateWithItem</c> creates a CPU or a PC station in it, plugged afterwards as one at the
    /// top. Openness has no call that moves an existing device into a group, so the device is created there.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Creates a device in the project, or in a device group at its top, made when it is not there.</summary>
        /// <param name="groupName">The group, or empty for the top of the project.</param>
        /// <param name="create">What creates the device, given where it goes.</param>
        /// <returns>The device created.</returns>
        /// <exception cref="PortalException">The group name holds a separator or is a device's name; or what <paramref name="create"/> throws.</exception>
        /// <remarks>
        /// A group this call made is removed again when the creation fails, so a refused device leaves nothing; a group
        /// that was already there is left as it was. The creation's own failure is rethrown unchanged, so a device refused
        /// in a new group is reported exactly as one refused at the top of the project (an unknown order number is
        /// <see cref="PortalErrorCode.InvalidParams"/> in both).
        /// </remarks>
        private Device CreateInDeviceGroup(string groupName, Func<DeviceComposition, Device> create)
        {
            var group = (groupName ?? string.Empty).Trim();

            if (group.Length == 0)
            {
                return create(CurrentProject!.Devices);
            }

            RequireUsableGroupName(group);

            var existing = CurrentProject!.DeviceGroups.FirstOrDefault(held => string.Equals(held.Name, group, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                return create(existing.Devices);
            }

            var made = CurrentProject.DeviceGroups.Create(group);

            try
            {
                return create(made.Devices);
            }
            catch (Exception failure)
            {
                RemoveGroupAfter(made, failure);
                throw;
            }
        }

        /// <remarks>
        /// A group named like a device would make <c>PLC_1/PLC_1/PLC_1</c> paths the lookup reads two ways, so it is
        /// refused; the separator is <see cref="StationNameRule.RequireGroupName"/>'s to refuse.
        /// </remarks>
        private void RequireUsableGroupName(string group)
        {
            StationNameRule.RequireGroupName(group);

            if (IsDeviceNameTaken(group))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{group}' is the name of a device or device item; give the group another name");
            }
        }

        /// <remarks>
        /// As <see cref="RemoveStationAfter"/>: the creation's failure is the one that explains, so it is only replaced
        /// when the group cannot be removed either, and then the delete's failure is named beside it.
        /// </remarks>
        private static void RemoveGroupAfter(DeviceUserGroup group, Exception failure)
        {
            var groupName = group.Name;

            try
            {
                group.Delete();
            }
            catch (Exception deleteFailure)
            {
                throw new PortalException(
                    PortalErrorCode.WriteFailed,
                    $"{failure.Message} Removing the device group '{groupName}' made for it failed too ({deleteFailure.Message}); delete it in TIA Portal.",
                    null,
                    new AggregateException(failure, deleteFailure));
            }
        }
    }
}
