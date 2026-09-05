using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// What the portal layer knows about one user-defined type, read once and detached.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="BlockDescription"/>, and it exists for the same reason: every
    /// property of a <c>PlcType</c> is a call into TIA Portal, so a type handed upwards reads fine
    /// while the project is open and throws afterwards, in code that has no idea it is talking to
    /// an engineering tool.
    ///
    /// It is a narrower thing than a block. A UDT has no programming language and no memory layout,
    /// so those fields are absent rather than left empty — a description should not carry a hole
    /// shaped like a property that could never be filled.
    /// </remarks>
    public sealed class TypeDescription
    {
        /// <summary>The type name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Its full path in the project, <c>Group/Subgroup/Name</c>.</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>The Openness type name, which for a user-defined type is <c>PlcStruct</c>.</summary>
        public string TypeName { get; init; } = string.Empty;

        /// <summary>The namespace, empty when it belongs to none.</summary>
        public string? Namespace { get; init; }

        /// <summary>
        /// Whether it compiles as it stands. An inconsistent type cannot be exported, and the
        /// native error does not say so.
        /// </summary>
        public bool IsConsistent { get; init; }

        /// <summary>When the type last changed.</summary>
        public DateTime ModifiedDate { get; init; }

        /// <summary>Whether it is know-how protected, and so unreadable.</summary>
        public bool IsKnowHowProtected { get; init; }

        /// <summary>How Openness renders the type, kept because it is useful in diagnostics.</summary>
        public string? Description { get; init; }

        /// <summary>Everything else Openness exposes about the type.</summary>
        public IReadOnlyList<ObjectAttribute> Attributes { get; init; } = new List<ObjectAttribute>();
    }
}
