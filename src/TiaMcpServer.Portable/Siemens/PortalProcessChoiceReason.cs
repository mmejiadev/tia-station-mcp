namespace TiaMcpServer.Siemens
{
    /// <summary>Why <see cref="PortalProcessSelector"/> chose the process it chose.</summary>
    public enum PortalProcessChoiceReason
    {
        /// <summary>No TIA Portal is running, so the server starts its own.</summary>
        NoneRunning,

        /// <summary>The process holds the project the caller asked for.</summary>
        HoldsRequestedProject,

        /// <summary>The process has a window, so it is the one a person is working in.</summary>
        HasUserInterface,

        /// <summary>Every running process is headless; the one with a project open was preferred.</summary>
        WithoutUserInterface,

        /// <summary>
        /// Connect was called again while the server held a TIA Portal it started, and kept it:
        /// letting it go would close its project without saving.
        /// </summary>
        KeptOwnPortal
    }
}
