using System;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// What a check of the audit trail's hash chain found.
    /// </summary>
    /// <remarks>
    /// Immutable, like everything else that describes what already happened.
    ///
    /// <see cref="Unchained"/> is reported rather than hidden. Chaining was added on 2026-08-29 to a
    /// trail that already held thousands of entries, and those entries cannot be attested
    /// retroactively — a chain can only vouch for what it covered. Reporting the count is the honest
    /// alternative to either deleting that history or implying it is verified.
    /// </remarks>
    public sealed class AuditChainReport
    {
        /// <summary>Records the result of a check.</summary>
        /// <param name="chained">How many entries carry chain fields and were verified.</param>
        /// <param name="unchained">How many entries precede the chain and cannot be attested.</param>
        /// <param name="brokenAtLine">The one-based line where the chain first fails, or zero when it holds.</param>
        /// <param name="reason">Why it fails, or empty when it holds.</param>
        /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
        public AuditChainReport(int chained, int unchained, int brokenAtLine, string reason)
        {
            if (chained < 0 || unchained < 0 || brokenAtLine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chained), "An audit chain cannot have a negative count");
            }

            Chained = chained;
            Unchained = unchained;
            BrokenAtLine = brokenAtLine;
            Reason = reason ?? string.Empty;
        }

        /// <summary>How many entries were verified against their predecessor.</summary>
        public int Chained { get; }

        /// <summary>How many entries were written before chaining existed, and are not attested.</summary>
        public int Unchained { get; }

        /// <summary>Where the chain first fails, one-based. Zero when it holds.</summary>
        public int BrokenAtLine { get; }

        /// <summary>Why it fails, in a sentence a person can act on. Empty when it holds.</summary>
        public string Reason { get; }

        /// <summary>Whether every chained entry still matches the entry before it.</summary>
        public bool IsIntact
        {
            get { return BrokenAtLine == 0; }
        }

        /// <summary>A one-line summary.</summary>
        /// <returns>The description.</returns>
        public override string ToString()
        {
            if (!IsIntact)
            {
                return $"Audit chain broken at line {BrokenAtLine}: {Reason}";
            }

            return Unchained == 0
                ? $"Audit chain intact over {Chained} entr(ies)."
                : $"Audit chain intact over {Chained} entr(ies); {Unchained} earlier entr(ies) predate chaining and are not attested.";
        }
    }
}
