namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Result of retrieving a project archive.</summary>
    public sealed class ResponseRetrieveProject : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="projectPath">Full path of the retrieved project file.</param>
        public ResponseRetrieveProject(string projectPath)
        {
            ProjectPath = projectPath;
        }

        /// <summary>Full path of the retrieved project file, now open in TIA Portal.</summary>
        public string ProjectPath { get; }
    }
}
