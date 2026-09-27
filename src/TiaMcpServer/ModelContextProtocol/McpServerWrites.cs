using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Every tool that changes anything, and nothing else.
    /// </summary>
    /// <remarks>
    /// The same class as <c>McpServer</c>, in files of its own. Splitting by responsibility rather
    /// than restructuring the whole surface was deliberate: what a reader needs to be able to find
    /// is the complete list of things this server can change, and that list was previously scattered
    /// through two and a half thousand lines of read tools.
    ///
    /// **The tools are split by area into <c>McpServerWrites.&lt;Area&gt;.cs</c>**, and the file name
    /// is what keeps the property below: the list of what this server can change is every file whose
    /// name starts <c>McpServerWrites</c>. Splitting it into files named after their areas alone would
    /// have scattered the list again; one file of 1,824 lines had stopped being readable by eye.
    ///
    /// **Everything in them calls <c>GuardedTool.Run</c>, with one exception named below.** That is
    /// the property these files exist to make checkable by eye: a tool in them that does not name a
    /// <c>ChangeTarget</c> and pass it to the guard is a bug, and now it is a visible one. A new
    /// write tool belongs in one of them, and in <c>Test16GuardedWrites</c>.
    ///
    /// The exception is <c>ApplyChange</c>, and it is the other half of the guard rather than a way
    /// around it: it confirms a plan the guard already produced and recorded. Making it take the
    /// guard as well would mean planning the confirmation of a plan. It is stated here because an
    /// "everything" that has an unstated exception stops being checkable by eye, which is the whole
    /// value of these files.
    ///
    /// A partial class rather than a separate type because the MCP SDK discovers tools by attribute
    /// on a type marked <c>[McpServerToolType]</c>, and because these methods share the private
    /// service accessors — <c>GuardedWrites</c>, <c>Backups</c>, <c>JobStore</c>, <c>Portal</c> —
    /// with the read tools. Two types would mean exposing those or duplicating them.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "ApplyChange"), Description("Confirm a planned change so it runs. The plan id comes from the tool that proposed it. A plan is spent once used and expires after ten minutes, so an old confirmation cannot authorise a later write.")]
        public static ResponseMessage ApplyChange(
            [Description("planId: the code of the plan to confirm, for example 'K7M-2QX'")] string planId)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var outcome = GuardedWrites.Confirm(Governance.PlanId.Parse(planId), DateTime.UtcNow);

                return new ResponseMessage
                {
                    Message = outcome.IsApplied
                        ? $"Applied plan '{planId}': {outcome.Result}"
                        : $"Plan '{planId}' was not applied: {outcome.Detail}",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = outcome.IsApplied,
                        ["outcome"] = outcome.Kind.ToString()
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to apply plan '{planId}'");
            }
        }
    }
}
