namespace TiaMcpServer.UnifiedTags
{
    /// <summary>How a linked WinCC Unified tag is read from the PLC.</summary>
    /// <remarks>
    /// Openness V20 has a third mode, cyclic continuous; it compiled as "The specified value is invalid:
    /// 'Cyclic continuous'" on a tag of an integrated connection to an S7-1200 (measured 2026-10-25), so
    /// it is not offered.
    /// </remarks>
    public enum UnifiedAcquisitionMode
    {
        /// <summary>Read in its cycle while something on a screen shows it; TIA Portal's default.</summary>
        CyclicOnUse,

        /// <summary>Read only when a script asks for it; the tag has no cycle.</summary>
        OnDemand
    }
}
