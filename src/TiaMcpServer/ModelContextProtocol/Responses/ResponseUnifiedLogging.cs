using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>A WinCC Unified HMI's data logs, alarm logs and logging tags, read or just created.</summary>
    public sealed class ResponseUnifiedLogging : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The logs and logging tags, as JSON.</param>
        public ResponseUnifiedLogging(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The logs and logging tags.</summary>
        public JsonObject Content { get; }
    }
}
