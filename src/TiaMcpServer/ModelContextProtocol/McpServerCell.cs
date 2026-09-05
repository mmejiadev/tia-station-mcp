using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Expanding a cell specification into SCL.
    ///
    /// The server knows nothing about any particular cell: the specification is a JSON file and
    /// the pattern is a template, both outside the code. This tool only puts them together.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "ExpandCellScl"), Description("Generate the SCL for a cell from its specification in spec/cells/ and the patterns in spec/patterns/. Returns the source without writing anything; pass Scl to WriteScl to put it in a project, then CompileSoftware. Station pattern first, coordinator second, because the coordinator instantiates the station.")]
        public static ResponseCellScl ExpandCellScl(
            [Description("cellPath: path to the cell specification, e.g. 'spec/cells/two-station-demo.json'")] string cellPath,
            [Description("patternDirectory: where station.scl.tmpl and coordinator.scl.tmpl live, default 'spec/patterns'")] string patternDirectory = "spec/patterns",
            [Description("includeEntryPoint: also generate the cell's instance data block and a Main OB that calls it every scan. Needed for the cell to run at all, and for a tag write to reach it — but it REPLACES the project's existing Main. False by default.")] bool includeEntryPoint = false)
        {
            // No Openness gate, deliberately. This reads two text files and does string work; it
            // never touches TIA Portal, so taking the gate would queue it behind a running compile
            // for no reason. See OpennessGate: only the tools that reach the portal take it.
            try
            {
                var cell = Spec.CellSpecificationFile.Load(cellPath);

                var stationPattern = Spec.SclTemplateExpander.Expand(ReadPattern(patternDirectory, "station.scl.tmpl"), cell);
                var coordinator = Spec.SclTemplateExpander.Expand(ReadPattern(patternDirectory, "coordinator.scl.tmpl"), cell);
                var scl = stationPattern + Environment.NewLine + coordinator;

                if (includeEntryPoint)
                {
                    // Last, and it has to be: the data block is an instance of the coordinator, so
                    // a source declaring it before the block it instantiates does not compile.
                    scl += Environment.NewLine + Spec.SclTemplateExpander.Expand(ReadPattern(patternDirectory, "main.scl.tmpl"), cell);
                }

                return new ResponseCellScl(cell.Name, cell.Stations.Select(item => item.Name).ToList(), scl)
                {
                    Message =
                        $"Generated SCL for cell {cell.Name} with {cell.Stations.Count} station(s)"
                        + (includeEntryPoint ? ", including a Main OB that replaces the project's. " : ". ")
                        + "Nothing has been written; pass Scl to WriteScl, then CompileSoftware.",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["stationCount"] = cell.Stations.Count,
                        ["handoverCount"] = cell.Handovers().Count
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw ToMcpException(pex, $"Failed to generate SCL for the cell at {cellPath}");
            }
            catch (ArgumentException ex)
            {
                // The specification types refuse invalid input with ArgumentException. Reported as
                // bad parameters rather than an internal error: it is the given file that is wrong,
                // and reporting a failure would invite a retry of a typo.
                throw new McpException(
                    $"The cell at {cellPath} cannot be used: {ex.Message}", ex, McpErrorCode.InvalidParams);
            }
        }

        private static string ReadPattern(string patternDirectory, string pattern)
        {
            var path = Path.Combine(patternDirectory ?? string.Empty, pattern);

            if (!File.Exists(path))
            {
                throw new TiaMcpServer.Siemens.PortalException(
                    TiaMcpServer.Siemens.PortalErrorCode.InvalidParams,
                    $"No pattern at {path}. The cell patterns ship in spec/patterns/.");
            }

            return File.ReadAllText(path);
        }
    }
}
