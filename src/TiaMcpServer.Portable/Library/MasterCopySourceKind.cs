namespace TiaMcpServer.Library
{
    /// <summary>What a master copy can be made from, and created as again, through these tools.</summary>
    /// <remarks>
    /// Openness V20 takes more as a master copy source (measured 2026-11-06 by reflection on
    /// <c>Siemens.Engineering.dll</c>: watch tables, alarm text lists, classic HMI screens and tags, hardware),
    /// and refuses a WinCC Unified screen. These three are the ones a PLC program is built from.
    /// </remarks>
    public enum MasterCopySourceKind
    {
        /// <summary>A program block: OB, FB, FC or data block.</summary>
        Block,

        /// <summary>A PLC tag table.</summary>
        TagTable,

        /// <summary>A PLC data type (UDT).</summary>
        PlcType
    }
}
