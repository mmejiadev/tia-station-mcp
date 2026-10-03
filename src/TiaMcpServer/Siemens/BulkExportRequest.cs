namespace TiaMcpServer.Siemens
{
    /// <summary>What a bulk export reads and where it writes. Shared by the three bulk exports.</summary>
    public sealed class BulkExportRequest
    {
        /// <summary>Creates the request.</summary>
        /// <param name="softwarePath">Full path to the PLC software.</param>
        /// <param name="exportPath">Directory the files are written to.</param>
        /// <param name="regexName">Name or regular expression selecting the items; empty for all.</param>
        /// <param name="preservePath">Mirror the group structure below the export directory.</param>
        public BulkExportRequest(string softwarePath, string exportPath, string regexName, bool preservePath)
        {
            SoftwarePath = softwarePath;
            ExportPath = exportPath;
            RegexName = regexName;
            PreservePath = preservePath;
        }

        /// <summary>Full path to the PLC software.</summary>
        public string SoftwarePath { get; }

        /// <summary>Directory the files are written to.</summary>
        public string ExportPath { get; }

        /// <summary>Name or regular expression selecting the items; empty for all.</summary>
        public string RegexName { get; }

        /// <summary>Mirror the group structure below the export directory.</summary>
        public bool PreservePath { get; }
    }
}
