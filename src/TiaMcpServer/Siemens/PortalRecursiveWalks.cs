using Siemens.Engineering.HW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Walks a device, block or type hierarchy, collecting what matches a name filter.
    /// </remarks>
    public partial class Portal
    {
        private bool GetDevicesRecursive(DeviceUserGroup group, List<Device> list, string regexName = "")
        {
            var anySuccess = false;

            // Parsed once for the whole group rather than per item, and refused rather than
            // caught: an invalid filter used to skip every entry silently, which returned a short
            // list that looked complete. See NameFilter.
            var filter = NameFilter.Parse(regexName);

            foreach (var composition in group.Devices)
            {
                if (composition is Device device)
                {
                    if (!filter.Matches(device.Name))
                    {
                        continue;
                    }

                    list.Add(device);

                    anySuccess = true;
                }
            }

            foreach (var subgroup in group.Groups)
            {
                anySuccess = GetDevicesRecursive(subgroup, list, regexName);
            }

            return anySuccess;
        }

        private bool GetBlocksRecursive(PlcBlockGroup group, List<PlcBlock> list, string regexName = "")
        {
            var anySuccess = false;

            // Parsed once for the whole group rather than per item, and refused rather than
            // caught: an invalid filter used to skip every entry silently, which returned a short
            // list that looked complete. See NameFilter.
            var filter = NameFilter.Parse(regexName);

            foreach (var composition in group.Blocks)
            {
                if (composition is PlcBlock block)
                {
                    if (!filter.Matches(block.Name))
                    {
                        continue;
                    }

                    list.Add(block);

                    anySuccess = true;
                }
            }

            foreach (var subgroup in group.Groups)
            {
                anySuccess = GetBlocksRecursive(subgroup, list, regexName);
            }

            return anySuccess;
        }

        private bool GetTypesRecursive(PlcTypeGroup group, List<PlcType> list, string regexName = "")
        {
            var anySuccess = false;

            // Parsed once for the whole group rather than per item, and refused rather than
            // caught: an invalid filter used to skip every entry silently, which returned a short
            // list that looked complete. See NameFilter.
            var filter = NameFilter.Parse(regexName);

            foreach (var composition in group.Types)
            {
                if (composition is PlcType type)
                {
                    if (!filter.Matches(type.Name))
                    {
                        continue;
                    }

                    list.Add(type);

                    anySuccess = true;
                }

            }

            foreach (PlcTypeGroup subgroup in group.Groups)
            {
                anySuccess = GetTypesRecursive(subgroup, list, regexName);
            }

            return anySuccess;
        }
    }
}
