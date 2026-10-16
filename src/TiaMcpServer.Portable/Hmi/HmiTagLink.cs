namespace TiaMcpServer.Hmi
{
    /// <summary>One HMI tag to generate, and the PLC tag it reads and writes.</summary>
    /// <remarks>
    /// A classic WinCC tag points at its PLC tag by name, not by address: the link is resolved by
    /// TIA Portal when the HMI is compiled, and the data type comes from the PLC tag (measured
    /// 2026-10-13). So a name is all a generated tag needs.
    /// </remarks>
    public sealed class HmiTagLink
    {
        /// <summary>Creates a link.</summary>
        /// <param name="hmiTagName">The HMI tag's name.</param>
        /// <param name="controllerTagName">The PLC tag's name, as its tag table spells it.</param>
        public HmiTagLink(string hmiTagName, string controllerTagName)
        {
            HmiTagName = hmiTagName;
            ControllerTagName = controllerTagName;
        }

        /// <summary>The HMI tag's name.</summary>
        public string HmiTagName { get; }

        /// <summary>The PLC tag's name, as its tag table spells it.</summary>
        public string ControllerTagName { get; }
    }
}
