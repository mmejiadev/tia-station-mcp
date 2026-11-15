using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads the kind of PLC connection a caller asks for, and names each kind back.</summary>
    /// <remarks>
    /// Kept apart from Openness so the words accepted are tested without TIA Portal. Case, spaces, hyphens and underscores
    /// are ignored, so "ISO-on-TCP", "iso_on_tcp" and "IsoOnTcp" all mean the same; anything else is refused with the
    /// accepted words, never mapped to a kind by a guess.
    /// </remarks>
    public static class PlcConnectionKinds
    {
        /// <summary>The words accepted, as a caller should write them.</summary>
        public const string Accepted = "S7, TCP, ISO-on-TCP";

        /// <summary>Reads a connection kind.</summary>
        /// <param name="text">What the caller wrote, e.g. <c>S7</c> or <c>ISO-on-TCP</c>.</param>
        /// <returns>The kind.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it is blank or not one of <see cref="Accepted"/>.</exception>
        public static PlcConnectionKind Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"connectionType is required: one of {Accepted}");
            }

            return Normalise(text!) switch
            {
                "S7" => PlcConnectionKind.S7,
                "TCP" => PlcConnectionKind.Tcp,
                "ISOONTCP" => PlcConnectionKind.IsoOnTcp,
                _ => throw new PortalException(PortalErrorCode.InvalidParams, $"'{text}' is not a connection this server makes. Use one of {Accepted}.")
            };
        }

        /// <summary>Names a kind the way TIA Portal's Devices &amp; networks view does.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>Its name, e.g. <c>ISO-on-TCP</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> for a value this does not know.</exception>
        public static string Name(PlcConnectionKind kind)
        {
            return kind switch
            {
                PlcConnectionKind.S7 => "S7",
                PlcConnectionKind.Tcp => "TCP",
                PlcConnectionKind.IsoOnTcp => "ISO-on-TCP",
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised connection kind: {kind}")
            };
        }

        private static string Normalise(string text)
        {
            return new string(text.Where(character => !char.IsWhiteSpace(character) && character != '-' && character != '_').ToArray())
                .ToUpperInvariant();
        }
    }
}
