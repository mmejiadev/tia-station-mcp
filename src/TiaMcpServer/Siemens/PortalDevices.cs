using Microsoft.Extensions.Logging;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;
using System.Text;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Devices and device items: what the project contains as hardware.
    /// </remarks>
    public partial class Portal
    {
        public string GetProjectTree()
        {
            _logger?.LogInformation("Getting project tree...");

            if (IsProjectNull())
            {
                return string.Empty;
            }

            StringBuilder sb = new();

            sb.AppendLine($"{_project?.Name}");

            var ancestorStates = new List<bool>();
            var sections = new List<Action>();
            
            if (_project?.Devices != null && _project.Devices.Count > 0)
            {
                sections.Add(() => GetProjectTreeDevices(sb, _project.Devices, ancestorStates));
            }
            
            if (_project?.DeviceGroups != null && _project.DeviceGroups.Count > 0)
            {
                sections.Add(() => GetProjectTreeGroups(sb, _project.DeviceGroups, ancestorStates));
            }
            
            if (_project?.UngroupedDevicesGroup != null)
            {
                sections.Add(() => GetProjectTreeUngroupedDeviceGroup(sb, _project.UngroupedDevicesGroup, ancestorStates));
            }
            
            for (int i = 0; i < sections.Count; i++)
            {
                var isLastSection = i == sections.Count - 1;
                if (i == 0)
                {
                    sections[i]();
                }
                else
                {
                    sections[i]();
                }
            }

            return sb.ToString();
        }

        

        private List<Device> FindDevices(string regexName = "")
        {
            _logger?.LogInformation("Getting devices...");

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<Device>();

            if (_project?.Devices != null)
            {
                foreach (Device device in _project.Devices)
                {
                    list.Add(device);
                }

                foreach (var group in _project.DeviceGroups)
                {
                    GetDevicesRecursive(group, list, regexName);
                }

                //foreach (var group in _project.UngroupedDevicesGroup)
                //{
                //    GetDevicesRecursive(_project.UngroupedDevicesGroup, list, regexName);
                //}
            }

            return list;
        }

        /// <summary>Describes the devices of the open project, filtered by name.</summary>
        /// <param name="regexName">The name filter, or empty for every device.</param>
        /// <returns>One description per matching device.</returns>
        /// <exception cref="PortalException">The filter is not a valid expression.</exception>
        public IReadOnlyList<ObjectDescription> GetDevices(string regexName = "")
        {
            var described = new List<ObjectDescription>();

            foreach (var device in FindDevices(regexName))
            {
                described.Add(ObjectDescriber.Describe(device, device.Name));
            }

            return described;
        }

        /// <summary>Describes one device.</summary>
        /// <param name="devicePath">Full path to the device in the project structure.</param>
        /// <returns>The description, or null when there is no such device.</returns>
        public ObjectDescription? GetDevice(string devicePath)
        {
            var device = FindDevice(devicePath);

            return device == null ? null : ObjectDescriber.Describe(device, device.Name);
        }

        /// <summary>Describes one device item.</summary>
        /// <param name="deviceItemPath">Full path to the device item in the project structure.</param>
        /// <returns>The description, or null when there is no such device item.</returns>
        public ObjectDescription? GetDeviceItem(string deviceItemPath)
        {
            var deviceItem = FindDeviceItem(deviceItemPath);

            return deviceItem == null ? null : ObjectDescriber.Describe(deviceItem, deviceItem.Name);
        }

        private Device? FindDevice(string devicePath)
        {
            _logger?.LogInformation($"Getting device by path: {devicePath}");

            if (IsProjectNull())
            {
                return null;
            }

            // Retrieve the device by its path
            return GetDeviceByPath(devicePath);
        }

        private DeviceItem? FindDeviceItem(string deviceItemPath)
        {
            _logger?.LogInformation($"Getting device item by path: {deviceItemPath}");

            if (IsProjectNull())
            {
                return null;
            }

            // Retrieve the device by its path
            return GetDeviceItemByPath(deviceItemPath);

        }

        /// <summary>Adds a device to the open project.</summary>
        /// <param name="typeIdentifier">
        /// What to create, as Openness names it, for example
        /// <c>OrderNumber:6ES7 511-1AK02-0AB0/V3.1</c>.
        /// </param>
        /// <param name="deviceName">Name for the device, for example <c>PLC_1</c>.</param>
        /// <returns>The name the device ended up with.</returns>
        /// <exception cref="PortalException">No project is open, or the identifier is not known to TIA.</exception>
        public string AddDevice(string typeIdentifier, string deviceName)
        {
            _logger?.LogInformation("Adding device {DeviceName} ({TypeIdentifier})...", deviceName, typeIdentifier);

            try
            {
                if (string.IsNullOrWhiteSpace(typeIdentifier) || string.IsNullOrWhiteSpace(deviceName))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "typeIdentifier and deviceName are required");
                }

                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open or create a project first");
                }

                // Three arguments, not two: the device gets a name and so does the item inside it.
                // Create() without an item makes an empty station with no CPU in it.
                var device = _project!.Devices.CreateWithItem(typeIdentifier, deviceName, deviceName);

                return device.Name;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.InvalidParams, $"Adding the device failed: {ex.Message}", null, ex);

                pex.Data["typeIdentifier"] = typeIdentifier;
                pex.Data["deviceName"] = deviceName;

                _logger?.LogError(pex, "AddDevice failed for {DeviceName} ({TypeIdentifier})", deviceName, typeIdentifier);
                throw pex;
            }
        }

        /// <summary>
        /// Compiles a device's hardware configuration.
        /// </summary>
        /// <param name="deviceItemPath">Full path to the CPU, for example <c>PLC_0</c>.</param>
        /// <returns>What the compile reported, in the same shape as a software compile.</returns>
        /// <remarks>
        /// <see cref="CompileSoftware"/> compiles the program and nothing else, so a change that
        /// invalidates the hardware configuration leaves a stale one behind. Downloading that
        /// produces "Loading of hardware configuration failed (0013 -32 0 0)", which names neither
        /// the cause nor the fix.
        ///
        /// <see cref="EnableSimulationSupport"/> is exactly such a change, which is why a download
        /// that had been writing the hardware configuration successfully started failing on it the
        /// moment simulation support was turned on.
        /// </remarks>
        /// <exception cref="PortalException">No project is open, or the path does not resolve.</exception>
        public CompilationReport CompileHardware(string deviceItemPath)
        {
            _logger?.LogInformation("Compiling hardware for {DeviceItemPath}...", deviceItemPath);

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Open a project before compiling");
                }

                var deviceItem = FindDeviceItem(deviceItemPath)
                    ?? throw new PortalException(PortalErrorCode.NotFound, $"Device item not found: {deviceItemPath}");

                var compilable = deviceItem.GetService<ICompilable>()
                    ?? throw new PortalException(PortalErrorCode.InvalidState, $"'{deviceItemPath}' cannot be compiled");

                return CompilerResultReader.Read(compilable.Compile());
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.CompileFailed, $"Hardware compile failed: {ex.Message}", null, ex);

                pex.Data["deviceItemPath"] = deviceItemPath;

                _logger?.LogError(pex, "CompileHardware failed for {DeviceItemPath}", deviceItemPath);
                throw pex;
            }
        }
    }
}
