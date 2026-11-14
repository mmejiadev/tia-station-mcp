namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// A PC network interface TIA Portal offers for going online with a CPU.
    /// </summary>
    public sealed class PcInterfaceInfo
    {
        /// <summary>Creates the description of an interface.</summary>
        /// <param name="mode">The connection mode it belongs to, e.g. <c>PN/IE</c>.</param>
        /// <param name="name">The interface, as TIA Portal names it.</param>
        /// <param name="number">Its number, which tells apart two interfaces of one name.</param>
        public PcInterfaceInfo(string mode, string name, int number)
        {
            Mode = mode;
            Name = name;
            Number = number;
        }

        /// <summary>The connection mode it belongs to.</summary>
        public string Mode { get; }

        /// <summary>The interface, as TIA Portal names it.</summary>
        public string Name { get; }

        /// <summary>Its number.</summary>
        public int Number { get; }

        /// <summary>Whether it leads to PLCSIM rather than to a real network.</summary>
        public bool IsSimulation => PcInterfaceRule.IsSimulation(Name);
    }
}
