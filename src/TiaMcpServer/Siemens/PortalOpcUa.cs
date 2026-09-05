using Microsoft.Extensions.Logging;
using Siemens.Engineering.SW;
using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// OPC UA server interfaces: configuration rather than code, and worth versioning for it.
    ///
    /// A change here breaks every client without touching a line of SCL, which is exactly why
    /// these can be exported to text.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Lists the OPC UA server interfaces a CPU publishes.</summary>
        /// <param name="softwarePath">Full path to the PLC software, for example <c>PLC_0</c>.</param>
        /// <returns>One entry per interface, enabled or not. Empty when the CPU publishes none.</returns>
        /// <exception cref="PortalException">No project is open, or the path does not resolve.</exception>
        public IReadOnlyList<OpcUaInterfaceInfo> GetOpcUaInterfaces(string softwarePath)
        {
            try
            {
                return new OpcUaInterfaceExporter(RequireSoftware(softwarePath), _logger).List();
            }
            catch (Exception ex)
            {
                throw DecorateOpcUaFailure(ex, softwarePath, "GetOpcUaInterfaces");
            }
        }

        /// <summary>
        /// Exports one OPC UA server interface to a file.
        /// </summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="interfaceName">The interface to export.</param>
        /// <param name="exportPath">File to write.</param>
        /// <returns>The path written.</returns>
        /// <remarks>
        /// The interface is the contract between the PLC and everything that talks to it over
        /// OPC UA. It is configuration rather than code, which is why it belongs in version
        /// control: changing it breaks every client without touching a line of SCL.
        /// </remarks>
        /// <exception cref="PortalException">
        /// No project is open, the path does not resolve, or the CPU publishes no such interface.
        /// </exception>
        public string ExportOpcUaInterface(string softwarePath, string interfaceName, string exportPath)
        {
            _logger?.LogInformation("Exporting OPC UA interface {Interface} of {SoftwarePath}...", interfaceName, softwarePath);

            try
            {
                return new OpcUaInterfaceExporter(RequireSoftware(softwarePath), _logger).Export(interfaceName, exportPath);
            }
            catch (Exception ex)
            {
                throw DecorateOpcUaFailure(ex, softwarePath, "ExportOpcUaInterface");
            }
        }

        private PlcSoftware RequireSoftware(string softwarePath)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project first");
            }

            return FindPlcSoftware(softwarePath)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"PLC software not found: {softwarePath}");
        }

        private PortalException DecorateOpcUaFailure(Exception ex, string softwarePath, string operation)
        {
            var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"{operation} failed: {ex.Message}", null, ex);

            pex.Data["softwarePath"] = softwarePath;

            _logger?.LogError(pex, "{Operation} failed for {SoftwarePath}", operation, softwarePath);

            return pex;
        }
    }
}
