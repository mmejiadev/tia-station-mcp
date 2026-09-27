using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// What every MCP tool in this server answers with, at minimum.
    /// </summary>
    /// <remarks>
    /// The base of the response hierarchy in the <c>ModelContextProtocol/Responses/</c> folder of the
    /// server, and it lives here rather than beside its fifty-three descendants because
    /// <see cref="GuardedTool"/> constrains on it. Every write tool goes through that class, so its
    /// tests are the ones that must run on a machine with no TIA Portal — which they can only do if
    /// nothing on this path reaches into an assembly that needs one.
    ///
    /// The descendants stay in the other assembly. Inheriting across the boundary costs nothing, and
    /// moving them all would drag four hundred undocumented public members of inherited code into a
    /// project whose whole point is that it has no debt ledger.
    ///
    /// The descendants without XML doc are inherited from upstream: unsealed, and with public
    /// setters wherever they hold data. The documented ones are this project's own and immutable, as <c>CLAUDE.md</c> requires: their
    /// data is set through the constructor, and only <see cref="Message"/> and <see cref="Meta"/>
    /// remain settable.
    /// </remarks>
    public class ResponseMessage
    {
        /// <summary>What happened, in a sentence for the caller.</summary>
        public string? Message { get; set; }

        /// <summary>Structured detail beside the message: timestamps, counts, paths.</summary>
        /// <remarks>
        /// The setter is what forty response types in <c>Responses/</c> assign in an object
        /// initializer. Making it read-only is the right shape for a new type and the wrong change
        /// to make to this one: it would rewrite every response site in the server for a property
        /// that is serialised straight to JSON and never mutated after construction.
        /// </remarks>
        [SuppressMessage(
            "Usage",
            "CA2227:Collection properties should be read only",
            Justification = "Assigned in object initializers by every response type; see the remarks.")]
        public JsonObject? Meta { get; set; }
    }
}
