namespace TiaMcpServer.History
{
    /// <summary>
    /// An append-only record of facts of one kind: compilations, projects opened.
    /// </summary>
    /// <typeparam name="TRecord">The kind of fact recorded.</typeparam>
    /// <remarks>
    /// What happened to a project, as opposed to what was decided about it, which is the audit
    /// trail's business. The web platform imports both (docs/WEB-PLATFORM.md).
    /// </remarks>
    public interface IJournal<in TRecord>
    {
        /// <summary>Appends one record.</summary>
        /// <param name="record">The fact to record.</param>
        /// <exception cref="Siemens.PortalException">The record could not be written.</exception>
        void Append(TRecord record);
    }
}
