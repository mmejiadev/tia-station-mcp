using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Outcome of a source snapshot export. A snapshot is deliberately allowed to be partial:
    /// a program almost always contains blocks that have no text representation, and failing the
    /// whole export because of one of them would make the operation useless. Everything that did
    /// not make it into the snapshot is reported here instead of being silently dropped.
    /// </summary>
    public sealed class SnapshotResult
    {
        /// <summary>Creates a snapshot result.</summary>
        /// <param name="exported">Paths of the files written, relative to the snapshot root.</param>
        /// <param name="inconsistent">Full project paths of items TIA Portal reports as inconsistent.</param>
        /// <param name="unsupported">Full project paths of blocks whose language has no text form.</param>
        /// <param name="failed">Full project paths that failed to export, each with its reason.</param>
        public SnapshotResult(
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

        /// <summary>
        /// Paths of the files written, relative to the snapshot root and using forward slashes so
        /// the list reads the same regardless of the platform that produced it.
        /// </summary>
        public IReadOnlyList<string> Exported { get; }

        /// <summary>
        /// Items skipped because TIA Portal reports them inconsistent. TIA Portal refuses to
        /// export these, and the native error does not say why, so compile the software and
        /// snapshot again.
        /// </summary>
        public IReadOnlyList<string> Inconsistent { get; }

        /// <summary>
        /// Blocks whose programming language has no text representation. LAD, FBD and GRAPH exist
        /// only as SimaticML XML, so they cannot appear in a text snapshot. This list is what tells
        /// you the snapshot does not describe the whole program.
        /// </summary>
        public IReadOnlyList<string> Unsupported { get; }

        /// <summary>Items that should have exported but did not, each with the reason.</summary>
        public IReadOnlyList<string> Failed { get; }

        /// <summary>True when every item that could be represented as text was written.</summary>
        public bool IsComplete => Failed.Count == 0 && Inconsistent.Count == 0;
    }
}
