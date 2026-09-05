using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// User-defined types: reading them, describing them, exporting and importing them.
    ///
    /// The mirror of PortalBlocks, and deliberately not merged with it: a UDT has no programming
    /// language and no memory layout, and the two share not one property.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Describes one user-defined type of a PLC program.</summary>
        /// <param name="softwarePath">Path to the PLC software in the project.</param>
        /// <param name="typePath">Full path to the type, <c>Group/Subgroup/Name</c>.</param>
        /// <returns>The description, or null when there is no such type.</returns>
        /// <exception cref="PortalException">The name is not a valid filter.</exception>
        public TypeDescription? GetType(string softwarePath, string typePath)
        {
            var type = FindType(softwarePath, typePath);

            return type == null ? null : TypeDescriber.Describe(type, GetTypePath(type));
        }

        /// <summary>Describes the user-defined types of a PLC program, filtered by name.</summary>
        /// <param name="softwarePath">Path to the PLC software in the project.</param>
        /// <param name="regexName">The name filter, or empty for every type.</param>
        /// <returns>One description per matching type, in the order the program lists them.</returns>
        /// <exception cref="PortalException">The filter is not a valid expression.</exception>
        public IReadOnlyList<TypeDescription> GetTypes(string softwarePath, string regexName = "")
        {
            return DescribeTypes(FindTypes(softwarePath, regexName));
        }

        /// <summary>Describes a set of types, each with the path it was found at.</summary>
        /// <param name="types">The types to describe.</param>
        /// <returns>One description per type, in the order given.</returns>
        private List<TypeDescription> DescribeTypes(IEnumerable<PlcType> types)
        {
            var described = new List<TypeDescription>();
            foreach (var type in types)
            {
                described.Add(TypeDescriber.Describe(type, GetTypePath(type)));
            }

            return described;
        }

        private PlcType? FindType(string softwarePath, string typePath)
        {
            _logger?.LogInformation($"Getting type by path: {typePath}");

            if (IsProjectNull())
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var typeGroup = plcSoftware?.TypeGroup;

                if (typeGroup != null)
                {
                    var path = typePath.Contains("/") ? typePath.Substring(0, typePath.LastIndexOf("/")) : string.Empty;
                    var regexName = typePath.Contains("/") ? typePath.Substring(typePath.LastIndexOf("/") + 1) : typePath;

                    PlcType? type = null;

                    var group = GetPlcTypeGroupByPath(softwarePath, path);
                    if (group != null)
                    {
                        if (regexName.IndexOfAny(_regexChars) >= 0)
                        {
                            // Refused rather than returned as null: an invalid filter is not the
                            // same answer as "there is no such type".
                            var filter = NameFilter.Parse(regexName);

                            type = group.Types.FirstOrDefault(t => filter.Matches(t.Name)) as PlcType;
                        }
                        else
                        {
                            type = group.Types.FirstOrDefault(t => t.Name.Equals(regexName, StringComparison.OrdinalIgnoreCase));
                        }

                        return type;
                    }
                }
            }

            return null;
        }

        /// <remarks>
        /// The type half of <see cref="GetBlockPath"/>, and the same reasoning applies: the root of
        /// the UDT tree is a <see cref="PlcTypeSystemGroup"/> called "PLC data types", which is not
        /// the first segment of any path a tool accepts.
        /// </remarks>
        private string GetTypePath(PlcType type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.Parent is PlcTypeGroup parentGroup)
            {
                var groupPath = GetUserTypeGroupPath(parentGroup);
                return string.IsNullOrEmpty(groupPath) ? type.Name : $"{groupPath}/{type.Name}";
            }

            return type.Name;
        }

        /// <remarks>
        /// Walks up from a group to the root, dropping the system group at the top.
        /// </remarks>
        private string GetUserTypeGroupPath(PlcTypeGroup group)
        {
            var segments = new List<string>();

            PlcTypeGroup? current = group;
            while (current != null && !(current is PlcTypeSystemGroup))
            {
                segments.Insert(0, current.Name);
                current = current.Parent as PlcTypeGroup;
            }

            return string.Join("/", segments);
        }

        private List<PlcType> FindTypes(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting types...");

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<PlcType>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = plcSoftware?.TypeGroup;

                    if (group != null)
                    {
                        GetTypesRecursive(group, list, regexName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting types from {SoftwarePath} with regex {RegexName}", softwarePath, regexName);
            }

            return list;
        }

        public TypeDescription? ExportType(string softwarePath, string typePath, string exportPath, bool preservePath = false)
        {
            _logger?.LogInformation($"Exporting type by path: {typePath}");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                }

                var type = FindType(softwarePath, typePath);

                if (type == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound, "Type not found");
                }

                // TIA Portal never exports inconsistent types
                if (!type.IsConsistent)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Type is inconsistent; TIA Portal does not export inconsistent types.");
                }

                if (preservePath)
                {
                    var groupPath = "";
                    if (type.Parent is PlcTypeGroup parentGroup)
                    {
                        groupPath = GetPlcTypeGroupPath(parentGroup);
                    }

                    exportPath = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{type.Name}.xml");
                }
                else
                {
                    exportPath = Path.Combine(exportPath, $"{type.Name}.xml");
                }

                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                type.Export(new FileInfo(exportPath), ExportOptions.None);

                return TypeDescriber.Describe(type, GetTypePath(type));
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                if (!pex.Data.Contains("softwarePath")) pex.Data["softwarePath"] = softwarePath;
                if (!pex.Data.Contains("typePath")) pex.Data["typePath"] = typePath;
                if (!pex.Data.Contains("exportPath")) pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportType failed for {SoftwarePath} {TypePath} -> {ExportPath}", softwarePath, typePath, exportPath);
                throw pex;
            }
        }

        public bool ImportType(string softwarePath, string groupPath, string importPath)
        {
            _logger?.LogInformation($"Importing type from path: {importPath}");

            var success = false;

            if (IsProjectNull())
            {
                return success;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var typeGroup = plcSoftware?.TypeGroup;

                if (typeGroup != null)
                {
                    var group = GetPlcTypeGroupByPath(softwarePath, groupPath);
                    if (group == null)
                    {
                        return false;
                    }

                    try
                    {
                        // Correct the argument type by using FileInfo instead of FileStream  
                        var fileInfo = new FileInfo(importPath);
                        if (fileInfo.Exists)
                        {
                            var list = group.Types.Import(fileInfo, ImportOptions.Override);
                            if (list != null && list.Count > 0)
                            {
                                return true;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }

            return success;
        }

        public IReadOnlyList<TypeDescription>? ExportTypes(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
            _logger?.LogInformation("Exporting types...");

            if (IsProjectNull())
            {
                return null;
            }

            var exportList = new List<PlcType>();
            var failures = new List<string>();

            PlcType[] list;

            try
            {
                list = FindTypes(softwarePath, regexName).ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve type list for {SoftwarePath}", softwarePath);
                return DescribeTypes(exportList);
            }

            for (int i = 0; i < list.Count(); i++)
            {
                var type = list[i];

                _logger?.LogDebug("- Exporting type {Index}/{Total} : {Name}", i, list.Count(), type.Name);

                string path;
                if (preservePath)
                {
                    var groupPath = "";
                    if (type.Parent is PlcTypeGroup parentGroup)
                    {
                        groupPath = GetPlcTypeGroupPath(parentGroup);
                    }
                    path = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{type.Name}.xml");
                }
                else
                {
                    path = Path.Combine(exportPath, $"{type.Name}.xml");
                }

                try
                {
                    if (!type.IsConsistent)
                    {
                        _logger?.LogWarning("Skipping inconsistent type {Name}", type.Name);
                        continue;
                    }

                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Delete(path);
                        }
                        catch (Exception ioEx)
                        {
                            failures.Add($"{type.Name}: cannot delete existing file ({ioEx.Message})");
                            _logger?.LogError(ioEx, "Delete failed for {File}", path);
                            continue;
                        }
                    }

                    try
                    {
                        type.Export(new FileInfo(path), ExportOptions.None);
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{type.Name}: export failed ({ex.Message})");
                        _logger?.LogError(ex, "Export failed for type {Type}", type.Name);
                        continue;
                    }

                    exportList.Add(type);
                }
                catch (Exception ex)
                {
                    failures.Add($"{type.Name}: unexpected exception ({ex.Message})");
                    _logger?.LogError(ex, "Unexpected error at type {Type}", type.Name);
                }
            }

            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ExportTypes completed with {failures.Count} failures out of {list.Count()}. First failure: {failures[0]}");
            }
            else
            {
                _logger?.LogInformation($"ExportTypes completed successfully. Exported {exportList.Count} types.");
            }

            return DescribeTypes(exportList);
        }
    }
}
