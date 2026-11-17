using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.CustomWebControls;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// WinCC Unified Custom Web Controls: controls made of a web page, installed in the project and placed on screens like
    /// any screen item. Measured 2026-11-15 on TIA Portal V21 with a pilot light written from scratch: TIA Portal recognised
    /// its package, drew its page in the screen editor, compiled it with no error, and in runtime a click fired its event,
    /// whose script toggled a tag that the control showed again. A package written while the project is open is not seen
    /// until the project is opened again, which is why installing reopens it.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Lists the Custom Web Controls installed in the open project.</summary>
        /// <returns><c>{ folder, controls: [ { name, displayName, file, properties, events, width, height } ], unreadable }</c>.</returns>
        /// <exception cref="PortalException">No local project is open.</exception>
        public JsonObject GetCustomWebControls()
        {
            try
            {
                var folder = CustomWebControlFiles.ProjectFolder(RequireLocalProjectForControls());
                var (read, unreadable) = CustomWebControlFiles.ReadInstalled(folder);

                return new JsonObject
                {
                    ["folder"] = folder,
                    ["controls"] = new JsonArray(read.Select(entry => (JsonNode)Describe(entry.Summary, entry.File)).ToArray()),
                    ["unreadable"] = new JsonArray(unreadable.Select(reason => (JsonNode)reason).ToArray())
                };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "GetCustomWebControls", string.Empty, string.Empty);
            }
        }

        /// <summary>Installs a Custom Web Control in the open project, replacing one of the same name.</summary>
        /// <param name="definition">The checked definition: from the catalogue, or written by the caller.</param>
        /// <param name="backupDirectory">Where a package it replaces is copied first.</param>
        /// <returns><c>{ installed, file, reopened, note }</c>.</returns>
        /// <exception cref="PortalException">No local project is open, it has unsaved changes, WebCC was not found, or the file could not be written.</exception>
        /// <remarks>
        /// In a TIA Portal of the server's own the project is closed and opened again so TIA Portal sees the control, which
        /// needs it saved: closing drops what is not. In a TIA Portal with a window, the person's, the project is not closed
        /// under them; the package is written and the answer says to open the project again.
        /// </remarks>
        public JsonObject InstallCustomWebControl(CustomWebControlDefinition definition, string backupDirectory)
        {
            _logger?.LogInformation("Installing Custom Web Control {Control}...", definition?.Identity.Name);

            try
            {
                var control = definition ?? throw new PortalException(PortalErrorCode.InvalidParams, "A control definition is required");
                var project = RequireLocalProjectForControls();
                var reopen = AttachedProcess?.HasUserInterface != true;

                if (reopen && project.IsModified)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "The project has unsaved changes, and installing a control closes and opens it again so TIA Portal sees it: save it first (SaveProject). Nothing was changed.");
                }

                RequireNameFree(project, control.Identity);

                var file = WritePackage(project, control, backupDirectory);

                return new JsonObject
                {
                    ["installed"] = control.Identity.Name,
                    ["file"] = file,
                    ["reopened"] = reopen && Reopen(project.Path.FullName),
                    ["note"] = reopen ? "TIA Portal has the control now." : "The project is open in TIA Portal's window: close it and open it again there to see the control."
                };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "InstallCustomWebControl"), "InstallCustomWebControl", string.Empty, definition?.Identity.Name ?? string.Empty);
            }
        }

        /// <summary>Places Custom Web Controls on a WinCC Unified screen, all of them or none.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <param name="list">The screen and the controls.</param>
        /// <param name="backupDirectory">Where the screen's items before the change are listed.</param>
        /// <returns><c>{ placed: [ names ] }</c>.</returns>
        /// <exception cref="PortalException">The screen or a control is missing, a name is taken, a property or event does not fit, or TIA Portal refused one; nothing is left placed.</exception>
        public JsonObject PlaceCustomWebControls(string hmiPath, CustomWebControlPlacementList list, string backupDirectory)
        {
            _logger?.LogInformation("Placing Custom Web Controls on {HmiPath}...", hmiPath);

            try
            {
                var requested = list ?? throw new PortalException(PortalErrorCode.InvalidParams, "A control list is required");
                var hmi = RequireUnifiedSoftware(hmiPath);
                var screen = FindUnifiedScreen(hmi, hmiPath, requested.Screen);
                var installed = InstalledByName(requested);
                var writer = new CustomWebControlWriter(screen, requested, installed, root => hmi.Tags.Find(root) != null);

                writer.Check();
                writer.Place(backupDirectory);

                return new JsonObject { ["screen"] = screen.Name, ["placed"] = new JsonArray(requested.Placements.Select(placement => (JsonNode)placement.Name).ToArray()) };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "PlaceCustomWebControls"), "PlaceCustomWebControls", hmiPath, string.Empty);
            }
        }

        /// <remarks>A name two packages declare, case aside, is refused when placed: which one TIA Portal would take is not known.</remarks>
        private Dictionary<string, CustomWebControlSummary> InstalledByName(CustomWebControlPlacementList requested)
        {
            var groups = CustomWebControlFiles.ReadInstalled(CustomWebControlFiles.ProjectFolder(RequireLocalProjectForControls())).Read
                .GroupBy(entry => entry.Summary.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var ambiguous = groups.FirstOrDefault(group => group.Count() > 1 && requested.Placements.Any(placement => string.Equals(placement.Control, group.Key, StringComparison.OrdinalIgnoreCase)));

            if (ambiguous != null)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{ambiguous.Key}' is declared by {ambiguous.Count()} packages ({string.Join(", ", ambiguous.Select(entry => Path.GetFileName(entry.File)))}); remove one before placing it");
            }

            return groups.ToDictionary(group => group.First().Summary.Name, group => group.First().Summary, StringComparer.OrdinalIgnoreCase);
        }

        private Project RequireLocalProjectForControls()
        {
            return RequireLocalProject("Custom Web Controls are files in the project's folder, and a multiuser session has none here");
        }

        private static string WritePackage(Project project, CustomWebControlDefinition definition, string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: a package replaced is copied into it first");
            }

            var package = CustomWebControlPackage.Build(definition, CustomWebControlFiles.FindWebCc());
            var folder = CustomWebControlFiles.ProjectFolder(project);
            var file = Path.Combine(folder, CustomWebControlPackage.FileName(definition.Identity));

            Directory.CreateDirectory(backupDirectory);
            if (File.Exists(file))
            {
                File.Copy(file, Path.Combine(backupDirectory, Path.GetFileName(file)), overwrite: true);
            }

            CustomWebControlFiles.WritePackage(folder, file, package);

            return file;
        }

        /// <remarks>
        /// A package of another GUID declaring the same name, case aside, would leave two controls TIA Portal places by one
        /// name: the placement would be checked against one and run the other (security review of 2026-11-15).
        /// </remarks>
        private static void RequireNameFree(Project project, CustomWebControlIdentity identity)
        {
            var own = CustomWebControlPackage.FileName(identity);
            var other = CustomWebControlFiles.ReadInstalled(CustomWebControlFiles.ProjectFolder(project)).Read
                .FirstOrDefault(entry => string.Equals(entry.Summary.Name, identity.Name, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFileName(entry.File), own, StringComparison.OrdinalIgnoreCase));

            if (other.File != null)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"'{Path.GetFileName(other.File)}' already declares a control named '{other.Summary.Name}'; choose another name. Nothing was written.");
            }
        }

        /// <remarks>
        /// Through the server's own close and open, so what it remembers of the open project stays right. The package is
        /// written by then, so a failed open says so and how to go on, rather than reading as an install that did not happen.
        /// </remarks>
        private bool Reopen(string projectPath)
        {
            CloseProject();

            bool opened;
            try
            {
                opened = OpenProject(projectPath);
            }
            catch (Exception failure) when (failure is PortalException || failure is EngineeringException)
            {
                throw ReopenFailed(projectPath, failure.Message.Trim(), failure);
            }

            // Review of 2026-11-16: a false open left the project closed under an answer saying the control was there.
            return opened ? true : throw ReopenFailed(projectPath, "TIA Portal is not connected", null);
        }

        private static PortalException ReopenFailed(string projectPath, string reason, Exception? failure)
        {
            return new PortalException(
                PortalErrorCode.WriteFailed,
                $"The control's package is installed, but opening the project again failed and it is closed now: open it with OpenProject('{projectPath}'). ({reason})",
                null,
                failure);
        }

        private static JsonObject Describe(CustomWebControlSummary summary, string file)
        {
            return new JsonObject
            {
                ["name"] = summary.Name,
                ["displayName"] = summary.DisplayName,
                ["file"] = Path.GetFileName(file),
                ["properties"] = new JsonArray(summary.Properties.Select(property => (JsonNode)$"{property.Name}: {(property.Kind?.ToString() ?? "not set by this server")}").ToArray()),
                ["events"] = new JsonArray(summary.Events.Select(name => (JsonNode)name).ToArray()),
                ["width"] = summary.DefaultWidth,
                ["height"] = summary.DefaultHeight
            };
        }
    }
}
