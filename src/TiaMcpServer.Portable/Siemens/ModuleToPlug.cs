namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// What to plug into a rack: an order number, a name for it, and the slot it goes in.
    /// </summary>
    /// <remarks>
    /// A parameter object rather than three more arguments beside the path, and it is validated
    /// here so that a malformed request never reaches TIA Portal. The slot is the part worth
    /// checking early: Openness reports a negative position as the same generic failure it reports
    /// for a rack that is full, and the two have nothing in common.
    /// </remarks>
    public sealed class ModuleToPlug
    {
        /// <summary>Reads a request to plug a module, checking it names all three things.</summary>
        /// <param name="typeIdentifier">
        /// What to plug, as Openness names it, for example
        /// <c>OrderNumber:6ES7 521-1BL00-0AB0/V2.1</c>. Copy one from GetPlugLocations, which
        /// prints the identifier of every module already in the rack.
        /// </param>
        /// <param name="name">Name for the module, for example <c>DI 32x24VDC</c>.</param>
        /// <param name="positionNumber">The slot, as GetPlugLocations numbers them.</param>
        /// <exception cref="PortalException">Something is missing, or the slot is negative.</exception>
        public ModuleToPlug(string typeIdentifier, string name, int positionNumber)
        {
            if (string.IsNullOrWhiteSpace(typeIdentifier))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    "typeIdentifier is required, for example 'OrderNumber:6ES7 521-1BL00-0AB0/V2.1'");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "name is required: the module is named in the project");
            }

            if (positionNumber < 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"Slot {positionNumber} does not exist. Call GetPlugLocations to see the slots this rack has.");
            }

            TypeIdentifier = typeIdentifier.Trim();
            Name = name.Trim();
            PositionNumber = positionNumber;
        }

        /// <summary>What to plug, as Openness names it.</summary>
        public string TypeIdentifier { get; }

        /// <summary>The name the module carries in the project.</summary>
        public string Name { get; }

        /// <summary>The slot it goes in.</summary>
        public int PositionNumber { get; }
    }
}
