using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Master copies of a library, read or just created, or an object created in a program from one.</summary>
    public sealed class ResponseMasterCopies : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The master copies, or what was created, as JSON.</param>
        public ResponseMasterCopies(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The master copies, or what was created.</summary>
        public JsonObject Content { get; }
    }
}
