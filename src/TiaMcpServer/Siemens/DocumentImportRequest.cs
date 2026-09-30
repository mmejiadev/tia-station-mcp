namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Where SIMATIC SD documents are imported from and to, and where the previous state is saved
    /// first. Shared by the single and the bulk import, which differ only in what they select.
    /// </summary>
    public sealed class DocumentImportRequest
    {
        /// <summary>Creates the request.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="groupPath">Block group the blocks are placed in; empty for the root.</param>
        /// <param name="importPath">Directory holding the documents.</param>
        /// <param name="backupDirectory">
        /// Where the program's blocks and types are exported before anything is written. Required:
        /// an import replaces blocks of the same name.
        /// </param>
        public DocumentImportRequest(string softwarePath, string groupPath, string importPath, string backupDirectory)
        {
            SoftwarePath = softwarePath;
            GroupPath = groupPath;
            ImportPath = importPath;
            BackupDirectory = backupDirectory;
        }

        /// <summary>Full path to the PLC software.</summary>
        public string SoftwarePath { get; }

        /// <summary>Block group the blocks are placed in; empty for the root.</summary>
        public string GroupPath { get; }

        /// <summary>Directory holding the documents.</summary>
        public string ImportPath { get; }

        /// <summary>Where the previous state is exported before anything is written.</summary>
        public string BackupDirectory { get; }
    }
}
