using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
#if TIA_V21
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.CommunicationConnections;
using System.Linq;
#endif

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// The configured connections between two PLCs: S7 (PUT, GET, BSEND, BRCV), TCP and ISO-on-TCP (TSEND, TRCV).
    ///
    /// Measured 2026-11-15 on TIA Portal V21, a CPU 1511-1 PN V4.0 and a CPU 1215C V4.7 on one subnet:
    /// <c>CommunicationManagement.Connections.Create&lt;S7Connection&gt;(localNode, partnerCpu, partnerNode)</c> made
    /// <c>S7_Connection_1</c>, ID 100 at both ends, TSAP <c>SIMATIC-ACC10001</c>, the 1511 establishing it; TCP and
    /// ISO-on-TCP came out as IDs 101 and 102, ports 2000 and TSAP <c>TCP-1</c>. Each was valid, the partner listed it
    /// too, and with the PLCs' passwords dealt with (SetPlcFullAccess) both stations compiled with no error. V20's
    /// Openness has none of this.
    ///
    /// Unlike the HMI connection, a second one between the same PLCs is not refused: a program may well use two S7
    /// connections, or an S7 and a TCP one, and TIA Portal gives each its own ID.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Whether this build can read and make connections between PLCs: only one built for TIA Portal V21.</summary>
        /// <remarks>Asked before a plan is made, so a call that can never run leaves no backup and no audit entry.</remarks>
#if TIA_V21
        public static bool CanUsePlcConnections => true;
#else
        public static bool CanUsePlcConnections => false;
#endif

        /// <summary>Lists a PLC's configured connections, seen from that PLC.</summary>
        /// <param name="plcPath">The PLC's software, for example <c>PLC_1</c>.</param>
        /// <returns>Every connection it has, of any kind, its own end first.</returns>
        /// <exception cref="PortalException">Not TIA Portal V21, no project, or the path is not a PLC.</exception>
        public IReadOnlyList<PlcConnectionSummary> GetPlcConnections(string plcPath)
        {
            _logger?.LogInformation("Reading the connections of {PlcPath}...", plcPath);

            try
            {
                return ReadPlcConnections(plcPath);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, $"Reading the connections failed: {ex.Message}", null, ex);

                pex.Data["plcPath"] = plcPath;

                _logger?.LogError(pex, "GetPlcConnections failed for {PlcPath}", plcPath);
                throw pex;
            }
        }

        /// <summary>Makes a connection from one PLC to another on their shared subnet.</summary>
        /// <param name="plcPath">The PLC that establishes it, for example <c>PLC_1</c>.</param>
        /// <param name="partnerPlcPath">The PLC at the other end.</param>
        /// <param name="kind">S7, TCP or ISO-on-TCP.</param>
        /// <param name="backupDirectory">Where the network is recorded first. Required.</param>
        /// <returns>The connection TIA Portal made, seen from <paramref name="plcPath"/>.</returns>
        /// <exception cref="PortalException">
        /// Not TIA Portal V21, no project, a path that is not a PLC, both paths the same PLC, the two not on one subnet,
        /// or TIA Portal refused.
        /// </exception>
        public PlcConnectionSummary CreatePlcConnection(string plcPath, string partnerPlcPath, PlcConnectionKind kind, string backupDirectory)
        {
            _logger?.LogInformation("Making an {Kind} connection from {PlcPath} to {PartnerPath}...", kind, plcPath, partnerPlcPath);

            try
            {
                if (string.IsNullOrWhiteSpace(backupDirectory))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this changes the network's connections");
                }

                return MakePlcConnection(new PlcConnectionRequest(plcPath, partnerPlcPath, kind), backupDirectory);
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"Making the connection failed: {ex.Message}", null, ex);

                pex.Data["plcPath"] = plcPath;
                pex.Data["partnerPlcPath"] = partnerPlcPath;
                pex.Data["kind"] = kind.ToString();
                pex.Data["backupDirectory"] = backupDirectory;

                _logger?.LogError(pex, "CreatePlcConnection failed for {PlcPath} to {PartnerPath}", plcPath, partnerPlcPath);
                throw pex;
            }
        }

#if TIA_V21
        private List<PlcConnectionSummary> ReadPlcConnections(string plcPath)
        {
            RequireProjectForConnections();

            return RequireConnections(RequirePlcItem(plcPath), plcPath)
                .Select(PlcConnectionReader.Describe)
                .ToList();
        }

        private PlcConnectionSummary MakePlcConnection(PlcConnectionRequest request, string backupDirectory)
        {
            RequireProjectForConnections();

            var plc = RequirePlcItem(request.PlcPath);
            var partner = RequirePlcItem(request.PartnerPlcPath);

            RequireTwoPlcs(plc, partner, request);

            var (localNode, partnerNode) = RequireSharedSubnet(plc, partner, request.PlcPath, request.PartnerPlcPath);
            var connections = RequireConnections(plc, request.PlcPath);

            RecordNetworkBefore(backupDirectory);

            return PlcConnectionReader.Describe(CreateConnection(connections, request.Kind, localNode, (partner, partnerNode)));
        }

        private void RequireProjectForConnections()
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before reading or making connections");
            }
        }

        private static void RequireTwoPlcs(DeviceItem plc, DeviceItem partner, PlcConnectionRequest request)
        {
            if (IsSameDeviceItem(plc, partner))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{request.PlcPath}' and '{request.PartnerPlcPath}' are the same PLC: a connection needs two");
            }
        }

        /// <remarks>
        /// One entry per kind and nothing else: a kind added to <see cref="PlcConnectionKind"/> without a line here
        /// throws rather than making some other kind of connection.
        /// </remarks>
        private static Connection CreateConnection(ConnectionComposition connections, PlcConnectionKind kind, Node localNode, (DeviceItem Plc, Node Node) partner)
        {
            return kind switch
            {
                PlcConnectionKind.S7 => connections.Create<S7Connection>(localNode, partner.Plc, partner.Node),
                PlcConnectionKind.Tcp => connections.Create<TcpConnection>(localNode, partner.Plc, partner.Node),
                PlcConnectionKind.IsoOnTcp => connections.Create<IsoOnTcpConnection>(localNode, partner.Plc, partner.Node),
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised connection kind: {kind}")
            };
        }
#else
        /// <remarks>V20's Openness has no connections between PLCs (see the class remarks).</remarks>
        private IReadOnlyList<PlcConnectionSummary> ReadPlcConnections(string plcPath)
        {
            throw new PortalException(PortalErrorCode.InvalidState, "Reading connections between PLCs needs TIA Portal V21.");
        }

        /// <remarks>V20's Openness has no connections between PLCs (see the class remarks).</remarks>
        private PlcConnectionSummary MakePlcConnection(PlcConnectionRequest request, string backupDirectory)
        {
            throw new PortalException(PortalErrorCode.InvalidState, "Making connections between PLCs needs TIA Portal V21; on V20 they are made by hand in the Devices & networks view.");
        }
#endif
    }
}
