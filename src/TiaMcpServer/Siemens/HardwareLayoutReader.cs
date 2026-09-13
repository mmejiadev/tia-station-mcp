using Microsoft.Extensions.Logging;
using Siemens.Engineering.HW;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads what every station in a project is built from: each module, its slot and its type.
    /// </summary>
    /// <remarks>
    /// The hardware counterpart of <see cref="NetworkTopologyReader"/>, and it walks the same tree
    /// for the same reason: device items nest, a rack holds a CPU which holds interfaces, and only
    /// the whole walk describes the station.
    ///
    /// Built-in items are recorded rather than filtered out. They cannot be unplugged, which makes
    /// them exactly the rows a caller needs in order to understand why a slot refuses everything.
    /// </remarks>
    public sealed class HardwareLayoutReader
    {
        private readonly ILogger? _logger;

        /// <summary>Creates a hardware layout reader.</summary>
        /// <param name="logger">Optional logger.</param>
        public HardwareLayoutReader(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Reads every module of a set of devices.</summary>
        /// <param name="devices">The project's devices.</param>
        /// <returns>One entry per device item, built-in or plugged.</returns>
        public IReadOnlyList<ModuleInfo> Read(IEnumerable<Device> devices)
        {
            if (devices == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "devices is required");
            }

            var modules = new List<ModuleInfo>();

            foreach (var device in devices)
            {
                var root = ProjectPath.AddressableDeviceName(device.Name);

                foreach (var deviceItem in device.DeviceItems)
                {
                    Collect(deviceItem, root, modules);
                }
            }

            _logger?.LogInformation("Hardware layout: {Count} module(s) found", modules.Count);

            return modules;
        }

        private static void Collect(DeviceItem deviceItem, string parentPath, List<ModuleInfo> modules)
        {
            var path = ProjectPath.Join(parentPath, deviceItem.Name);

            modules.Add(new ModuleInfo(
                path,
                deviceItem.PositionNumber,
                deviceItem.TypeIdentifier ?? string.Empty,
                deviceItem.IsBuiltIn));

            foreach (var nested in deviceItem.DeviceItems)
            {
                Collect(nested, path, modules);
            }
        }
    }
}
