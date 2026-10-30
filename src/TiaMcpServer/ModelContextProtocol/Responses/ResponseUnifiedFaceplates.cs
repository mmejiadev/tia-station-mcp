using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>WinCC Unified faceplate instances, read or just placed.</summary>
    public sealed class ResponseUnifiedFaceplates : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The faceplates, as JSON.</param>
        public ResponseUnifiedFaceplates(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The faceplates.</summary>
        public JsonObject Content { get; }
    }
}
