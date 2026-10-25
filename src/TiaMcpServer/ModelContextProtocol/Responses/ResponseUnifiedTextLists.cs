using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>A WinCC Unified HMI's text lists, or the list just written.</summary>
    public sealed class ResponseUnifiedTextLists : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The lists, as JSON.</param>
        public ResponseUnifiedTextLists(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The lists.</summary>
        public JsonObject Content { get; }
    }
}
