#if TIA_V21
using Siemens.Engineering.HW.CommunicationConnections;

namespace TiaMcpServer.Siemens
{
    /// <summary>Describes a connection Openness holds, in the terms the program's instructions use.</summary>
    /// <remarks>
    /// Measured 2026-11-15 on TIA Portal V21: each kind keeps its name, IDs, addresses and TSAPs or ports on its own
    /// class, and the base <c>Connection</c> has only the kind, the validity and the two targets. A connection read from
    /// the partner's side has the ends swapped: on the PLC it was made from, <c>S7_Connection_1</c> is local to
    /// partner; on the partner, local is the partner. The kinds this server does not make are still listed, with what
    /// the base class says, because a list that hid them would misreport what the PLC has.
    /// </remarks>
    internal static class PlcConnectionReader
    {
        /// <summary>Describes one connection.</summary>
        /// <param name="connection">The connection, as the PLC's <c>CommunicationManagement</c> lists it.</param>
        /// <returns>Its summary, seen from the PLC it was read from.</returns>
        internal static PlcConnectionSummary Describe(Connection connection)
        {
            return connection switch
            {
                S7Connection s7 => Summarise(connection, s7.LocalConnectionName, Ends(s7)),
                TcpConnection tcp => Summarise(connection, tcp.LocalConnectionName, Ends(tcp)),
                IsoOnTcpConnection iso => Summarise(connection, iso.LocalConnectionName, Ends(iso)),
                HmiConnection hmi => Summarise(connection, hmi.LocalConnectionName, Ends(connection, hmi.LocalAddress, hmi.PartnerAddress)),
                _ => Summarise(connection, string.Empty, Ends(connection, string.Empty, string.Empty))
            };
        }

        private static PlcConnectionSummary Summarise(Connection connection, string name, PlcConnectionEnds ends)
        {
            return new PlcConnectionSummary(connection.ConnectionType.ToString(), name ?? string.Empty, connection.IsValid, ends);
        }

        private static PlcConnectionEnds Ends(S7Connection s7)
        {
            return new PlcConnectionEnds(
                new PlcConnectionEnd(DeviceName(s7.LocalTarget), s7.LocalAddress, s7.LocalConnectionId, "TSAP " + s7.LocalTsap),
                new PlcConnectionEnd(DeviceName(s7.PartnerTarget), s7.PartnerAddress, s7.PartnerConnectionId, "TSAP " + s7.PartnerTsap),
                s7.LocalActiveEstablishment);
        }

        private static PlcConnectionEnds Ends(TcpConnection tcp)
        {
            return new PlcConnectionEnds(
                new PlcConnectionEnd(DeviceName(tcp.LocalTarget), tcp.LocalAddress, tcp.LocalConnectionId, "port " + tcp.LocalPort),
                new PlcConnectionEnd(DeviceName(tcp.PartnerTarget), tcp.PartnerAddress, tcp.PartnerConnectionId, "port " + tcp.PartnerPort),
                tcp.LocalActiveEstablishment);
        }

        private static PlcConnectionEnds Ends(IsoOnTcpConnection iso)
        {
            return new PlcConnectionEnds(
                new PlcConnectionEnd(DeviceName(iso.LocalTarget), iso.LocalAddress, iso.LocalConnectionId, "TSAP " + iso.LocalTsap),
                new PlcConnectionEnd(DeviceName(iso.PartnerTarget), iso.PartnerAddress, iso.PartnerConnectionId, "TSAP " + iso.PartnerTsap),
                iso.LocalActiveEstablishment);
        }

        private static PlcConnectionEnds Ends(Connection connection, string localAddress, string partnerAddress)
        {
            return new PlcConnectionEnds(
                new PlcConnectionEnd(DeviceName(connection.LocalTarget), localAddress ?? string.Empty, string.Empty, string.Empty),
                new PlcConnectionEnd(DeviceName(connection.PartnerTarget), partnerAddress ?? string.Empty, string.Empty, string.Empty),
                false);
        }

        private static string DeviceName(global::Siemens.Engineering.HW.DeviceItem? item)
        {
            return item?.Name ?? string.Empty;
        }
    }
}
#endif
