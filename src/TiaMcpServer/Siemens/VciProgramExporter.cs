using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
using Siemens.Engineering.VersionControl;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Vci;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Exports a PLC program's blocks, PLC data types and tag tables to a VCI workspace, one file each, in folders that
    /// mirror the project.
    /// </summary>
    /// <remarks>
    /// One object at a time, as V21 refuses the program as a whole ("The Object {0} is not supported", measured
    /// 2026-11-13). An object already mapped is left alone, so exporting again adds what is new and changes nothing
    /// else; a block or type that does not compile is reported rather than exported, as Openness refuses it ("not
    /// consistent"). SimaticML, which the workspace offered for blocks and tag tables alike.
    /// </remarks>
    internal sealed class VciProgramExporter
    {
        private const string FileFormat = "SimaticML";

        private readonly Workspace _workspace;
        private readonly List<string> _exported = new List<string>();
        private readonly List<string> _alreadyMapped = new List<string>();
        private readonly List<string> _inconsistent = new List<string>();
        private readonly List<string> _unsupported = new List<string>();

        private VciProgramExporter(Workspace workspace)
        {
            _workspace = workspace;
        }

        /// <summary>Exports every block, type and tag table of the program that is not mapped yet.</summary>
        /// <param name="workspace">The workspace to export to.</param>
        /// <param name="software">The program.</param>
        /// <returns>What happened to each object.</returns>
        internal static VciExportReport Export(Workspace workspace, PlcSoftware software)
        {
            var exporter = new VciProgramExporter(workspace);
            var root = new[] { VciWorkspacePath.SafeName(software.Name) };

            exporter.ExportBlocks(software.BlockGroup, root.Append(VciWorkspacePath.Blocks).ToList());
            exporter.ExportTypes(software.TypeGroup, root.Append(VciWorkspacePath.Types).ToList());
            exporter.ExportTagTables(software.TagTableGroup, root.Append(VciWorkspacePath.Tags).ToList());

            return new VciExportReport(exporter._exported, exporter._alreadyMapped, exporter._inconsistent, exporter._unsupported);
        }

        private void ExportBlocks(PlcBlockGroup group, IReadOnlyList<string> folder)
        {
            foreach (var block in group.Blocks)
            {
                ExportOne(block, block.Name, block.IsConsistent, folder);
            }

            foreach (var child in group.Groups)
            {
                ExportBlocks(child, folder.Append(VciWorkspacePath.SafeName(child.Name)).ToList());
            }
        }

        private void ExportTypes(PlcTypeGroup group, IReadOnlyList<string> folder)
        {
            foreach (var type in group.Types)
            {
                ExportOne(type, type.Name, type.IsConsistent, folder);
            }

            foreach (var child in group.Groups)
            {
                ExportTypes(child, folder.Append(VciWorkspacePath.SafeName(child.Name)).ToList());
            }
        }

        private void ExportTagTables(PlcTagTableGroup group, IReadOnlyList<string> folder)
        {
            foreach (var table in group.TagTables)
            {
                ExportOne(table, table.Name, true, folder);
            }

            foreach (var child in group.Groups)
            {
                ExportTagTables(child, folder.Append(VciWorkspacePath.SafeName(child.Name)).ToList());
            }
        }

        private void ExportOne(IEngineeringObject item, string name, bool isConsistent, IReadOnlyList<string> folder)
        {
            var fileName = VciWorkspacePath.SafeName(name);
            var file = string.Join("/", folder.Append(fileName + ".xml"));

            if (_workspace.MappedObjects.Find(item) != null)
            {
                _alreadyMapped.Add(file);
                return;
            }

            if (!isConsistent)
            {
                _inconsistent.Add(file);
                return;
            }

            if (!_workspace.GetSupportedFileFormats(item).Contains(FileFormat))
            {
                _unsupported.Add(file);
                return;
            }

            // The folder must exist: the probe that first took '\PLC_1\Blocks' had made it the try before, and the
            // Openness tests on a new workspace were refused "Relative Directory Path is Invalid" (2026-11-13).
            Directory.CreateDirectory(Path.Combine(new[] { _workspace.RootPath.FullName }.Concat(folder).ToArray()));

            // A DirectoryInfo keeps the text it was built from, and that text is what Openness reads (VciWorkspacePath).
            _workspace.ExportObject(item, new DirectoryInfo(VciWorkspacePath.ForOpenness(folder)), fileName, FileFormat);
            _exported.Add(file);
        }
    }
}
