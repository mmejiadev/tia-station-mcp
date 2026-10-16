using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Hmi;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Classic WinCC panels, read: what one holds, and its export to XML.
    ///
    /// The export is the only way to see what is on a screen. Classic WinCC gives Openness no
    /// object model for screen items, so a button, its events and its animations are read from
    /// the XML — which is also the format the import tools take back.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetHmiInfo"), Description("List what a classic WinCC panel (Basic, Comfort, Advanced) holds: screens, screen templates, HMI tag tables with their tag counts, and text lists, each by full path. Take the HMI path from GetProjectTree, where it shows as 'HmiTarget'. WinCC Unified panels are not supported. To see what is on a screen, use ExportHmi and read the XML.")]
        public static ResponseNetworkTopology GetHmiInfo(
            [Description("hmiPath: full path to the HMI target, e.g. 'HMI_1'")] string hmiPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var summary = Portal.GetHmiInfo(hmiPath);

                var lines = Section("Screen", summary.Screens)
                    .Concat(Section("Template", summary.Templates))
                    .Concat(Section("Tag table", summary.TagTables))
                    .Concat(Section("Text list", summary.TextLists))
                    .ToList();

                return new ResponseNetworkTopology(lines)
                {
                    Message = $"'{hmiPath}': {summary.Screens.Count} screen(s), {summary.TagTables.Count} HMI tag table(s), {summary.TextLists.Count} text list(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to read the HMI at '{hmiPath}'");
            }
        }

        [McpServerTool(Name = "CompareHmiScreens"), Description("Compare two exported classic WinCC screens item by item, attribute by attribute: a reference screen (the teacher's, from ExportHmi) and a candidate (a generated one, exported after GenerateHmiScreen). Items are matched by object name; for each, every attribute, text, event, animation and tag link that differs is listed, and items only one screen has are named. Reads the two files only: TIA Portal is not touched.")]
        public static ResponseNetworkTopology CompareHmiScreens(
            [Description("referencePath: the reference screen's XML file")] string referencePath,
            [Description("candidatePath: the screen to check, as an XML file")] string candidatePath)
        {
            try
            {
                var differences = HmiScreenComparison.Compare(File.ReadAllText(referencePath), File.ReadAllText(candidatePath));

                return new ResponseNetworkTopology(differences.ToList())
                {
                    Message = differences.Count == 0
                        ? "The two screens match attribute for attribute"
                        : $"{differences.Count} difference(s)",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["differences"] = differences.Count
                    }
                };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Xml.XmlException)
            {
                throw new McpException($"Cannot compare '{referencePath}' with '{candidatePath}': {ex.Message}", McpErrorCode.InvalidParams);
            }
        }

        private static IEnumerable<string> Section(string kind, IEnumerable<string> names)
        {
            return names.Select(name => $"{kind} | {name}");
        }

        [McpServerTool(Name = "ExportHmi"), Description("Export everything a classic WinCC panel holds to XML, one file per item: screens, templates, HMI tag tables, text and graphic lists, with folders as subdirectories. This is how to read what is on a screen - its items, their positions, events and animations, linked to tags and screens by name - and the format ImportHmiScreen, ImportHmiTagTable and ImportHmiTextList take back. The directory must be new or empty. Items that could not be exported are listed with the reason; the rest are still written.")]
        public static ResponseNetworkTopology ExportHmi(
            [Description("hmiPath: full path to the HMI target, e.g. 'HMI_1'")] string hmiPath,
            [Description("exportPath: a new or empty directory to write into")] string exportPath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var report = Portal.ExportHmi(hmiPath, exportPath);
                var lines = report.Exported.Concat(report.Failures.Select(failure => "FAILED | " + failure)).ToList();

                return new ResponseNetworkTopology(lines)
                {
                    Message = report.Failures.Count == 0
                        ? $"{report.Exported.Count} file(s) written to '{exportPath}'"
                        : $"{report.Exported.Count} file(s) written to '{exportPath}', {report.Failures.Count} item(s) could not be exported",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = report.Failures.Count == 0,
                        ["exported"] = report.Exported.Count,
                        ["failed"] = report.Failures.Count
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to export the HMI at '{hmiPath}' to '{exportPath}'");
            }
        }
    }
}
