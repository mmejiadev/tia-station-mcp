namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One use: which way the relation runs, how the object is touched, and where.
    /// </summary>
    /// <remarks>
    /// <see cref="ReferenceType"/> is the field that matters and the reason this is not a bare
    /// string. "UsedBy" means the other object calls this one and would break if it changed;
    /// "Uses" means the opposite. Openness has thirteen relations and those are two of them.
    ///
    /// <see cref="Location"/> comes from Openness' <c>Location.ReferenceLocation</c> rather than
    /// its <c>Location.Name</c>. The documentation calls one "the reference location of a
    /// referenced object" and the other "the name of a referenced location object", which is a
    /// distinction only a measurement settles; the one that says *location* was taken, and the
    /// test that reads a real call asserts it is not empty, so the wrong choice fails loudly
    /// instead of printing a blank column.
    /// </remarks>
    public sealed class CrossReferenceUsage
    {
        /// <summary>Creates a use.</summary>
        /// <param name="referenceType">The direction: <c>UsedBy</c>, <c>Uses</c>, and Openness' other relations.</param>
        /// <param name="access">How the object is touched: <c>Call</c>, <c>Read</c>, <c>Write</c>, <c>UC</c>, <c>CC</c>.</param>
        /// <param name="location">Where the two objects meet, for example the network of a call.</param>
        /// <param name="address">The address within that location, or empty when there is none.</param>
        public CrossReferenceUsage(string referenceType, string access, string location, string address)
        {
            ReferenceType = referenceType;
            Access = access;
            Location = location;
            Address = address;
        }

        /// <summary>The direction of the relation, as Openness names it.</summary>
        public string ReferenceType { get; }

        /// <summary>How the object is touched at this location.</summary>
        public string Access { get; }

        /// <summary>Where the two objects meet.</summary>
        public string Location { get; }

        /// <summary>The address within that location.</summary>
        public string Address { get; }
    }
}
