using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// A flat list of cross references sorted into the three answers a caller can act on.
    /// </summary>
    /// <remarks>
    /// Two of the three are the point: what would break if this block changed, and what this block
    /// would take with it. The third exists because Openness has thirteen relations and only two of
    /// them are those, and a reference filed under neither must still be visible. Dropping it would
    /// be the read-side version of a silent default -- the caller would be told the block is used
    /// by nobody when it is used by something this code did not recognise.
    /// </remarks>
    public sealed class CrossReferenceReport
    {
        private CrossReferenceReport(
            IReadOnlyList<CrossReferenceInfo> incoming,
            IReadOnlyList<CrossReferenceInfo> outgoing,
            IReadOnlyList<CrossReferenceInfo> other)
        {
            Incoming = incoming;
            Outgoing = outgoing;
            Other = other;
        }

        /// <summary>Sorts a flat list of references into the report.</summary>
        /// <param name="references">The references, in the order they were read.</param>
        /// <returns>The same references, grouped by direction and none of them discarded.</returns>
        /// <exception cref="PortalException">The list is null.</exception>
        public static CrossReferenceReport Of(IReadOnlyList<CrossReferenceInfo> references)
        {
            if (references == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "references is required");
            }

            return new CrossReferenceReport(
                references.Where(reference => reference.IsIncoming).ToList(),
                references.Where(reference => reference.IsOutgoing).ToList(),
                references.Where(reference => !reference.IsIncoming && !reference.IsOutgoing).ToList());
        }

        /// <summary>What uses the subject, and would break if it changed.</summary>
        public IReadOnlyList<CrossReferenceInfo> Incoming { get; }

        /// <summary>What the subject uses.</summary>
        public IReadOnlyList<CrossReferenceInfo> Outgoing { get; }

        /// <summary>Everything Openness related some other way, kept rather than discarded.</summary>
        public IReadOnlyList<CrossReferenceInfo> Other { get; }

        /// <summary>How many references were read in total.</summary>
        public int Count => Incoming.Count + Outgoing.Count + Other.Count;

        /// <summary>A one-line summary, which is what a caller reads before the lists.</summary>
        public string Summary =>
            $"{Incoming.Count} use(s) of it, {Outgoing.Count} thing(s) it uses, {Other.Count} other relation(s)";
    }
}
