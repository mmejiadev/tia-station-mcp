using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Library types with their versions, read, copied or just released.</summary>
    public sealed class ResponseLibraryTypes : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The types, as JSON.</param>
        public ResponseLibraryTypes(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The types.</summary>
        public JsonObject Content { get; }
    }
}
