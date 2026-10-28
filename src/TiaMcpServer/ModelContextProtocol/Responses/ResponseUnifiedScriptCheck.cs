using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>What a check of WinCC Unified scripts found.</summary>
    public sealed class ResponseUnifiedScriptCheck : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The counts and the findings, as JSON.</param>
        public ResponseUnifiedScriptCheck(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The counts and the findings.</summary>
        public JsonObject Content { get; }
    }
}
