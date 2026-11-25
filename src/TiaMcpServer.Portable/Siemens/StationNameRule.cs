namespace TiaMcpServer.Siemens
{
    /// <summary>What the name of a station created through the server, and of the device group it goes in, may hold.</summary>
    /// <remarks>
    /// A station in a device group is written <c>Group/Station</c>, in its paths and in the write policy. A station named
    /// <c>Cell/PLC_1</c> at the top of the project would be spelled the same as <c>PLC_1</c> in group <c>Cell</c>, so a rule
    /// allowing <c>Cell/*</c> - meant for the group - would let a session create a station outside it. Neither name may
    /// therefore hold the separator. TIA Portal itself gives some hardware stations such names, which the readers already
    /// leave out of paths; the server just does not make new ones.
    /// </remarks>
    public static class StationNameRule
    {
        private const char Separator = '/';

        /// <summary>Throws unless the name can be a new station's.</summary>
        /// <param name="parameterName">The parameter the name came in, for the message.</param>
        /// <param name="stationName">The name.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for a name holding the separator.</exception>
        public static void RequireStationName(string parameterName, string stationName)
        {
            if ((stationName ?? string.Empty).IndexOf(Separator) >= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{parameterName} '{stationName}' must not hold '{Separator}': a station in a device group is written 'Group{Separator}Station', so the name would read as a group; use groupName for that");
            }
        }

        /// <summary>Throws unless the name can be one device group at the top of the project.</summary>
        /// <param name="groupName">The name, already trimmed.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for a name holding the separator.</exception>
        /// <remarks>One level only: a path would need each level made, and taken back when the device fails.</remarks>
        public static void RequireGroupName(string groupName)
        {
            if ((groupName ?? string.Empty).IndexOf(Separator) >= 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"groupName '{groupName}' must be one device group at the top of the project, without '{Separator}'");
            }
        }
    }
}
