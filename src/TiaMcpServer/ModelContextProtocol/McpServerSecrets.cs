using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Secrets;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Secrets by name only. There is no tool that takes or returns a value, on purpose: what goes through a tool stays in
    /// the conversation. A person stores one in their own terminal with <c>TiaMcpServer secret set NAME</c>.
    /// </remarks>
    public static partial class McpServer
    {
        private static ISecretStore SecretStore =>
            _services != null
                ? _services.GetRequiredService<ISecretStore>()
                : DpapiSecretStore.ForCurrentUser();

        [McpServerTool(Name = "ListSecrets", ReadOnly = true), Description("List the names of the secrets stored on this machine, encrypted for this Windows account. Never returns a value, and no tool takes one. To store a secret, ask the person to run 'TiaMcpServer secret set NAME' in their own terminal; never ask them to write it in the conversation.")]
        public static ResponseSecretNames ListSecrets()
        {
            try
            {
                var names = SecretStore.Names();

                return new ResponseSecretNames(names)
                {
                    Message = $"{names.Count} secret(s) stored",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, "listing the secrets");
            }
        }
    }
}
