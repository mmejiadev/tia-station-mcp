namespace TiaMcpServer.Siemens
{
    /// <summary>How a project is to be opened, and the process to attach to when that is the way.</summary>
    public sealed class ProjectOpeningDecision
    {
        /// <summary>Records a decision.</summary>
        /// <param name="action">What to do.</param>
        /// <param name="holder">The process holding the project, for <see cref="ProjectOpeningAction.AttachToHolder"/>.</param>
        public ProjectOpeningDecision(ProjectOpeningAction action, PortalProcessInfo? holder = null)
        {
            Action = action;
            Holder = holder;
        }

        /// <summary>What to do.</summary>
        public ProjectOpeningAction Action { get; }

        /// <summary>The process holding the project, or null when the action needs none.</summary>
        public PortalProcessInfo? Holder { get; }
    }
}
