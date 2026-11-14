using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using Siemens.Engineering.VersionControl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TiaMcpServer.Vci;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// TIA Portal's Version Control Interface: a workspace is a folder whose files mirror project objects, which is what
    /// a Git repository can then hold. Measured on V21 (2026-11-13): see <see cref="VciWorkspacePath"/> for how a folder
    /// is spelled, <see cref="VciSyncRule"/> for how the states read, and <see cref="VciProgramExporter"/> for what is
    /// exported. Branches are Git's, not TIA Portal's.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>The VCI workspaces of the open project.</summary>
        /// <returns>Each workspace with its folder and how many objects are mapped to it.</returns>
        /// <exception cref="PortalException">No project is open, or it offers no version control.</exception>
        public IReadOnlyList<VciWorkspaceInfo> GetVciWorkspaces()
        {
            try
            {
                return RequireVci().WorkspaceGroup.Workspaces
                    .Select(workspace => new VciWorkspaceInfo(workspace.Name, workspace.RootPath.FullName, workspace.MappedObjects.Count))
                    .ToList();
            }
            catch (Exception ex)
            {
                throw DecorateVciFailure(ex, string.Empty, "Reading the VCI workspaces");
            }
        }

        /// <summary>How each object mapped to a workspace compares with its file.</summary>
        /// <param name="workspaceName">The workspace.</param>
        /// <returns>Each mapped object's file and state.</returns>
        /// <exception cref="PortalException">No project, or no such workspace.</exception>
        public IReadOnlyList<VciObjectState> GetVciStatus(string workspaceName)
        {
            try
            {
                var workspace = RequireWorkspace(workspaceName);

                return workspace.MappedObjects.Select(mapped => DescribeMapped(workspace, mapped)).ToList();
            }
            catch (Exception ex)
            {
                throw DecorateVciFailure(ex, workspaceName, "Reading the VCI status");
            }
        }

        /// <summary>Creates a VCI workspace in a folder.</summary>
        /// <param name="workspaceName">Its name in the project.</param>
        /// <param name="rootDirectory">The folder its files go in, a full path.</param>
        /// <returns>The workspace.</returns>
        /// <exception cref="PortalException">No project, a name taken, or a folder that is not a full path.</exception>
        /// <remarks>Nothing existing changes: a new workspace and its folder. TIA Portal writes <c>.vci\workspace.vci.config</c> there.</remarks>
        public VciWorkspaceInfo CreateVciWorkspace(string workspaceName, string rootDirectory)
        {
            _logger?.LogInformation("Creating VCI workspace {Workspace} in {Root}...", workspaceName, rootDirectory);

            try
            {
                var name = RequireWorkspaceName(workspaceName);
                var workspaces = RequireVci().WorkspaceGroup.Workspaces;

                if (workspaces.Find(name) != null)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"A VCI workspace called '{name}' already exists; nothing was created");
                }

                if (string.IsNullOrWhiteSpace(rootDirectory) || !Path.IsPathRooted(rootDirectory))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "rootDirectory is the full path of the folder the files go in, e.g. 'C:/Repos/Cell'");
                }

                Directory.CreateDirectory(rootDirectory);
                var created = workspaces.Create(name, new DirectoryInfo(Path.GetFullPath(rootDirectory)));

                return new VciWorkspaceInfo(created.Name, created.RootPath.FullName, created.MappedObjects.Count);
            }
            catch (Exception ex)
            {
                throw DecorateVciFailure(ex, workspaceName, "Creating the VCI workspace");
            }
        }

        /// <summary>Exports a PLC program's blocks, data types and tag tables to a workspace, the ones not mapped yet.</summary>
        /// <param name="workspaceName">The workspace.</param>
        /// <param name="softwarePath">The PLC's software, for example <c>PLC_1</c>.</param>
        /// <returns>What happened to each object.</returns>
        /// <exception cref="PortalException">No project, no such workspace or PLC, or TIA Portal refused an object.</exception>
        /// <remarks>The project does not change: files are written, and objects become mapped to them.</remarks>
        public VciExportReport ExportToVciWorkspace(string workspaceName, string softwarePath)
        {
            _logger?.LogInformation("Exporting {SoftwarePath} to VCI workspace {Workspace}...", softwarePath, workspaceName);

            try
            {
                return VciProgramExporter.Export(RequireWorkspace(workspaceName), RequireSoftware(softwarePath));
            }
            catch (Exception ex)
            {
                throw DecorateVciFailure(ex, workspaceName, $"Exporting '{softwarePath}' to the VCI workspace");
            }
        }

        /// <summary>Synchronises every changed object of a workspace one way, or none when any would lose a change.</summary>
        /// <param name="workspaceName">The workspace.</param>
        /// <param name="direction">Which side is copied over the other.</param>
        /// <param name="backupDirectory">Where the programs are exported first when the project is overwritten. Required.</param>
        /// <returns>The objects' states afterwards.</returns>
        /// <exception cref="PortalException">
        /// No project or workspace, a conflict or an undecided state (<see cref="VciSyncRule"/>), an object that does not
        /// compile when the project is copied out, or TIA Portal refused.
        /// </exception>
        public IReadOnlyList<VciObjectState> SyncVciWorkspace(string workspaceName, VciDirection direction, string backupDirectory)
        {
            _logger?.LogInformation("Synchronising VCI workspace {Workspace} {Direction}...", workspaceName, direction);

            try
            {
                if (string.IsNullOrWhiteSpace(backupDirectory))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: this can overwrite the project");
                }

                var workspace = RequireWorkspace(workspaceName);
                var mapped = workspace.MappedObjects.ToList();
                var states = mapped.Select(item => DescribeMapped(workspace, item)).ToList();
                var files = VciSyncRule.Plan(states, direction);
                var toSync = mapped.Where((item, index) => files.Contains(states[index].File)).ToList();

                PrepareSync(toSync, direction, backupDirectory);
                toSync.ForEach(item => item.Synchronize(ToOpenness(direction)));

                return workspace.MappedObjects.Select(item => DescribeMapped(workspace, item)).ToList();
            }
            catch (Exception ex)
            {
                throw DecorateVciFailure(ex, workspaceName, $"Synchronising the VCI workspace {direction}");
            }
        }

        /// <remarks>
        /// Out of the project, every block and type must compile, as Openness exports nothing else; into it, the programs
        /// it overwrites are exported first, blocks and types (<see cref="ProgramBackup"/>) and tag tables alike.
        /// </remarks>
        private void PrepareSync(IReadOnlyList<MappedObject> toSync, VciDirection direction, string backupDirectory)
        {
            if (direction == VciDirection.ProjectToWorkspace)
            {
                RequireConsistent(toSync);
                return;
            }

            foreach (var software in toSync.Select(item => OwningSoftware(item.EngineeringObject)).Where(software => software != null).Distinct())
            {
                var directory = Path.Combine(backupDirectory, VciWorkspacePath.SafeName(software!.Name));

                ProgramBackup.Save(software, directory, _logger);
                new TagTableExporter(_logger).ExportAll(software.TagTableGroup, Path.Combine(directory, VciWorkspacePath.Tags));
            }
        }

        private static void RequireConsistent(IReadOnlyList<MappedObject> toSync)
        {
            var inconsistent = toSync.Select(item => item.EngineeringObject)
                .Where(item => (item is PlcBlock block && !block.IsConsistent) || (item is PlcType type && !type.IsConsistent))
                .Select(item => (item as PlcBlock)?.Name ?? (item as PlcType)?.Name)
                .ToList();

            if (inconsistent.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Compile the program first: {string.Join(", ", inconsistent)} do not compile, and Openness exports nothing that does not. Nothing was synchronised.");
            }
        }

        private static PlcSoftware? OwningSoftware(IEngineeringObject? item)
        {
            var current = item;

            while (current != null && current is not PlcSoftware)
            {
                current = current.Parent;
            }

            return current as PlcSoftware;
        }

        private static SynchronizationMode ToOpenness(VciDirection direction)
        {
            return direction switch
            {
                VciDirection.ProjectToWorkspace => SynchronizationMode.ProjectToWorkspace,
                VciDirection.WorkspaceToProject => SynchronizationMode.WorkspaceToProject,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised synchronisation direction: {direction}")
            };
        }

        private static VciObjectState DescribeMapped(Workspace workspace, MappedObject mapped)
        {
            var status = mapped.GetStatus();
            var folder = VciWorkspacePath.RelativeFolder(workspace.RootPath.FullName, mapped.DirectoryPath.FullName);
            // SimaticML is the format this server exports, and an .xml file; another format a person mapped is named by it.
            var extension = mapped.FileFormat == "SimaticML" ? ".xml" : "." + mapped.FileFormat;
            var file = (folder.Length == 0 ? string.Empty : folder + "/") + mapped.FileNameWithoutExtension + extension;

            return new VciObjectState(file, status.CompareState.ToString(), status.IndividualObjectCompareDetails.ToString());
        }

        private VersionControlInterface RequireVci()
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Open a project before working with version control");
            }

            return CurrentProject!.GetService<VersionControlInterface>()
                ?? throw new PortalException(PortalErrorCode.InvalidState, "This project offers no version control interface");
        }

        private Workspace RequireWorkspace(string workspaceName)
        {
            var name = RequireWorkspaceName(workspaceName);
            var workspaces = RequireVci().WorkspaceGroup.Workspaces;

            return workspaces.Find(name)
                ?? throw new PortalException(PortalErrorCode.NotFound, $"No VCI workspace called '{name}'. Workspaces: {string.Join(", ", workspaces.Select(workspace => workspace.Name))}");
        }

        private static string RequireWorkspaceName(string workspaceName)
        {
            var name = (workspaceName ?? string.Empty).Trim();

            return name.Length > 0 ? name : throw new PortalException(PortalErrorCode.InvalidParams, "workspaceName is required");
        }

        private PortalException DecorateVciFailure(Exception ex, string workspaceName, string operation)
        {
            var pex = ex as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"{operation} failed: {ex.Message}", null, ex);

            pex.Data["workspaceName"] = workspaceName;

            _logger?.LogError(pex, "{Operation} failed for {Workspace}", operation, workspaceName);

            return pex;
        }
    }
}
