using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>What uses a block, and what it uses.</summary>
    /// <remarks>
    /// Three lists rather than one, because the two questions a caller asks have opposite answers
    /// and the direction is easy to lose. <see cref="Other"/> holds the relations that are neither,
    /// kept rather than dropped: a reference filed under a relation this server does not recognise
    /// must not read as "nobody uses this block".
    /// </remarks>
    public sealed class ResponseCrossReferences : ResponseMessage
    {
        /// <summary>Creates the response.</summary>
        /// <param name="report">The references, already sorted by direction.</param>
        public ResponseCrossReferences(CrossReferenceReport report)
        {
            UsedBy = Render(report.Incoming);
            Uses = Render(report.Outgoing);
            Other = Render(report.Other);
        }

        /// <summary>
        /// What calls or reads the block, one line per place. These are what breaks if its
        /// interface changes.
        /// </summary>
        public IReadOnlyList<string> UsedBy { get; }

        /// <summary>What the block itself calls or reads, one line per place.</summary>
        public IReadOnlyList<string> Uses { get; }

        /// <summary>Relations that are neither, named by the relation Openness reported.</summary>
        public IReadOnlyList<string> Other { get; }

        private static List<string> Render(IReadOnlyList<CrossReferenceInfo> references)
        {
            return references.Select(reference => reference.Line).ToList();
        }
    }
}
