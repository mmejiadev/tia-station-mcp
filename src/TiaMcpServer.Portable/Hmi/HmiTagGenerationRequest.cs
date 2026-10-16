namespace TiaMcpServer.Hmi
{
    /// <summary>Which PLC program gets HMI tags, on which panel, in which table and through which connection.</summary>
    public sealed class HmiTagGenerationRequest
    {
        /// <summary>Creates a request.</summary>
        /// <param name="plcSoftwarePath">Full path to the PLC software whose memory tags are read.</param>
        /// <param name="hmiPath">Full path to the classic WinCC HMI target.</param>
        /// <param name="tableName">The HMI tag table the tags go into; created or regenerated.</param>
        /// <param name="connectionName">The HMI connection to the PLC, for example <c>HMI_Connection_1</c>.</param>
        public HmiTagGenerationRequest(string plcSoftwarePath, string hmiPath, string tableName, string connectionName)
        {
            PlcSoftwarePath = plcSoftwarePath;
            HmiPath = hmiPath;
            TableName = tableName;
            ConnectionName = connectionName;
        }

        /// <summary>Full path to the PLC software whose memory tags are read.</summary>
        public string PlcSoftwarePath { get; }

        /// <summary>Full path to the classic WinCC HMI target.</summary>
        public string HmiPath { get; }

        /// <summary>The HMI tag table the tags go into; created or regenerated.</summary>
        public string TableName { get; }

        /// <summary>The HMI connection to the PLC.</summary>
        public string ConnectionName { get; }
    }
}
