using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Configuration;

namespace TiaMcpServer.OpcUa
{
    /// <summary>
    /// Builds the client's own identity: its application configuration and its certificate.
    /// </summary>
    /// <remarks>
    /// A secured OPC UA channel is mutual. The CPU checks this client's certificate as well as the
    /// other way round, so the client needs one of its own. It is created on first use under the
    /// PKI directory and reused afterwards, so a CPU that was told to trust it once keeps trusting
    /// it. A new certificate on every start would need a person at the CPU on every start.
    ///
    /// <c>AutoAcceptUntrustedCertificates</c> is off. The server's certificate is judged by
    /// <see cref="ServerCertificatePins"/>, never by a setting that accepts everything.
    /// </remarks>
    internal static class OpcUaClientConfiguration
    {
        internal const string ApplicationName = "tia-station-mcp";

        private const int OperationTimeoutMilliseconds = 15000;
        private const int SessionTimeoutMilliseconds = 60000;
        private const ushort CertificateLifetimeMonths = 60;

        internal static async Task<ApplicationConfiguration> CreateAsync(
            string pkiRoot,
            ITelemetryContext telemetry,
            CancellationToken cancellationToken)
        {
            var root = Path.GetFullPath(pkiRoot);
            var application = new ApplicationInstance(telemetry)
            {
                ApplicationName = ApplicationName,
                ApplicationType = ApplicationType.Client
            };

            var configuration = await application
                .Build($"urn:{Environment.MachineName}:{ApplicationName}", $"urn:{ApplicationName}")
                .SetOperationTimeout(OperationTimeoutMilliseconds)
                .AsClient()
                .SetDefaultSessionTimeout(SessionTimeoutMilliseconds)
                .AddSecurityConfiguration(OwnCertificate(root), root)
                .SetAutoAcceptUntrustedCertificates(false)
                .SetRejectSHA1SignedCertificates(true)
                .CreateAsync(cancellationToken)
                .ConfigureAwait(false);

            var hasCertificate = await application
                .CheckApplicationInstanceCertificatesAsync(false, CertificateLifetimeMonths, cancellationToken)
                .ConfigureAwait(false);

            if (!hasCertificate)
            {
                throw new InvalidOperationException(
                    $"The OPC UA client certificate could not be created under '{root}'.");
            }

            return configuration;
        }

        private static CertificateIdentifierCollection OwnCertificate(string root)
        {
            return new CertificateIdentifierCollection
            {
                new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(root, "own"),
                    SubjectName = $"CN={ApplicationName}, O={ApplicationName}",
                    CertificateType = ObjectTypeIds.RsaSha256ApplicationCertificateType
                }
            };
        }
    }
}
