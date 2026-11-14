namespace TiaMcpServer.Vci
{
    /// <summary>Which way a synchronisation copies: what is overwritten is the other side.</summary>
    public enum VciDirection
    {
        /// <summary>The project's objects are written to the workspace's files.</summary>
        ProjectToWorkspace,

        /// <summary>The workspace's files are imported into the project.</summary>
        WorkspaceToProject
    }
}
