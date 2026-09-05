using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// One block group and everything under it, read once and detached.
    /// </summary>
    /// <remarks>
    /// The group tree is how a program is actually organised, and a flat list of blocks loses it.
    /// Reading the whole tree in one pass is also the cheaper shape: walking it lazily from the MCP
    /// layer would mean a call into TIA Portal for every group the caller happened to expand.
    /// </remarks>
    public sealed class BlockGroupDescription
    {
        /// <summary>The group name. The root group of a PLC program has one too.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>The groups directly under this one.</summary>
        public IReadOnlyList<BlockGroupDescription> Groups { get; init; } = new List<BlockGroupDescription>();

        /// <summary>The blocks directly in this group, not those in its subgroups.</summary>
        public IReadOnlyList<BlockDescription> Blocks { get; init; } = new List<BlockDescription>();
    }
}
