using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Writing SCL source into the program.
    ///
    /// Part of McpServerWrites: every tool here changes something and goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "WriteScl"), Description("Write SCL source into a PLC program, generating the blocks it declares. The existing blocks are exported to the backup registry first, because generation overwrites blocks of the same name; call ListBackups to find that copy. Compile afterwards to find out whether the code is valid.")]
        public static ResponseWriteScl WriteScl(
            [Description("softwarePath: full path in the project structure to the plc software, e.g. 'Group1/PLC_1'")] string softwarePath,
            [Description("sclCode: the SCL source text; it may declare more than one block")] string sclCode)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(softwarePath);
                var backupDirectory = Backups.Allocate("WriteScl", target);
                var request = new Governance.ChangeRequest("WriteScl", target, Summarise(sclCode))
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var generated = Portal.WriteScl(softwarePath, sclCode, backupDirectory);

                        return new ResponseWriteScl(generated)
                        {
                            Message = $"Generated {generated.Count} block(s): {string.Join(", ", generated)}. Compile the software to check them.",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    },
                    () => new ResponseWriteScl(Array.Empty<string>()));
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed writing SCL into '{softwarePath}'");
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error writing SCL into '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        /// <summary>
        /// Shortens a value so the audit trail stays readable.
        /// </summary>
        /// <remarks>
        /// A whole SCL source would drown every other line of the trail, and the trail's job is to
        /// be read. The first line names the block being written, which is what someone scanning it
        /// is looking for; the source itself is in the backup the same change took.
        /// </remarks>
        private static string Summarise(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var firstLine = value.Split('\n')[0].Trim();

            return firstLine.Length <= SummaryLength ? firstLine : firstLine.Substring(0, SummaryLength) + "...";
        }
    }
}
