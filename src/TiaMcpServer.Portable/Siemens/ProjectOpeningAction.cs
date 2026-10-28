namespace TiaMcpServer.Siemens
{
    /// <summary>What opening a project comes down to, given what is already open where.</summary>
    public enum ProjectOpeningAction
    {
        /// <summary>The attached TIA Portal already holds the project: work on it as it is.</summary>
        UseOpenProject,

        /// <summary>Another TIA Portal holds it, and has it locked: attach to that one instead.</summary>
        AttachToHolder,

        /// <summary>The attached TIA Portal holds no project: open it there.</summary>
        OpenHere,

        /// <summary>The server's own TIA Portal holds another project: close that and open this.</summary>
        ReplaceOwnProject,

        /// <summary>
        /// Somebody else's TIA Portal holds another project. It is theirs, so it is not closed, and
        /// the opening is refused with the reason.
        /// </summary>
        Refuse
    }
}
