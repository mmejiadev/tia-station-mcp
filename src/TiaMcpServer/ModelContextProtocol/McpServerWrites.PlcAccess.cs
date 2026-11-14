using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// A PLC without passwords, the way the class uses it. Part of McpServerWrites. AddDevice does the same to
    /// every PLC it adds; this tool is for a PLC that was already there.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "SetPlcFullAccess"), Description("Leave a PLC the way the class uses it, without passwords: its confidential configuration data not protected, and full access with no password. On TIA Portal V21 a new S7-1500 is created asking for both passwords and its hardware does not compile until they are set or this is done ('Password for confidential PLC configuration data is not configured', 'the password for full access must be set'); AddDevice already does it for every PLC it adds. Give the PLC as CompileHardware takes it, e.g. 'PLC_1/PLC_1' for a station AddDevice made, or a device item holding PLCs. A PLC whose confidential data has a password is refused, as removing it needs that password. Each PLC's parameters and protection are recorded to the backup registry first. An S7-1200 has no access level, its access going through users and roles: after its confidential data it still needs some user with full access, so the project's Anonymous user is activated and given the 'PLC administrator' role — which reaches EVERY PLC of the project whose access goes through users and roles; its state before is recorded too, and the policy has to allow 'project' as well as the PLC. Compile the hardware afterwards. Not for a PLC that runs a real machine: that one keeps its protection.")]
        public static ResponseMessage SetPlcFullAccess(
            [Description("deviceItemPath: the PLC's device item, e.g. 'PLC_1/PLC_1'")] string deviceItemPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = OpennessGate.Enter();

            try
            {
                // A PLC on users and roles changes the project's Anonymous user, which reaches every such PLC of the
                // project: the policy has to allow the project as well as the PLC.
                var plcTarget = ChangeTarget.Program(deviceItemPath);
                var target = GuardedWrites.TargetToPropose(plcTarget, ChangeTarget.Project);
                var backupDirectory = Backups.Allocate("SetPlcFullAccess", plcTarget);
                var request = new Governance.ChangeRequest("SetPlcFullAccess", target, "confidential data not protected, full access without a password; for a PLC on users and roles, the project's Anonymous user gets PLC administrator")
                    .WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribePlcAccess(Portal.SetPlcFullAccess(deviceItemPath, backupDirectory), "Compile the hardware, then download, for it to reach the PLC."),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"leaving '{deviceItemPath}' at full access");
            }
        }

        /// <remarks>
        /// Only in Study Mode, the class's: in Workshop Mode the PLC may be one that runs a machine, and a station added
        /// there keeps the protection TIA Portal gives it. Exhaustive, as anything that decides a write must be.
        /// </remarks>
        private static bool OpensAddedPlcs()
        {
            return ModeGate.Mode switch
            {
                Governance.OperationMode.Study => true,
                Governance.OperationMode.Workshop => false,
                _ => throw new McpException($"Unrecognised operation mode: {ModeGate.Mode}", McpErrorCode.InternalError)
            };
        }

        /// <remarks>
        /// In Study Mode an added S7-1200 changes the project's Anonymous user, which reaches every PLC of the project on
        /// users and roles, so the policy has to allow the project as well as the new station.
        /// </remarks>
        private static string TargetOfAddDevice(string stationTarget, bool opensPlcs)
        {
            return opensPlcs ? GuardedWrites.TargetToPropose(stationTarget, ChangeTarget.Project) : stationTarget;
        }

        /// <remarks>
        /// The station was added whatever happens to its PLC's protection, so this answers that it was, with a warning
        /// when the protection could not be changed. Throwing instead would record the AddDevice plan as failed in the
        /// audit trail while the station is in the project, and have the caller add it again under a taken name. What
        /// happened goes in the meta as well as the text, so a client can tell without reading prose.
        /// </remarks>
        private static ResponseMessage DescribeAddedDevice(string added, string typeIdentifier, bool opensPlcs, string backupDirectory)
        {
            if (!opensPlcs)
            {
                return DescribeAddedStation(added, typeIdentifier);
            }

            try
            {
                var opened = Portal.SetFullAccessOfAddedDevice(added, backupDirectory);
                var response = DescribeAddedStation(added, typeIdentifier, opened.Plcs.Count == 0 ? string.Empty : $". {DescribePlcAccessChanges(opened)}");

                response.Meta!["plcs"] = PlcAccessJson(opened.Plcs);
                response.Meta["anonymousUser"] = opened.AnonymousUser;

                return response;
            }
            catch (PortalException pex)
            {
                Logger?.LogWarning(pex, "Device {Device} was added; leaving its PLC without passwords failed", added);
                var response = DescribeAddedStation(added, typeIdentifier, $". Warning: leaving its PLC without passwords failed ({pex.Message}); use SetPlcFullAccess on it, or set the passwords in TIA Portal");

                response.Meta!["plcAccessWarning"] = pex.Message;

                return response;
            }
        }

        private static ResponseMessage DescribePlcAccess(PlcAccessReport report, string next)
        {
            return new ResponseMessage
            {
                Message = $"{DescribePlcAccessChanges(report)}. {next}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["plcs"] = PlcAccessJson(report.Plcs),
                    ["anonymousUser"] = report.AnonymousUser
                }
            };
        }

        private static string DescribePlcAccessChanges(PlcAccessReport report)
        {
            var each = report.Plcs.Select(change =>
                $"'{change.PlcName}' at {change.After.AccessLevel} with confidential data {change.After.ConfidentialData} "
                + $"(was {change.Before.AccessLevel}, {change.Before.ConfidentialData})");
            var users = report.AnonymousUser.Length == 0 ? string.Empty : "; " + report.AnonymousUser;

            return "Left without passwords: " + string.Join("; ", each) + users;
        }

        private static JsonArray PlcAccessJson(IReadOnlyList<PlcAccessChange> changes)
        {
            return new JsonArray(changes.Select(change => (JsonNode)new JsonObject
            {
                ["plc"] = change.PlcName,
                ["accessLevelBefore"] = change.Before.AccessLevel,
                ["accessLevel"] = change.After.AccessLevel,
                ["confidentialDataBefore"] = change.Before.ConfidentialData,
                ["confidentialData"] = change.After.ConfidentialData
            }).ToArray());
        }
    }
}
