using Microsoft.Extensions.Logging;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.OpcUa;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Lists and exports the OPC UA server interfaces a CPU publishes.
    /// </summary>
    /// <remarks>
    /// Reached through <c>PlcSoftware.GetService&lt;OpcUaProvider&gt;()</c> and then
    /// <c>CommunicationGroup.ServerInterfaceGroup.ServerInterfaces</c>. Worth writing down,
    /// because the obvious candidate is wrong: <c>HW.Utilities.OpcUaExportProvider</c> exists,
    /// looks exactly right, and is unreachable — nothing public returns one and it has no
    /// accessible constructor.
    /// </remarks>
    public sealed class OpcUaInterfaceExporter
    {
        private readonly PlcSoftware _software;
        private readonly ILogger? _logger;

        /// <summary>Creates an exporter for one PLC software container.</summary>
        /// <param name="software">The PLC software that owns the interfaces.</param>
        /// <param name="logger">Optional logger.</param>
        public OpcUaInterfaceExporter(PlcSoftware software, ILogger? logger = null)
        {
            _software = software ?? throw new ArgumentNullException(nameof(software));
            _logger = logger;
        }

        /// <summary>Lists the server interfaces the CPU publishes.</summary>
        /// <returns>One entry per interface, enabled or not.</returns>
        public IReadOnlyList<OpcUaInterfaceInfo> List()
        {
            return Interfaces()
                .Select(serverInterface => new OpcUaInterfaceInfo(
                    serverInterface.Name,
                    serverInterface.Enabled,
                    serverInterface.Author,
                    serverInterface.LastModified))
                .ToList();
        }

        /// <summary>Exports one server interface to a file.</summary>
        /// <param name="interfaceName">The interface to export.</param>
        /// <param name="exportPath">File to write. Its directory is created if missing.</param>
        /// <returns>The path written.</returns>
        /// <exception cref="PortalException">The interface does not exist on this CPU.</exception>
        public string Export(string interfaceName, string exportPath)
        {
            if (string.IsNullOrWhiteSpace(interfaceName) || string.IsNullOrWhiteSpace(exportPath))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "interfaceName and exportPath are required");
            }

            var serverInterface = Interfaces().FirstOrDefault(
                    candidate => string.Equals(candidate.Name, interfaceName, StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(
                    PortalErrorCode.NotFound,
                    $"No OPC UA server interface named '{interfaceName}'. Available: {DescribeAvailable()}");

            var directory = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Export refuses to write over an existing file, as the block exports do.
            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }

            serverInterface.Export(new FileInfo(exportPath));

            _logger?.LogInformation("OPC UA interface {Name} exported to {Path}", interfaceName, exportPath);

            return exportPath;
        }

        private IEnumerable<ServerInterface> Interfaces()
        {
            var provider = _software.GetService<OpcUaProvider>();

            if (provider?.CommunicationGroup?.ServerInterfaceGroup == null)
            {
                return Enumerable.Empty<ServerInterface>();
            }

            return provider.CommunicationGroup.ServerInterfaceGroup.ServerInterfaces;
        }

        private string DescribeAvailable()
        {
            var names = Interfaces().Select(candidate => $"'{candidate.Name}'").ToList();

            return names.Count == 0 ? "none — this CPU publishes no OPC UA interface" : string.Join(", ", names);
        }
    }
}
