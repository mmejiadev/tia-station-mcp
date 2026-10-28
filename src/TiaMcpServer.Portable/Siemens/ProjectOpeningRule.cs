using System;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>Decides how to open a project without closing anybody else's.</summary>
    /// <remarks>
    /// Before this rule, opening a project began by closing whatever the attached TIA Portal held,
    /// and the server attaches to the user's own window when she has one open. So asking for a
    /// second project closed hers — unsaved work included — without a word.
    ///
    /// **A project open in a TIA Portal the server did not start is never closed by opening
    /// another.** The server's own TIA Portal is its to manage; anyone else's is not, and the
    /// absence of ownership is a refusal, never a permission. A project already open somewhere is
    /// used where it is: TIA Portal locks it, so opening it a second time could only fail.
    /// </remarks>
    public static class ProjectOpeningRule
    {
        /// <summary>Decides how to open the requested project.</summary>
        /// <param name="request">The project wanted and what is open where.</param>
        /// <returns>The action to take.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
        public static ProjectOpeningDecision Decide(ProjectOpeningRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (ProjectFilePath.AreSame(request.OpenProjectPath, request.RequestedPath))
            {
                return new ProjectOpeningDecision(ProjectOpeningAction.UseOpenProject);
            }

            var holder = request.OtherProcesses.FirstOrDefault(
                process => ProjectFilePath.AreSame(process.ProjectPath, request.RequestedPath));

            if (holder != null)
            {
                return new ProjectOpeningDecision(ProjectOpeningAction.AttachToHolder, holder);
            }

            if (request.OpenProjectPath.Length == 0)
            {
                return new ProjectOpeningDecision(ProjectOpeningAction.OpenHere);
            }

            return request.OwnsPortal
                ? new ProjectOpeningDecision(ProjectOpeningAction.ReplaceOwnProject)
                : new ProjectOpeningDecision(ProjectOpeningAction.Refuse);
        }
    }
}
