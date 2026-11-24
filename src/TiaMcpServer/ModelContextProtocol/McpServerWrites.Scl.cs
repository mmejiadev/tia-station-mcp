using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Scl;
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
        [McpServerTool(Name = "WriteScl"), Description("Write SCL source into a PLC program, generating the blocks and PLC data types (TYPE) it declares, reported apart. PEEK and POKE calls with constants come back in meta.memoryAccess (rule, isError, line, message), measured in PLCSIM Advanced: errors compile and fail quietly when run (an optimized DB by number, a DB that does not exist, a DB number outside the DB area, an unknown area); warnings may not do what they seem (an offset past 65535, a write to outputs, a peripheral area the instruction's help does not list, GET_ERR_ID where ENO is what reports the error). The findings that need only the text are also named in the change plan, before anything is written. The existing blocks are exported to the backup registry first, because generation overwrites blocks of the same name; call ListBackups to find that copy. Compile afterwards to find out whether the code is valid.")]
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
                var fromText = SclMemoryAccessRules.CheckText(sclCode);
                var request = new Governance.ChangeRequest("WriteScl", target, Summarise(sclCode) + SclMemoryAccessSummary.ForPlan(fromText))
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var generated = Portal.WriteScl(softwarePath, sclCode, backupDirectory);

                        var (memoryAccess, checkFailure) = CheckMemoryAccess(softwarePath, sclCode);

                        return new ResponseWriteScl(generated.Blocks, generated.Types)
                        {
                            Message = $"Generated {generated.Blocks.Count} block(s): {string.Join(", ", generated.Blocks)}; {generated.Types.Count} type(s): {string.Join(", ", generated.Types)}. Compile the software to check them."
                                + DescribeMemoryAccess(memoryAccess),
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["memoryAccess"] = new JsonArray(memoryAccess.Select(ToJson).ToArray()),
                                ["memoryAccessCheckFailed"] = checkFailure
                            }
                        };
                    },
                    () => new ResponseWriteScl(Array.Empty<string>(), Array.Empty<string>()));
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

        /// <summary>Checks the PEEK and POKE calls of SCL just written, without letting the check fail the write.</summary>
        /// <remarks>
        /// After the write, so the blocks just generated count: a PEEK of a DB this source makes optimized, or creates, is
        /// judged on what the program then holds. The rules that need only the text also ran before the write, into the plan
        /// a supervisor confirms; they run again here so the answer lists every finding in one place. The check only
        /// advises, and the blocks are already written: a failure to read the project is reported beside the write, not as
        /// its failure, which would tell the caller to write again.
        /// </remarks>
        private static (IReadOnlyList<SclFinding> Findings, string? Failure) CheckMemoryAccess(string softwarePath, string sclCode)
        {
            try
            {
                return (Portal.CheckSclMemoryAccess(softwarePath, sclCode), null);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                return (Array.Empty<SclFinding>(), pex.Message);
            }
        }

        /// <summary>Words the PEEK and POKE findings for the message: errors fail quietly when run, warnings may not do what they seem.</summary>
        private static string DescribeMemoryAccess(IReadOnlyList<SclFinding> findings)
        {
            var errors = findings.Where(finding => finding.IsError).ToList();
            var warnings = findings.Where(finding => !finding.IsError).ToList();

            return (errors.Count == 0 ? string.Empty : $" PEEK/POKE that compile and fail quietly when run: {string.Join("; ", errors)}.")
                + (warnings.Count == 0 ? string.Empty : $" PEEK/POKE to look at: {string.Join("; ", warnings)}.");
        }

        private static JsonNode ToJson(SclFinding finding)
        {
            return new JsonObject
            {
                ["rule"] = finding.Rule,
                ["isError"] = finding.IsError,
                ["line"] = finding.Line,
                ["message"] = finding.Message
            };
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
