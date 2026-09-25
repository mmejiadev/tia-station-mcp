using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.OpcUa
{
    /// <summary>
    /// Reads from an OPC UA server over a secured channel, one session per call.
    /// </summary>
    /// <remarks>
    /// **A session per call, deliberately.** Holding one open would mean deciding when it is stale,
    /// reconnecting it, and noticing when the CPU behind the address was swapped. A new session
    /// costs a few hundred milliseconds and checks the certificate pin every time.
    ///
    /// **One connection at a time.** The certificate check is a callback on a configuration shared
    /// by every session, and it has to know which endpoint it is judging. Serialising connections
    /// is what makes that knowable. Reads are short, so nothing waits long.
    ///
    /// **Secured endpoints only.** An endpoint offering no message security has no certificate
    /// worth pinning and carries values anyone on the wire could alter. It is refused with the
    /// setting to change on the CPU, rather than quietly used.
    /// </remarks>
    public sealed class OpcUaReader : IOpcUaReader, IDisposable
    {
        private const string SessionName = "tia-station-mcp read";
        private const uint MaxReferencesPerBrowse = 1000;

        private readonly string _pkiRoot;
        private readonly ServerCertificatePins _pins;
        private readonly ITelemetryContext _telemetry;
        private readonly SemaphoreSlim _oneConnection = new SemaphoreSlim(1, 1);

        private ApplicationConfiguration? _configuration;
        private OpcUaEndpoint? _connectingTo;
        private string? _certificateRefusal;

        /// <summary>Creates the reader.</summary>
        /// <param name="pkiRoot">Where the client keeps its own certificate.</param>
        /// <param name="pins">The certificates each server presented the first time.</param>
        /// <exception cref="ArgumentException"><paramref name="pkiRoot"/> is empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="pins"/> is null.</exception>
        public OpcUaReader(string pkiRoot, ServerCertificatePins pins)
        {
            if (string.IsNullOrWhiteSpace(pkiRoot))
            {
                throw new ArgumentException("A PKI directory is required", nameof(pkiRoot));
            }

            _pkiRoot = pkiRoot;
            _pins = pins ?? throw new ArgumentNullException(nameof(pins));
            _telemetry = DefaultTelemetry.Create(_ => { });
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<OpcUaNode>> BrowseAsync(OpcUaEndpoint endpoint, string? nodeId, CancellationToken cancellationToken)
        {
            var parent = string.IsNullOrWhiteSpace(nodeId) ? ObjectIds.ObjectsFolder : ParseNodeId(nodeId!);

            return await WithSessionAsync(
                endpoint,
                session => OpcUaBrowser.BrowseAsync(session, parent, MaxReferencesPerBrowse, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<OpcUaReading>> ReadAsync(OpcUaEndpoint endpoint, IReadOnlyList<string> nodeIds, CancellationToken cancellationToken)
        {
            if (nodeIds == null || nodeIds.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Name at least one node to read");
            }

            var parsed = nodeIds.Select(ParseNodeId).ToList();

            return await WithSessionAsync(
                endpoint,
                session => ReadValuesAsync(session, nodeIds, parsed, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _oneConnection.Dispose();
        }

        private async Task<T> WithSessionAsync<T>(OpcUaEndpoint endpoint, Func<ISession, Task<T>> work, CancellationToken cancellationToken)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            await _oneConnection.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Owned here rather than inside OpenSessionAsync: the session holds on to its
                // identity for as long as it is open, so the identity may only go when it does.
                using var token = new AnonymousIdentityToken();
                using var identity = new UserIdentity(token);
                using var session = await OpenSessionAsync(endpoint, identity, cancellationToken).ConfigureAwait(false);

                try
                {
                    return await work(session).ConfigureAwait(false);
                }
                finally
                {
                    await session.CloseAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _connectingTo = null;
                _certificateRefusal = null;
                _oneConnection.Release();
            }
        }

        private async Task<ISession> OpenSessionAsync(OpcUaEndpoint endpoint, IUserIdentity identity, CancellationToken cancellationToken)
        {
            var configuration = await ConfigurationAsync(cancellationToken).ConfigureAwait(false);
            var description = await SelectSecuredEndpointAsync(configuration, endpoint, cancellationToken).ConfigureAwait(false);
            var configured = new ConfiguredEndpoint(null, description, EndpointConfiguration.Create(configuration));

            _connectingTo = endpoint;
            _certificateRefusal = null;

            try
            {
                return await new DefaultSessionFactory(_telemetry).CreateAsync(
                    configuration,
                    configured,
                    false,
                    true,
                    SessionName,
                    (uint)configuration.ClientConfiguration.DefaultSessionTimeout,
                    identity,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceResultException exception)
            {
                throw ConnectionFailure(endpoint, exception);
            }
        }

        private async Task<EndpointDescription> SelectSecuredEndpointAsync(
            ApplicationConfiguration configuration,
            OpcUaEndpoint endpoint,
            CancellationToken cancellationToken)
        {
            EndpointDescription? description;

            try
            {
                description = await CoreClientUtils.SelectEndpointAsync(
                    configuration,
                    endpoint.Url.ToString(),
                    true,
                    _telemetry,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceResultException exception)
            {
                throw ConnectionFailure(endpoint, exception);
            }

            if (description == null)
            {
                throw new PortalException(PortalErrorCode.ConnectFailed, $"The OPC UA server at {endpoint} answered but described no endpoint.");
            }

            if (description.SecurityMode == MessageSecurityMode.None)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"The OPC UA server at {endpoint} offers no secured endpoint, and an unsecured one is refused: " +
                    "its values could be altered on the wire and it has no certificate to check. In TIA Portal, " +
                    "enable a security policy such as Basic256Sha256 - Sign & Encrypt under the CPU's " +
                    "OPC UA > Server > Security, then download the hardware configuration.");
            }

            return description;
        }

        private async Task<ApplicationConfiguration> ConfigurationAsync(CancellationToken cancellationToken)
        {
            if (_configuration != null)
            {
                return _configuration;
            }

            var configuration = await OpcUaClientConfiguration.CreateAsync(_pkiRoot, _telemetry, cancellationToken).ConfigureAwait(false);
            configuration.CertificateValidator.CertificateValidation += JudgeServerCertificate;

            _configuration = configuration;
            return configuration;
        }

        private void JudgeServerCertificate(CertificateValidator validator, CertificateValidationEventArgs arguments)
        {
            var endpoint = _connectingTo;

            // Only "nobody vouches for this certificate" is ours to overrule, and only by the pin.
            // An expired certificate, a revoked one or one for another host is a different finding
            // and stays refused, with the stack's own reason.
            if (endpoint == null || arguments.Error.StatusCode != StatusCodes.BadCertificateUntrusted)
            {
                return;
            }

            var thumbprint = arguments.Certificate.Thumbprint;
            var verdict = _pins.Verify(endpoint.PolicyTarget, thumbprint);

            arguments.Accept = verdict switch
            {
                PinVerdict.FirstUse => true,
                PinVerdict.Matches => true,
                PinVerdict.Changed => RefuseChangedCertificate(endpoint, thumbprint),
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised certificate pin verdict: {verdict}")
            };
        }

        private bool RefuseChangedCertificate(OpcUaEndpoint endpoint, string thumbprint)
        {
            _certificateRefusal =
                $"The OPC UA server at {endpoint} presented certificate {thumbprint}, but {_pins.Pinned(endpoint.PolicyTarget)} " +
                "was recorded the first time. The CPU may have been replaced or reset, or something else is answering " +
                "at that address. Only a person can tell which: if the change is expected, delete the line for " +
                $"'{endpoint.PolicyTarget}' from the pin file and connect again.";

            return false;
        }

        private PortalException ConnectionFailure(OpcUaEndpoint endpoint, ServiceResultException exception)
        {
            if (_certificateRefusal != null)
            {
                return new PortalException(PortalErrorCode.InvalidState, _certificateRefusal, null, exception);
            }

            return new PortalException(
                PortalErrorCode.ConnectFailed,
                $"Could not open an OPC UA session with {endpoint}: {StatusCodes.GetBrowseName(exception.StatusCode)}. {exception.Message}",
                null,
                exception);
        }

        private static async Task<IReadOnlyList<OpcUaReading>> ReadValuesAsync(
            ISession session,
            IReadOnlyList<string> requested,
            IReadOnlyList<NodeId> nodeIds,
            CancellationToken cancellationToken)
        {
            var toRead = new ReadValueIdCollection(nodeIds.Select(nodeId => new ReadValueId
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value
            }));

            var response = await session.ReadAsync(null, 0, TimestampsToReturn.Source, toRead, cancellationToken).ConfigureAwait(false);

            return response.Results
                .Select((dataValue, index) => OpcUaValueFormatter.Format(requested[index], dataValue))
                .ToList();
        }

        private static NodeId ParseNodeId(string text)
        {
            try
            {
                return NodeId.Parse(text);
            }
            catch (Exception exception) when (exception is ServiceResultException || exception is ArgumentException || exception is FormatException)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{text}' is not an OPC UA node identifier. Copy one from BrowseOpcUaServer, " +
                    "for example ns=3;s=\"DB_Cell\".\"Running\".",
                    null,
                    exception);
            }
        }
    }
}
