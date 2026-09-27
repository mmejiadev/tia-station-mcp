using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <remarks>
    /// Reading and exporting user-defined types.
    /// </remarks>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetTypeInfo"), Description("Get a type info from the plc software")]
        public static ResponseTypeInfo GetTypeInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: defines the path in the project structure to the type")] string typePath)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var type = Portal.GetType(softwarePath, typePath);
                if (type != null)
                {
                    var info = Describe(type);
                    info.Message = $"Type info retrieved from '{typePath}' in '{softwarePath}'";
                    info.Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    };

                    return info;
                }
                else
                {
                    throw new McpException($"Type not found at '{typePath}' in '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving type info from '{typePath}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetTypes"), Description("Get a list of types from the plc software")]
        public static ResponseTypes GetTypes(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "")
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var list = Portal.GetTypes(softwarePath, regexName);

                var responseList = DescribeTypes(list);

                if (list != null)
                {
                    return new ResponseTypes
                    {
                        Message = $"Types with regex '{regexName}' retrieved from '{softwarePath}'",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving user defined types with regex '{regexName}' in '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw ToolFailure(ex, $"retrieving user defined types with regex '{regexName}' in '{softwarePath}'");
            }
        }

        [McpServerTool(Name = "ExportType"), Description("Export a type from the plc software")]
        public static ResponseExportType ExportType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where export the type")] string exportPath,
            [Description("typePath: defines the path in the project structure to the type")] string typePath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            // One Openness call at a time. See OpennessGate: two of them really do interleave.
            using var openness = TiaMcpServer.Siemens.OpennessGate.Enter();

            try
            {
                var type = Portal.ExportType(softwarePath, typePath, exportPath, preservePath);
                if (type != null)
                {
                    return new ResponseExportType
                    {
                        Message = $"Type exported from '{typePath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting type from '{typePath}' to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException("Type not found.", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export type.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";
                            Logger?.LogError(pex, "MCP ExportType failed for {SoftwarePath} {TypePath} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["typePath"], pex.Data?["exportPath"]);
                            throw new McpException(msg, McpErrorCode.InternalError);
                        }
                }
                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting type from '{typePath}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportTypes"), Description("Export types from the plc software to path")]
        public static async Task<ResponseExportTypes> ExportTypes(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the types")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var call = new BulkCall(softwarePath, exportPath, regexName);
            var progress = ProgressReporter.For(server, context, Logger);

            try
            {
                Logger?.LogInformation($"Starting export of types from '{softwarePath}' to '{exportPath}'");

                var allTypes = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.GetTypes(softwarePath, regexName)));

                if (allTypes == null || allTypes.Count == 0)
                {
                    await progress.ReportAsync(0, 0, "No types found to export");
                    return NoTypesExported(call);
                }

                await progress.ReportAsync(0, allTypes.Count, $"Starting export of {allTypes.Count} types...");

                var exportedTypes = await Task.Run(() => TiaMcpServer.Siemens.OpennessGate.Run(() => Portal.ExportTypes(softwarePath, exportPath, regexName, preservePath)))
                    ?? throw new McpException($"Failed exporting types '{regexName}' from '{softwarePath}' to {exportPath}", McpErrorCode.InternalError);

                var response = TypesExported(call, allTypes, exportedTypes);
                await progress.ReportAsync(exportedTypes.Count, allTypes.Count, $"Export completed: {exportedTypes.Count} types exported successfully");
                return response;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                await progress.ReportAsync(0, 0, $"Type export failed: {ex.Message}");
                throw ToolFailure(ex, $"exporting types '{regexName}' from '{softwarePath}' to {exportPath}");
            }
        }

        private static ResponseExportTypes NoTypesExported(BulkCall call)
        {
            return new ResponseExportTypes
            {
                Message = $"No types found with regex '{call.RegexName}' in '{call.SoftwarePath}'",
                Items = new List<ResponseTypeInfo>(),
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["totalTypes"] = 0,
                    ["exportedTypes"] = 0,
                    ["duration"] = call.ElapsedSeconds
                }
            };
        }

        /// <remarks>
        /// Inconsistent types are reported from the list read before the export, not from what it
        /// returned: SimaticML export skips them, so they are exactly the ones missing from it.
        /// </remarks>
        private static ResponseExportTypes TypesExported(BulkCall call, IReadOnlyList<TypeDescription> allTypes, IReadOnlyList<TypeDescription> exportedTypes)
        {
            var items = DescribeTypes(exportedTypes);
            var inconsistent = DescribeTypes(allTypes.Where(type => !type.IsConsistent));
            Logger?.LogInformation($"Type export completed: {items.Count} types exported in {call.ElapsedSeconds:F2} seconds");

            return new ResponseExportTypes
            {
                Message = $"Export completed: {items.Count} types with regex '{call.RegexName}' exported from '{call.SoftwarePath}' to '{call.Directory}'",
                Items = items,
                Inconsistent = inconsistent,
                Meta = new JsonObject
                {
                    ["timestamp"] = DateTime.Now,
                    ["success"] = true,
                    ["totalTypes"] = allTypes.Count,
                    ["exportedTypes"] = items.Count,
                    ["inconsistentTypes"] = inconsistent.Count,
                    ["duration"] = call.ElapsedSeconds
                }
            };
        }

        private static List<ResponseTypeInfo> DescribeTypes(IEnumerable<TypeDescription>? types)
        {
            var described = new List<ResponseTypeInfo>();

            if (types == null)
            {
                return described;
            }

            foreach (var type in types)
            {
                described.Add(Describe(type));
            }

            return described;
        }

        private static ResponseTypeInfo Describe(TypeDescription type)
        {
            return new ResponseTypeInfo
            {
                Name = type.Name,
                Path = type.Path,
                TypeName = type.TypeName,
                Namespace = type.Namespace,
                IsConsistent = type.IsConsistent,
                ModifiedDate = type.ModifiedDate,
                IsKnowHowProtected = type.IsKnowHowProtected,
                Attributes = type.Attributes,
                Description = type.Description
            };
        }
    }
}
