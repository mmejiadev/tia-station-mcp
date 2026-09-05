namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One attribute read from a TIA Portal object.
    /// </summary>
    /// <remarks>
    /// Openness exposes most of what it knows about an object as a bag of named attributes rather
    /// than as typed properties, and callers do want to see them. What they must not see is the
    /// engineering object itself: reading an attribute goes through TIA Portal, so an object handed
    /// upwards is a live remote reference that stops working the moment the project closes. This is
    /// the value that was read, and nothing more.
    /// </remarks>
    public sealed class ObjectAttribute
    {
        /// <summary>Creates an attribute description.</summary>
        /// <param name="name">The attribute name as Openness spells it.</param>
        /// <param name="value">The value that was read.</param>
        /// <param name="accessMode">Whether it is readable, writable, or both.</param>
        public ObjectAttribute(string name, object? value, string? accessMode)
        {
            Name = name;
            Value = value;
            AccessMode = accessMode;
        }

        /// <summary>The attribute name as Openness spells it.</summary>
        public string Name { get; }

        /// <summary>The value that was read.</summary>
        public object? Value { get; }

        /// <summary>Whether it is readable, writable, or both.</summary>
        public string? AccessMode { get; }
    }
}
