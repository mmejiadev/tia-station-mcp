using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>The global script modules of a WinCC Unified HMI, or what was written to one.</summary>
    public sealed class ResponseUnifiedScriptModules : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="content">The modules, or the write's result, as JSON.</param>
        public ResponseUnifiedScriptModules(JsonObject content)
        {
            Content = content;
        }

        /// <summary>The modules, or the write's result.</summary>
        public JsonObject Content { get; }
    }
}
