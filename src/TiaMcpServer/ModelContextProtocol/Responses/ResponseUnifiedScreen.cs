using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>A WinCC Unified screen, or an item type, in the shape DrawUnifiedScreen takes.</summary>
    public sealed class ResponseUnifiedScreen : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The screen or the item type, as JSON.</param>
        public ResponseUnifiedScreen(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The screen or the item type.</summary>
        public JsonObject Content { get; }
    }
}
