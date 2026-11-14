using System.Collections.Generic;

namespace TiaMcpServer.Vci
{
    /// <summary>What exporting a program to a VCI workspace did with each object.</summary>
    public sealed class VciExportReport
    {
        /// <summary>Creates the report.</summary>
        /// <param name="exported">The files written, relative to the workspace's root.</param>
        /// <param name="alreadyMapped">Objects left alone, as they are mapped to a file already.</param>
        /// <param name="inconsistent">Blocks and types not exported, as they do not compile.</param>
        /// <param name="unsupported">Objects the workspace offers no SimaticML file for.</param>
        public VciExportReport(IReadOnlyList<string> exported, IReadOnlyList<string> alreadyMapped, IReadOnlyList<string> inconsistent, IReadOnlyList<string> unsupported)
        {
            Exported = exported;
            AlreadyMapped = alreadyMapped;
            Inconsistent = inconsistent;
            Unsupported = unsupported;
        }

        /// <summary>The files written.</summary>
        public IReadOnlyList<string> Exported { get; }

        /// <summary>Objects mapped to a file already.</summary>
        public IReadOnlyList<string> AlreadyMapped { get; }

        /// <summary>Blocks and types that do not compile.</summary>
        public IReadOnlyList<string> Inconsistent { get; }

        /// <summary>Objects with no SimaticML file.</summary>
        public IReadOnlyList<string> Unsupported { get; }
    }
}
