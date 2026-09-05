using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// What the portal layer knows about one program block, read once and detached.
    /// </summary>
    /// <remarks>
    /// This is the type that crosses into the MCP layer; <c>PlcBlock</c> is not. The dependency rule
    /// in CLAUDE.md is the reason, and the reason behind the rule is that every property of a
    /// <c>PlcBlock</c> is a call into TIA Portal: a block handed upwards reads fine while the
    /// project is open and throws afterwards, at a point in the code that has no idea it is talking
    /// to an engineering tool.
    ///
    /// The properties are <c>init</c>-only rather than arguments of a twelve-parameter constructor:
    /// a description is written with an object initialiser and is immutable the moment it exists.
    /// The repository's four-argument limit is about methods that do something; wrapping a dozen
    /// read values in nested parameter objects would only add layers to read a name through.
    /// </remarks>
    public sealed class BlockDescription
    {
        /// <summary>The block name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Its full path in the project, <c>Group/Subgroup/Name</c>.</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>The Openness type: <c>FC</c>, <c>FB</c>, <c>GlobalDB</c>, and so on.</summary>
        public string TypeName { get; init; } = string.Empty;

        /// <summary>The block namespace, empty when it belongs to none.</summary>
        public string? Namespace { get; init; }

        /// <summary>The programming language, as its Openness enum name.</summary>
        public string? ProgrammingLanguage { get; init; }

        /// <summary>The memory layout, as its Openness enum name.</summary>
        public string? MemoryLayout { get; init; }

        /// <summary>
        /// Whether the block compiles as it stands. An inconsistent block cannot be exported, and
        /// the native error does not say so, which is why this travels with every description.
        /// </summary>
        public bool IsConsistent { get; init; }

        /// <summary>The title shown in the block header.</summary>
        public string? HeaderName { get; init; }

        /// <summary>When the block last changed.</summary>
        public DateTime ModifiedDate { get; init; }

        /// <summary>Whether the block is know-how protected, and so unreadable.</summary>
        public bool IsKnowHowProtected { get; init; }

        /// <summary>How Openness renders the block, kept because it is useful in diagnostics.</summary>
        public string? Description { get; init; }

        /// <summary>Everything else Openness exposes about the block.</summary>
        public IReadOnlyList<ObjectAttribute> Attributes { get; init; } = new List<ObjectAttribute>();
    }
}
