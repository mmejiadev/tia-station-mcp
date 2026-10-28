using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>A WinCC Unified HMI's alarm classes and alarms, or the ones just created.</summary>
    public sealed class ResponseUnifiedAlarms : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The classes and alarms, as JSON.</param>
        public ResponseUnifiedAlarms(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The classes and alarms.</summary>
        public JsonObject Content { get; }
    }
}
