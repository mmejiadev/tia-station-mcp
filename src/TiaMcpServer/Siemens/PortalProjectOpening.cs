using Siemens.Engineering;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// Carrying out what <see cref="ProjectOpeningRule"/> decides: using a project where it is open,
    /// moving to the TIA Portal that holds it, opening it, or refusing. Beside PortalInstances, which
    /// chooses the TIA Portal; the rule itself is tested without one.
    /// </remarks>
    public partial class Portal
    {
        private ProjectOpeningDecision DecideOpening(string requestedProjectPath, string openProjectPath)
        {
            // Nothing elsewhere matters when the project is open here, and reading every process
            // asks each TIA Portal for its project.
            var others = ProjectFilePath.AreSame(openProjectPath, requestedProjectPath)
                ? new List<PortalProcessInfo>()
                : ReadOtherProcesses(_attachedProcess?.Id ?? 0);

            return ProjectOpeningRule.Decide(new ProjectOpeningRequest(requestedProjectPath, openProjectPath, OwnsPortalProcess, others));
        }

        private bool CarryOut(ProjectOpeningDecision decision, string requestedProjectPath, string openProjectPath)
        {
            return decision.Action switch
            {
                ProjectOpeningAction.UseOpenProject => UseOpenProject(requestedProjectPath),
                ProjectOpeningAction.AttachToHolder => AttachToHolder(decision.Holder!, requestedProjectPath),
                ProjectOpeningAction.OpenHere => OpenInAttachedPortal(requestedProjectPath),
                ProjectOpeningAction.ReplaceOwnProject => ReplaceOwnProject(requestedProjectPath),
                ProjectOpeningAction.Refuse => throw RefuseToCloseUsersProject(requestedProjectPath, openProjectPath),
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised way of opening a project: {decision.Action}")
            };
        }

        /// <summary>Closes the project open in the attached portal before a new one is created or retrieved there.</summary>
        /// <param name="newProjectLocation">Where the new project goes, for the refusal message.</param>
        /// <exception cref="PortalException">The open project is in a TIA Portal the server did not start.</exception>
        /// <remarks>The same rule as opening: anyone else's project is left open, and the call refused.</remarks>
        private void MakeRoomForNewProject(string newProjectLocation)
        {
            var openPath = ProjectPathInAttachedPortal(string.Empty);
            var decision = ProjectOpeningRule.Decide(new ProjectOpeningRequest(newProjectLocation, openPath, OwnsPortalProcess, new List<PortalProcessInfo>()));

            switch (decision.Action)
            {
                case ProjectOpeningAction.OpenHere:
                    return;
                case ProjectOpeningAction.ReplaceOwnProject:
                    CloseEveryProjectHere();
                    return;
                case ProjectOpeningAction.Refuse:
                    throw RefuseToCloseUsersProject(newProjectLocation, openPath);
                default:
                    throw new PortalException(PortalErrorCode.InvalidState, $"Unexpected way of making room for a new project: {decision.Action}");
            }
        }

        /// <summary>Leaves the attached portal for the process that holds the requested project.</summary>
        /// <remarks>
        /// The holder is found and attached to before anything is let go, so a holder that closed or
        /// refused in between leaves the server where it was, its own project untouched. Only then
        /// is the previous portal released: closed if the server started it, so nothing is left
        /// running, merely detached from otherwise.
        /// </remarks>
        private bool AttachToHolder(PortalProcessInfo holder, string requestedProjectPath)
        {
            var process = TiaPortal.GetProcesses().FirstOrDefault(candidate => candidate.Id == holder.Id);

            if (process == null)
            {
                throw new PortalException(PortalErrorCode.ConnectFailed, $"{holder} held the project and is no longer running");
            }

            var attached = process.Attach();

            ReleasePortal();

            _portal = attached;
            _attachedProcess = holder;
            AttachedProcessReason = PortalProcessChoiceReason.HoldsRequestedProject;
            AttachToOpenProject(requestedProjectPath);

            return CurrentProject != null;
        }

        private bool UseOpenProject(string requestedProjectPath)
        {
            AttachToOpenProject(requestedProjectPath);

            return CurrentProject != null;
        }

        private bool OpenInAttachedPortal(string requestedProjectPath)
        {
            // see [5.3.1 Projekt öffnen, S.113]
            _session = null;
            CurrentProject = _portal?.Projects.OpenWithUpgrade(new FileInfo(requestedProjectPath));

            return CurrentProject != null;
        }

        private bool ReplaceOwnProject(string requestedProjectPath)
        {
            CloseEveryProjectHere();

            return OpenInAttachedPortal(requestedProjectPath);
        }

        private void CloseEveryProjectHere()
        {
            ReleaseProjectIfOwned();

            foreach (var project in _portal?.Projects.ToList() ?? new List<Project>())
            {
                project.Close();
            }
        }

        /// <remarks>
        /// Said of the process, not of the project: a project the server itself opened in the user's
        /// window is not closed either, since she may have been working in it since.
        /// </remarks>
        private PortalException RefuseToCloseUsersProject(string requestedProjectPath, string openProjectPath)
        {
            return new PortalException(
                PortalErrorCode.InvalidState,
                $"'{requestedProjectPath}' was not opened: TIA Portal process {_attachedProcess?.Id} holds " +
                $"'{openProjectPath}', and the server did not start that TIA Portal, so it closes nothing " +
                "there that someone may be working in. Save and close it in TIA Portal, or open the requested " +
                "project there, and ask again.");
        }
    }
}
