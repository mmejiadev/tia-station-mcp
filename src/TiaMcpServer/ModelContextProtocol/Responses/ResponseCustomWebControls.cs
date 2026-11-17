using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>WinCC Unified Custom Web Controls: the catalogue, those installed, or what was just installed or placed.</summary>
    public sealed class ResponseCustomWebControls : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The controls, as JSON.</param>
        public ResponseCustomWebControls(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The controls.</summary>
        public JsonObject Content { get; }
    }
}
