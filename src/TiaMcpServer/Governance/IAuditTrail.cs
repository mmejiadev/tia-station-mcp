using System.Collections.Generic;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// Where every audited operation is written down.
    /// </summary>
    /// <remarks>
    /// An interface because the storage will change and the rule will not. It starts as
    /// append-only JSONL, which needs no dependency and can be read with any text editor — the
    /// property that matters most for something whose job is to be trusted. Phase 3 needs
    /// queries for the five Workshop-gate criteria, and that is when SQLite earns its place:
    /// swapping the implementation, not rewriting the layer.
    ///
    /// **Writing must be able to fail loudly.** In Workshop Mode a failed write refuses the
    /// action, so an implementation that silently drops entries would defeat the whole design.
    /// </remarks>
    public interface IAuditTrail
    {
        /// <summary>Appends an entry.</summary>
        /// <param name="entry">The entry to record.</param>
        /// <exception cref="Siemens.PortalException">The entry could not be written.</exception>
        void Append(AuditEntry entry);

        /// <summary>Reads the trail back, oldest first.</summary>
        /// <returns>Every entry recorded so far.</returns>
        /// <remarks>
        /// Reading is part of the contract, not a convenience: an audit trail nobody can read is
        /// not an audit trail, and the Workshop gate is a question asked of this data.
        /// </remarks>
        IReadOnlyList<AuditEntry> Read();
    }
}
