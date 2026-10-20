using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using System.Collections.Generic;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Compiling a WinCC Unified HMI, which TIA Portal only does through the station that holds it.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>
        /// Names what compiling the software at a path really compiles, so the write policy can be
        /// asked about that and not about something smaller.
        /// </summary>
        /// <param name="softwarePath">The path the caller asked to compile.</param>
        /// <returns>
        /// The path itself for a PLC program or a classic WinCC HMI; for a WinCC Unified HMI, the name
        /// of the station that holds it, because the whole station is compiled, hardware included.
        /// </returns>
        /// <exception cref="PortalException">
        /// <see cref="PortalErrorCode.InvalidState"/> when a Unified HMI's station also holds a PLC
        /// program; see <see cref="UnifiedStationCompileRule"/>. Thrown before any plan exists, so it
        /// is reported as bad input to fix, not as a failed operation to retry.
        /// </exception>
        public string ResolveCompileTarget(string softwarePath)
        {
            if (IsProjectNull() || GetSoftwareContainer(softwarePath) is not { Software: UnifiedHmiSoftware unified } container)
            {
                return softwarePath;
            }

            return RequireCompilableStation(container.Parent, unified.Name).Name;
        }

        /// <remarks>
        /// Measured 2026-10-17 on TIA Portal V20: Unified software offers no services at all and the
        /// device item holding it has no <see cref="ICompilable"/>. Only the device does, so a Unified
        /// HMI is compiled with the whole station around it, hardware included. That is also the only
        /// way to read its errors: TIA Portal's Info tab showed "Compiling aborted" with no text for
        /// errors this returns in full.
        ///
        /// The station rule is checked again here although <see cref="ResolveCompileTarget"/> checked
        /// it before the plan: the project can change between the two.
        /// </remarks>
        private static ICompilable FindUnifiedStationCompilable(IEngineeringObject? holder, string hmiName)
        {
            var device = RequireCompilableStation(holder, hmiName);

            return device.GetService<ICompilable>()
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"Station '{device.Name}' offers no compile service");
        }

        private static Device RequireCompilableStation(IEngineeringObject? holder, string hmiName)
        {
            var device = FindOwningDevice(holder)
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"WinCC Unified HMI '{hmiName}' is not inside a device, so it cannot be compiled");

            var programs = new List<string>();
            CollectPlcPrograms(device, programs, new HashSet<HardwareObject>());

            UnifiedStationCompileRule.RequireNoPlcPrograms(hmiName, device.Name, programs);

            return device;
        }

        private static Device? FindOwningDevice(IEngineeringObject? engineeringObject)
        {
            var current = engineeringObject;

            while (current != null && current is not Device)
            {
                current = current.Parent;
            }

            return current as Device;
        }

        /// <remarks>
        /// Both <c>DeviceItems</c> and <c>Items</c>: rack occupants show up under <c>Items</c> and not
        /// <c>DeviceItems</c> (measured 2026-09-13), and a program missed here would be compiled
        /// behind the policy's back — so the walk errs on visiting too much.
        /// </remarks>
        private static void CollectPlcPrograms(HardwareObject hardware, List<string> programs, HashSet<HardwareObject> visited)
        {
            if (!visited.Add(hardware))
            {
                return;
            }

            if (hardware is DeviceItem deviceItem && deviceItem.GetService<SoftwareContainer>()?.Software is PlcSoftware plcSoftware)
            {
                programs.Add(plcSoftware.Name);
            }

            foreach (var child in ChildrenOf(hardware))
            {
                CollectPlcPrograms(child, programs, visited);
            }
        }

        private static IEnumerable<HardwareObject> ChildrenOf(HardwareObject hardware)
        {
            foreach (var item in hardware.DeviceItems)
            {
                yield return item;
            }

            foreach (var item in hardware.Items)
            {
                yield return item;
            }
        }
    }
}
