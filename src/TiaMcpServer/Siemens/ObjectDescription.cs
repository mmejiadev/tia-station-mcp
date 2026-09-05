using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// A device, a device item, a PLC software or an open project, read once and detached.
    /// </summary>
    /// <remarks>
    /// One type for four things, because what the portal reads about all four is the same three
    /// values: a name, how Openness renders it, and its attribute bag. Four classes with identical
    /// members would be ceremony — they would not tell a reader anything the name of the method
    /// that returned one does not already say.
    ///
    /// That is a statement about today, not a principle. The moment one of them carries something
    /// the others cannot — a device's order number, a software's CPU family — it earns its own
    /// description rather than a nullable property here that is meaningless for the other three.
    ///
    /// What is not negotiable is that this crosses into the MCP layer and <c>Device</c>,
    /// <c>DeviceItem</c>, <c>PlcSoftware</c> and <c>ProjectBase</c> do not: reading any property of
    /// those is a call into TIA Portal, and they stop answering when the project closes.
    /// </remarks>
    public sealed class ObjectDescription
    {
        /// <summary>The object's name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>How Openness renders it, kept because it is useful in diagnostics.</summary>
        public string? Description { get; init; }

        /// <summary>Everything Openness exposes about it.</summary>
        public IReadOnlyList<ObjectAttribute> Attributes { get; init; } = new List<ObjectAttribute>();
    }
}
