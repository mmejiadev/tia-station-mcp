using System;
using System.Globalization;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.OpcUa
{
    /// <summary>
    /// The address of an OPC UA server, checked before anything connects to it.
    /// </summary>
    /// <remarks>
    /// A caller hands this server a string, and a string can name any machine on the network. So
    /// the string is read once, here, into the two things the rest of the assembly needs: the URL
    /// the stack connects to, and the policy target a person listed in <c>policy.json</c>. Parsing
    /// it twice, in two places, is how the URL that is dialled and the name that was authorised
    /// come to differ.
    ///
    /// The policy target is host and port only. A path on the URL selects a server *on* a machine,
    /// and what a policy author decides is which machine may be contacted.
    /// </remarks>
    public sealed class OpcUaEndpoint
    {
        /// <summary>The only scheme a Siemens CPU serves, and the only one accepted.</summary>
        public const string Scheme = "opc.tcp";

        /// <summary>The port an S7-1500 listens on unless somebody changed it.</summary>
        public const int DefaultPort = 4840;

        private OpcUaEndpoint(Uri url)
        {
            Url = url;
            // Uri already lower-cases the host, and policy patterns match without regard to case.
            Host = url.Host;
            Port = url.Port;
        }

        /// <summary>The URL the client connects to.</summary>
        public Uri Url { get; }

        /// <summary>The machine.</summary>
        public string Host { get; }

        /// <summary>The TCP port.</summary>
        public int Port { get; }

        /// <summary>
        /// The name a policy lists to allow this server, for example <c>opcua/192.168.0.1:4840</c>.
        /// </summary>
        /// <remarks>Built by <see cref="ChangeTarget"/>, where every policy target is built.</remarks>
        public string PolicyTarget => ChangeTarget.OpcUaServer(Host, Port);

        /// <summary>Reads an endpoint URL.</summary>
        /// <param name="text">For example <c>opc.tcp://192.168.0.1:4840</c>.</param>
        /// <returns>The endpoint.</returns>
        /// <exception cref="PortalException">
        /// The text is not an <c>opc.tcp</c> URL naming a host, or it carries credentials.
        /// </exception>
        public static OpcUaEndpoint Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw Invalid(text, "no endpoint was given");
            }

            var withPort = AddDefaultPort(text.Trim());

            if (!Uri.TryCreate(withPort, UriKind.Absolute, out var url))
            {
                throw Invalid(text, "it is not a URL");
            }

            if (!string.Equals(url.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw Invalid(text, $"the scheme is '{url.Scheme}', and only '{Scheme}' is served by a CPU");
            }

            // "opc.tcp://host:" names no port, and Uri reports that as -1 rather than refusing it.
            // Found by running the tools: the policy target came out as "opcua/host:-1".
            if (url.Port <= 0)
            {
                throw Invalid(text, "it names no port");
            }

            // A password in a URL ends up in the audit trail, the log and the policy file. The
            // server identifies the session with a certificate; credentials do not travel this way.
            if (!string.IsNullOrEmpty(url.UserInfo))
            {
                throw Invalid(text, "it carries credentials, which may not be written into an endpoint");
            }

            return new OpcUaEndpoint(url);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            // Uri prints an empty path as "/", which reads as part of the address in every message.
            return Url.AbsolutePath == "/" ? $"{Scheme}://{Host}:{Port.ToString(CultureInfo.InvariantCulture)}" : Url.ToString();
        }

        // Uri reports -1 for a scheme it does not know, and opc.tcp is one it does not know, so a
        // URL without a port would otherwise produce the target "opcua/host:-1" — which no policy
        // lists, and which would be refused with a reason that names a port nobody typed.
        private static string AddDefaultPort(string text)
        {
            var authorityStart = text.IndexOf("://", StringComparison.Ordinal);

            if (authorityStart < 0)
            {
                return text;
            }

            var hostStart = authorityStart + 3;
            var pathStart = text.IndexOf('/', hostStart);
            var authority = pathStart < 0 ? text.Substring(hostStart) : text.Substring(hostStart, pathStart - hostStart);

            if (authority.LastIndexOf(':') > authority.LastIndexOf(']'))
            {
                return text;
            }

            var rest = pathStart < 0 ? string.Empty : text.Substring(pathStart);

            return text.Substring(0, hostStart) + authority + ":" + DefaultPort.ToString(CultureInfo.InvariantCulture) + rest;
        }

        private static PortalException Invalid(string? text, string reason)
        {
            return new PortalException(
                PortalErrorCode.InvalidParams,
                $"'{text}' is not an OPC UA endpoint: {reason}. Expected something like opc.tcp://192.168.0.1:4840.");
        }
    }
}
