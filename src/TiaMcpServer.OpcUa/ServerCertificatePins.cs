using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.OpcUa
{
    /// <summary>
    /// Remembers which certificate each OPC UA server presented the first time, and refuses a
    /// different one afterwards.
    /// </summary>
    /// <remarks>
    /// A CPU makes its own self-signed certificate, and there is no authority on a workshop network
    /// that could vouch for it. So the first certificate a server presents is accepted and written
    /// down, and every later connection must present the same one. A different certificate means
    /// the CPU was replaced or reset, or something else answered at that address. Only a person can
    /// tell which, so the connection is refused and the refusal names both thumbprints.
    ///
    /// To accept a replaced CPU, a person deletes its line from the file. No tool does this. A tool
    /// that forgot a pin would undo the check with one call.
    ///
    /// The file is plain JSON, one line per server, keyed by the endpoint's policy target:
    ///
    /// <code>
    /// { "opcua/192.168.0.1:4840": "3F2A…" }
    /// </code>
    ///
    /// A file that exists and cannot be read refuses every connection. If an unreadable pin file
    /// were treated as an empty one, damaging the file would count as a first use and switch the
    /// check off.
    /// </remarks>
    public sealed class ServerCertificatePins
    {
        private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions { WriteIndented = true };

        private readonly string _path;
        private readonly object _lock = new object();

        /// <summary>Creates the pin store.</summary>
        /// <param name="path">Where the pins are kept. Created on the first pin.</param>
        /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
        public ServerCertificatePins(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A pin file path is required", nameof(path));
            }

            _path = path;
        }

        /// <summary>
        /// Checks a presented certificate against the one on file, recording it if there is none.
        /// </summary>
        /// <param name="policyTarget">The server, as <see cref="OpcUaEndpoint.PolicyTarget"/> names it.</param>
        /// <param name="thumbprint">The thumbprint of the certificate it presented.</param>
        /// <returns>What the file said.</returns>
        /// <exception cref="ArgumentException">Either argument is empty.</exception>
        /// <exception cref="PortalException">The pin file exists and cannot be read or written.</exception>
        public PinVerdict Verify(string policyTarget, string thumbprint)
        {
            if (string.IsNullOrWhiteSpace(policyTarget))
            {
                throw new ArgumentException("A server is required", nameof(policyTarget));
            }

            if (string.IsNullOrWhiteSpace(thumbprint))
            {
                throw new ArgumentException("A certificate thumbprint is required", nameof(thumbprint));
            }

            // One lock across reading and writing. Two first connections at once must not both
            // find the file empty and both record, the second overwriting the first.
            lock (_lock)
            {
                var pins = Read();

                if (pins.TryGetValue(policyTarget, out var pinned))
                {
                    return string.Equals(pinned, thumbprint, StringComparison.OrdinalIgnoreCase)
                        ? PinVerdict.Matches
                        : PinVerdict.Changed;
                }

                pins[policyTarget] = thumbprint.ToUpperInvariant();
                Write(pins);

                return PinVerdict.FirstUse;
            }
        }

        /// <summary>The thumbprint on file for a server, if there is one.</summary>
        /// <param name="policyTarget">The server.</param>
        /// <returns>The thumbprint, or null when nothing is pinned.</returns>
        /// <exception cref="PortalException">The pin file exists and cannot be read.</exception>
        public string? Pinned(string policyTarget)
        {
            lock (_lock)
            {
                return Read().TryGetValue(policyTarget ?? string.Empty, out var pinned) ? pinned : null;
            }
        }

        private Dictionary<string, string> Read()
        {
            if (!File.Exists(_path))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var pins = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path));

                return new Dictionary<string, string>(
                    pins ?? new Dictionary<string, string>(),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (exception is JsonException || exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"The OPC UA certificate pins at '{_path}' could not be read: {exception.Message}. " +
                    "Refusing every OPC UA connection rather than treating an unreadable file as an empty one.",
                    null,
                    exception);
            }
        }

        private void Write(Dictionary<string, string> pins)
        {
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(_path));

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Written beside the file and moved over it. A crash half way through a direct
                // write would leave a truncated file, and that must not read as "nothing pinned".
                var temporary = _path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(pins, Indented));

                if (File.Exists(_path))
                {
                    File.Replace(temporary, _path, null);
                    return;
                }

                File.Move(temporary, _path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"The OPC UA certificate pin could not be recorded at '{_path}': {exception.Message}. " +
                    "Refusing the connection: a first use that is not written down would be a first use again next time.",
                    null,
                    exception);
            }
        }
    }
}
