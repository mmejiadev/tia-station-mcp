using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Hmi;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Classic WinCC panels, written: screens, HMI tag tables and text lists imported from XML, and
    /// HMI tags generated from a PLC program.
    ///
    /// Part of McpServerWrites: every tool here changes something and goes through the guard.
    /// </remarks>
    public static partial class McpServer
    {
        private const string CompileTheHmi = "Run CompileSoftware on the HMI path: compiling is what checks that every linked tag, screen and function exists.";

        [McpServerTool(Name = "ImportHmiScreen"), Description("Import a screen into a classic WinCC panel from XML, in the shape ExportHmi writes. A screen of the same name is replaced, so the whole panel is exported to the backup registry first; if any of it cannot be saved, nothing is imported. Items link to tags, screens, text lists and connections by name; a link to something the panel lacks is refused before importing, because one can end the TIA Portal session. Run CompileSoftware on the HMI path afterwards.")]
        public static ResponseMessage ImportHmiScreen(
            [Description("hmiPath: full path to the HMI target, e.g. 'HMI_1'")] string hmiPath,
            [Description("folderPath: screen folder to import into, as GetHmiInfo shows it, or empty for the root")] string folderPath,
            [Description("importPath: the screen's XML file")] string importPath)
        {
            return RunHmiImport(
                "ImportHmiScreen",
                ChangeTarget.Program(hmiPath, folderPath),
                importPath,
                backupDirectory => Portal.ImportHmiScreen(hmiPath, folderPath, importPath, backupDirectory));
        }

        [McpServerTool(Name = "ImportHmiTagTable"), Description("Import an HMI tag table into a classic WinCC panel from XML, in the shape ExportHmi writes. A table of the same name is replaced whole, so the whole panel is exported to the backup registry first; if any of it cannot be saved, nothing is imported. To give PLC memory tags an HMI tag, GenerateHmiTags writes the table for you. Run CompileSoftware on the HMI path afterwards.")]
        public static ResponseMessage ImportHmiTagTable(
            [Description("hmiPath: full path to the HMI target, e.g. 'HMI_1'")] string hmiPath,
            [Description("importPath: the tag table's XML file")] string importPath)
        {
            return RunHmiImport(
                "ImportHmiTagTable",
                ChangeTarget.Program(hmiPath),
                importPath,
                backupDirectory => Portal.ImportHmiTagTable(hmiPath, importPath, backupDirectory));
        }

        [McpServerTool(Name = "ImportHmiTextList"), Description("Import a text list into a classic WinCC panel from XML, in the shape ExportHmi writes. A list of the same name is replaced, so the whole panel is exported to the backup registry first; if any of it cannot be saved, nothing is imported. Run CompileSoftware on the HMI path afterwards.")]
        public static ResponseMessage ImportHmiTextList(
            [Description("hmiPath: full path to the HMI target, e.g. 'HMI_1'")] string hmiPath,
            [Description("importPath: the text list's XML file")] string importPath)
        {
            return RunHmiImport(
                "ImportHmiTextList",
                ChangeTarget.Program(hmiPath),
                importPath,
                backupDirectory => Portal.ImportHmiTextList(hmiPath, importPath, backupDirectory));
        }

        /// <remarks>The three imports differ only in what they call; the guard, the backup and the answer are one path.</remarks>
        private static ResponseMessage RunHmiImport(string tool, string target, string importPath, Func<string, IReadOnlyList<string>> import)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var backupDirectory = Backups.Allocate(tool, target);
                var request = new Governance.ChangeRequest(tool, target, importPath).WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        var imported = import(backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"Imported into '{target}': {string.Join(", ", imported)}. {CompileTheHmi}",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true,
                                ["imported"] = string.Join(", ", imported)
                            }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"running {tool} with '{importPath}' on '{target}'");
            }
        }

        [McpServerTool(Name = "GenerateHmiScreen"), Description("Generate a screen on a classic WinCC panel from a JSON description, in the style TIA Portal gives new items (the class projects' look), replacing a screen of the same name. Description: {\"name\": \"principal\", \"number\": 2, \"backColor\": \"white\", \"items\": [...]}; number is optional, and a free one is chosen without it. Item types: button (text; press + tag/value/screen, or events: [{event: press|click, function, tag, value, screen}] with functions setBitWhilePressed, setTag, activateScreen, activatePreviousScreen, activateScreenByNumber), ioField (tag, mode InOutput/Output/Input, format like '9999'), symbolicField (tag, textList, mode), text (text), pilotLight (tag, offColor, onColor, flashing, visibleTag), bar (tag, minimum, maximum). Every item needs left and top and takes width, height, name, backColor, foreColor, fontSize, align and animations: [{type: appearance, tag, ranges: [{from, to, backColor, foreColor, flashing}]}, {type: visibility, tag, bit, visible}]. Colours are 'R, G, B', '#RRGGBB' or red/green/grey/... Everything it links to must already be on the panel - HMI tags (GenerateHmiTags), text lists (GenerateHmiTextList), and the screens its buttons open, so generate a target screen first and add the way back afterwards - or nothing is written: a link to something missing can end the TIA Portal session. The panel is exported to the backup registry first. Run CompileSoftware on the HMI path afterwards, and CompareHmiScreens against a reference export to check it.")]
        public static ResponseMessage GenerateHmiScreen(
            [Description("hmiPath: full path to the HMI target, e.g. 'HMI_1'")] string hmiPath,
            [Description("folderPath: screen folder to put it in, as GetHmiInfo shows it, or empty for the root")] string folderPath,
            [Description("description: the screen as JSON, see the tool description")] string description)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a description that does not parse is a mistake in the call,
                // not a change anyone needs to approve or audit.
                var screen = HmiScreenDescriptionReader.Parse(description);
                var target = ChangeTarget.Program(hmiPath, folderPath);
                var backupDirectory = Backups.Allocate("GenerateHmiScreen", target);
                var request = new Governance.ChangeRequest("GenerateHmiScreen", target, screen.Name).WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        Portal.GenerateHmiScreen(hmiPath, folderPath, screen, backupDirectory);

                        return DescribeScreenGeneration(screen);
                    },
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"generating a screen on '{hmiPath}'");
            }
        }

        private static ResponseMessage DescribeScreenGeneration(HmiScreenDescription screen)
        {
            return new ResponseMessage
            {
                Message = $"Screen '{screen.Name}' generated with {screen.Items.Count} item(s). {CompileTheHmi}",
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["items"] = screen.Items.Count
                }
            };
        }

        [McpServerTool(Name = "GenerateHmiTextList"), Description("Create or replace a text list on a classic WinCC panel: one text per value of a tag, which a symbolicField item of GenerateHmiScreen shows - 'STOPPED' for 0, 'AUTOMATIC' for 1. Description: {\"name\": \"MODES\", \"entries\": [{\"value\": 0, \"text\": \"STOPPED\"}, {\"value\": 1, \"text\": \"AUTOMATIC\"}]}. The panel is exported to the backup registry first.")]
        public static ResponseMessage GenerateHmiTextList(
            [Description("hmiPath: full path to the HMI target, e.g. 'HMI_1'")] string hmiPath,
            [Description("description: the text list as JSON, see the tool description")] string description)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                // Read before the guard: a description that does not parse is a mistake in the call.
                var list = HmiTextListDocument.Build(description);
                var target = ChangeTarget.Program(hmiPath);
                var backupDirectory = Backups.Allocate("GenerateHmiTextList", target);
                var request = new Governance.ChangeRequest("GenerateHmiTextList", target, list.Key).WithBackup(backupDirectory);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () =>
                    {
                        Portal.GenerateHmiTextList(hmiPath, list.Key, list.Value, backupDirectory);

                        return new ResponseMessage
                        {
                            Message = $"Text list '{list.Key}' written to '{hmiPath}'. {CompileTheHmi}",
                            Meta = new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true }
                        };
                    },
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"generating a text list on '{hmiPath}'");
            }
        }

        [McpServerTool(Name = "GenerateHmiTags"), Description("Give every memory tag (%M) of a PLC program an HMI tag on a classic WinCC panel, named as the PLC tag - Marxa gets Marxa - with a 1 s acquisition cycle, all in one HMI tag table. That is how the class projects are built by hand, and it is what a screen's buttons and pilot lights link to. A memory tag that already has an HMI tag in another table is left out, so running it twice changes nothing. The target table is created, or regenerated if it only holds generated tags; a table holding anything else is refused, since replacing it would delete those. The panel is exported to the backup registry first. Run CompileSoftware on the HMI path afterwards.")]
        public static ResponseMessage GenerateHmiTags(
            [Description("plcSoftwarePath: the PLC program whose memory tags are read, e.g. 'PLC_1'")] string plcSoftwarePath,
            [Description("hmiPath: full path to the HMI target, e.g. 'HMI_1'")] string hmiPath,
            [Description("tableName: the HMI tag table to write, e.g. 'PLC tags'")] string tableName,
            [Description("connectionName: the HMI connection to that PLC, as the panel's existing tags use it; default 'HMI_Connection_1'")] string connectionName = HmiTagTableDocument.DefaultConnection)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var target = ChangeTarget.Program(hmiPath);
                var backupDirectory = Backups.Allocate("GenerateHmiTags", target);
                var request = new Governance.ChangeRequest("GenerateHmiTags", target, $"{tableName} from {plcSoftwarePath}")
                    .WithBackup(backupDirectory);
                var generation = new HmiTagGenerationRequest(plcSoftwarePath, hmiPath, tableName, connectionName);

                return GuardedTool.Run(
                    GuardedWrites,
                    request,
                    () => DescribeGeneration(Portal.GenerateHmiTags(generation, backupDirectory), tableName),
                    () => new ResponseMessage());
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"generating HMI tags for '{hmiPath}' from '{plcSoftwarePath}'");
            }
        }

        private static ResponseMessage DescribeGeneration(HmiTagPlan plan, string tableName)
        {
            var written = plan.Links.Count > 0
                ? $"{plan.Links.Count} HMI tag(s) written to '{tableName}'. {CompileTheHmi}"
                : plan.AlreadyPresent.Count > 0
                    ? $"Nothing written: every memory tag already has an HMI tag ({plan.AlreadyPresent.Count})."
                    : "Nothing written: the PLC program has no memory tag (%M). Create the tags the panel should read and write first.";
            var leftOut = plan.AlreadyPresent.Count == 0 || plan.Links.Count == 0
                ? string.Empty
                : $" Left out, already in another table: {string.Join(", ", plan.AlreadyPresent)}.";

            return new ResponseMessage
            {
                Message = written + leftOut,
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["written"] = plan.Links.Count,
                    ["alreadyPresent"] = plan.AlreadyPresent.Count
                }
            };
        }
    }
}
