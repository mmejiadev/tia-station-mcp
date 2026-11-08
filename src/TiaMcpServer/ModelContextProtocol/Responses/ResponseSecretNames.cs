using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The names of the secrets held. Never a value.</summary>
    public sealed class ResponseSecretNames : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="names">The names.</param>
        public ResponseSecretNames(IReadOnlyList<string> names)
        {
            Names = names;
        }

        /// <summary>The names.</summary>
        public IReadOnlyList<string> Names { get; }
    }
}
