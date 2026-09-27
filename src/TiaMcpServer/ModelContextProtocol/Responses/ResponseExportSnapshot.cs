using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Result of exporting a PLC program to text.</summary>
    public sealed class ResponseExportSnapshot : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="exported">Files written, relative to the snapshot root.</param>
        /// <param name="inconsistent">Items skipped because TIA Portal reports them inconsistent.</param>
        /// <param name="unsupported">Blocks whose programming language has no text representation.</param>
        /// <param name="failed">Items that should have exported but did not, each with its reason.</param>
        public ResponseExportSnapshot(
            IReadOnlyList<string> exported,
            IReadOnlyList<string> inconsistent,
            IReadOnlyList<string> unsupported,
            IReadOnlyList<string> failed)
        {
            Exported = exported;
            Inconsistent = inconsistent;
            Unsupported = unsupported;
            Failed = failed;
        }

        /// <summary>Files written, relative to the snapshot root, using forward slashes.</summary>
        public IReadOnlyList<string> Exported { get; }

        /// <summary>Items skipped as inconsistent. Compile the software and export again.</summary>
        public IReadOnlyList<string> Inconsistent { get; }

        /// <summary>
        /// Blocks that cannot appear in a text snapshot at all, because LAD, FBD and GRAPH exist
        /// only as SimaticML XML. A non-empty list means the snapshot does not describe the whole
        /// program, so it is not a backup.
        /// </summary>
        public IReadOnlyList<string> Unsupported { get; }

        /// <summary>Items that should have exported but did not, each with its reason.</summary>
        public IReadOnlyList<string> Failed { get; }
    }
}
