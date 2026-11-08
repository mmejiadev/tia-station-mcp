using System;

namespace TiaMcpServer.Library
{
    /// <summary>The object of a PLC program a master copy is made from.</summary>
    public sealed class MasterCopySource
    {
        /// <summary>Creates the description of a source.</summary>
        /// <param name="kind">What the object is.</param>
        /// <param name="path">Its full path in the program, e.g. <c>Cell/FB_Motor</c>.</param>
        public MasterCopySource(MasterCopySourceKind kind, string path)
        {
            Kind = kind;
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        /// <summary>What the object is.</summary>
        public MasterCopySourceKind Kind { get; }

        /// <summary>Its full path in the program.</summary>
        public string Path { get; }
    }
}
