using System.Text.Json.Nodes;

namespace TiaMcpServer.Siemens
{
    /// <summary>What placing a device group template did to each PLC's protection, by the PLC's name.</summary>
    /// <remarks>Moved out of <c>PortalDeviceTemplates</c> when that file passed 300 lines; only the placement fills it.</remarks>
    internal sealed class PlcProtection
    {
        /// <summary>The PLCs protected with the stored secret.</summary>
        public JsonArray Protected { get; } = new JsonArray();

        /// <summary>The PLCs left without a password, as no secret was named.</summary>
        public JsonArray WithoutPassword { get; } = new JsonArray();

        /// <summary>The PLCs that had a password in the template, which they keep.</summary>
        public JsonArray AlreadyProtected { get; } = new JsonArray();
    }
}
