namespace TiaMcpServer.Knowledge
{
    /// <summary>
    /// The three things a hardware lookup is permitted to answer.
    /// </summary>
    /// <remarks>
    /// <see cref="NotFound"/> and <see cref="Unavailable"/> are separate on purpose, and collapsing
    /// them would lose the distinction that matters to a reader: the index was asked and had
    /// nothing, or the index was never asked at all. The first says something about the corpus, the
    /// second about this machine, and a plan that showed the same sentence for both would let a
    /// missing index masquerade as a documented silence.
    ///
    /// There is no fourth. Retrieval never degrades into prose about the equipment.
    /// </remarks>
    public enum HardwareContextOutcome
    {
        /// <summary>Verbatim excerpts were found, with their document and page.</summary>
        Cited,

        /// <summary>The index was asked and could cite nothing. Open the manual.</summary>
        NotFound,

        /// <summary>No lookup could be performed at all, and the reason says why.</summary>
        Unavailable
    }
}
