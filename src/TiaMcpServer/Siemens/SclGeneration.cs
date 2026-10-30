using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>What TIA Portal generated from an SCL source: blocks and PLC data types, kept apart.</summary>
    /// <remarks>
    /// Apart because they are found apart: a name in <see cref="Types"/> passed to a tool that looks up
    /// blocks is not found, and the other way round.
    /// </remarks>
    public sealed class SclGeneration
    {
        /// <summary>Records what a source generated.</summary>
        /// <param name="blocks">Names of the blocks generated.</param>
        /// <param name="types">Names of the PLC data types generated.</param>
        public SclGeneration(IReadOnlyList<string> blocks, IReadOnlyList<string> types)
        {
            Blocks = blocks;
            Types = types;
        }

        /// <summary>Names of the blocks generated.</summary>
        public IReadOnlyList<string> Blocks { get; }

        /// <summary>Names of the PLC data types generated.</summary>
        public IReadOnlyList<string> Types { get; }
    }
}
