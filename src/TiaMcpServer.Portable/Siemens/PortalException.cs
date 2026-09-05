using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// The one exception this server throws for anything that went wrong with a project.
    /// </summary>
    /// <remarks>
    /// It carries a <see cref="PortalErrorCode"/> because the MCP layer decides from that code
    /// whether the caller is being told to fix its arguments or that the environment failed.
    ///
    /// Context is **not** attached where it is thrown. Every public method of the portal layer
    /// catches once, fills <see cref="Exception.Data"/> with the paths it was working on, logs, and
    /// rethrows — one decoration point, so no failure arrives half-annotated. See
    /// <c>docs/error-model.md</c>.
    ///
    /// It lives in this assembly rather than beside <c>Portal</c> so that the governance layer can
    /// throw it without dragging in Openness. Nothing here touches Siemens.Engineering; a
    /// <c>PortalException</c> is a plain exception with a code.
    ///
    /// **The standard parameterless and message-only exception constructors are deliberately
    /// absent.** They would build a failure with no <see cref="PortalErrorCode"/>, and the default
    /// value of that enum is <see cref="PortalErrorCode.NotFound"/> — so a caller that forgot the
    /// code would report every failure as a missing item, and the MCP layer would tell the client
    /// to fix a name that was never wrong.
    /// </remarks>
    [Serializable]
    [SuppressMessage(
        "Design",
        "CA1032:Implement standard exception constructors",
        Justification = "A PortalException with no code would default to NotFound, which is the silent default CLAUDE.md forbids. See the remarks.")]
    public class PortalException : Exception
    {
        /// <summary>Which kind of failure this is, and therefore how it reaches the caller.</summary>
        public PortalErrorCode Code { get; }

        /// <summary>
        /// What the caller could have meant, when a lookup failed on a name.
        /// </summary>
        /// <remarks>
        /// A "block not found" that lists the blocks that do exist is the difference between one
        /// round trip and five. Null when the failure has nothing to suggest.
        /// </remarks>
        public IEnumerable<string>? Candidates { get; }

        /// <summary>Records a failure.</summary>
        /// <param name="code">Which kind of failure it is.</param>
        /// <param name="message">Concise and actionable; the structured detail goes to the log.</param>
        /// <param name="candidates">What the caller could have meant, when there is such a list.</param>
        /// <param name="inner">The underlying failure, when there is one.</param>
        public PortalException(PortalErrorCode code, string message, IEnumerable<string>? candidates = null, Exception? inner = null)
            : base(message, inner)
        {
            Code = code;
            Candidates = candidates;
        }

        /// <summary>Deserialization constructor.</summary>
        /// <param name="info">Serialized data.</param>
        /// <param name="context">Streaming context.</param>
        /// <remarks>
        /// <c>[Serializable]</c> without this is a trap rather than a feature: MSTest and the
        /// Openness callback layer both move exceptions across app domain boundaries, and without
        /// it the real exception is replaced by a <c>SerializationException</c> complaining that
        /// no constructor was found. That is how the message naming an unanswered download prompt
        /// went missing — the failure reported the plumbing instead of the problem.
        /// </remarks>
        protected PortalException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Code = (PortalErrorCode)info.GetInt32(nameof(Code));
        }

        /// <inheritdoc />
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            if (info == null)
            {
                return;
            }

            info.AddValue(nameof(Code), (int)Code);
        }
    }
}

