namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One module of a station: where it sits, what it is called and what it is.
    /// </summary>
    /// <remarks>
    /// What a rack is made of, in the form that survives the project being closed. It is the record
    /// a hardware write is measured against: a module plugged into slot 3 changes what the station
    /// is, and neither the program export nor the network table would show it.
    /// </remarks>
    public sealed class ModuleInfo
    {
        /// <summary>Creates a module description.</summary>
        /// <param name="devicePath">Full path of the module in the project.</param>
        /// <param name="positionNumber">The slot it sits in.</param>
        /// <param name="typeIdentifier">What it is, as Openness names it — usually an order number.</param>
        /// <param name="isBuiltIn">
        /// True when it is part of the device rather than plugged into it, which is why it cannot
        /// be unplugged.
        /// </param>
        public ModuleInfo(string devicePath, int positionNumber, string typeIdentifier, bool isBuiltIn)
        {
            DevicePath = devicePath;
            PositionNumber = positionNumber;
            TypeIdentifier = typeIdentifier;
            IsBuiltIn = isBuiltIn;
        }

        /// <summary>Full path of the module in the project.</summary>
        public string DevicePath { get; }

        /// <summary>The slot it sits in.</summary>
        public int PositionNumber { get; }

        /// <summary>What it is, as Openness names it.</summary>
        public string TypeIdentifier { get; }

        /// <summary>True when it is part of the device rather than plugged into it.</summary>
        public bool IsBuiltIn { get; }
    }
}
