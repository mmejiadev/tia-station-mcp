using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>A WinCC Unified HMI's connections and tags, or the tags just created.</summary>
    public sealed class ResponseUnifiedTags : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The connections and tags, as JSON.</param>
        public ResponseUnifiedTags(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The connections and tags.</summary>
        public JsonObject Content { get; }
    }
}
