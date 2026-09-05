namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// What kind of failure a <see cref="PortalException"/> reports.
    /// </summary>
    /// <remarks>
    /// The code is what decides how the failure reaches the caller, so it is not a label: see
    /// <c>docs/error-model.md</c>. Three categories, and the members below belong to one each.
    /// **Validation** is input the caller can fix and maps to MCP <c>InvalidParams</c>.
    /// **Invalid state** is a real item that does not allow the operation, and maps to
    /// <c>InvalidParams</c> too, with guidance saying what to do first. Everything else is an
    /// **operation failure** — the environment, I/O, or the Openness API — and maps to
    /// <c>InternalError</c> with a concise reason.
    ///
    /// The distinction earns its keep in one place above all: an expected refusal reported as an
    /// operation failure tells the caller to retry something it must not retry.
    /// </remarks>
    public enum PortalErrorCode
    {
        /// <summary>The named project, device, block or type does not exist. Validation.</summary>
        NotFound,

        /// <summary>An export did not produce the file it was asked for. Operation failure.</summary>
        ExportFailed,

        /// <summary>The arguments are missing, empty or malformed. Validation.</summary>
        InvalidParams,

        /// <summary>
        /// The item exists but the operation is not allowed on it yet — no project open, no
        /// connection, an inconsistent block. Invalid state, and the message says what to do first.
        /// </summary>
        InvalidState,

        /// <summary>TIA Portal could not be started or attached to. Operation failure.</summary>
        ConnectFailed,

        /// <summary>Retrieving a project from its archive failed. Operation failure.</summary>
        RetrieveFailed,

        /// <summary>A compilation could not be run. Operation failure.</summary>
        /// <remarks>
        /// Not the same as a compilation that ran and reported errors: that is a result the caller
        /// reads out of a <c>CompilationReport</c>, and it is the loop working, not a failure.
        /// </remarks>
        CompileFailed,

        /// <summary>A write into the project did not complete. Operation failure.</summary>
        WriteFailed,

        /// <summary>A PLCSIM Advanced operation failed. Operation failure.</summary>
        SimulationFailed
    }
}
