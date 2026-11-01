using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>WinCC Unified trend controls and their trends, read or just set.</summary>
    public sealed class ResponseUnifiedTrends : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The trend controls, as JSON.</param>
        public ResponseUnifiedTrends(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The trend controls.</summary>
        public JsonObject Content { get; }
    }
}
