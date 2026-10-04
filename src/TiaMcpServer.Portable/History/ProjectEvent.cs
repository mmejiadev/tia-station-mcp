namespace TiaMcpServer.History
{
    /// <summary>How a project came to be the one the server works on.</summary>
    public enum ProjectEvent
    {
        /// <summary>An existing project file was opened.</summary>
        Opened,

        /// <summary>A project was restored from an archive.</summary>
        Retrieved,

        /// <summary>A new, empty project was created.</summary>
        Created
    }
}
