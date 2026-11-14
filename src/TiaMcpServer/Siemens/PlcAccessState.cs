namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// How a PLC is protected: its confidential configuration data and the access level it grants without a login.
    /// </summary>
    /// <remarks>Read as Openness names the values, so a record of them reads the same as TIA Portal's own words.</remarks>
    public sealed class PlcAccessState
    {
        /// <summary>Creates a description of a PLC's protection.</summary>
        /// <param name="confidentialData">How its confidential configuration data is protected, e.g. <c>WithoutPassword</c>.</param>
        /// <param name="accessLevel">The access level granted without a password, e.g. <c>NoAccess</c>.</param>
        public PlcAccessState(string confidentialData, string accessLevel)
        {
            ConfidentialData = confidentialData;
            AccessLevel = accessLevel;
        }

        /// <summary>How its confidential configuration data is protected.</summary>
        public string ConfidentialData { get; }

        /// <summary>The access level granted without a password.</summary>
        public string AccessLevel { get; }
    }
}
