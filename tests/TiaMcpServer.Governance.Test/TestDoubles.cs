using System;
using System.Collections.Generic;
using TiaMcpServer.Knowledge;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance.Tests
{
    /// <summary>A clock that only moves when a test moves it.</summary>
    internal sealed class FixedClock : ISystemClock
    {
        public FixedClock(DateTimeOffset now)
        {
            UtcNow = now;
        }

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan by)
        {
            UtcNow += by;
        }
    }

    /// <summary>A gate in whichever mode a test needs.</summary>
    /// <remarks>
    /// Workshop Mode is compiled out of the ordinary build, so its rules could not otherwise be
    /// exercised at all. See <see cref="IModeGate"/> for why standing in for it is safe.
    /// </remarks>
    internal sealed class StubModeGate : IModeGate
    {
        public StubModeGate(OperationMode mode)
        {
            Mode = mode;
        }

        public OperationMode Mode { get; }

        public Confirmation RequiredConfirmation => ModeGate.ConfirmationFor(Mode);
    }

    /// <summary>An audit trail that keeps entries in memory.</summary>
    internal sealed class RecordingAuditTrail : IAuditTrail
    {
        private readonly List<AuditEntry> _entries = new List<AuditEntry>();

        public IReadOnlyList<AuditEntry> Entries => _entries;

        public void Append(AuditEntry entry)
        {
            _entries.Add(entry);
        }

        public IReadOnlyList<AuditEntry> Read()
        {
            return _entries;
        }
    }

    /// <summary>An audit trail that cannot be written, as a full disk or a locked file would be.</summary>
    internal sealed class UnwritableAuditTrail : IAuditTrail
    {
        public void Append(AuditEntry entry)
        {
            throw new PortalException(PortalErrorCode.InvalidState, "the audit trail is unwritable");
        }

        public IReadOnlyList<AuditEntry> Read()
        {
            return Array.Empty<AuditEntry>();
        }
    }
    /// <summary>A lookup that answers whatever a test hands it, and remembers what it was asked.</summary>
    /// <remarks>
    /// The recorded question is the point of the class, not a convenience: one rule under test is
    /// that a refused change is never looked up, and that can only be asserted by something that
    /// notices being asked.
    /// </remarks>
    internal sealed class StubHardwareLookup : IHardwareLookup
    {
        private readonly HardwareContext _answer;

        public StubHardwareLookup(HardwareContext answer)
        {
            _answer = answer;
        }

        public List<string> Questions { get; } = new List<string>();

        public HardwareContext Describe(string question)
        {
            Questions.Add(question);

            return _answer;
        }
    }

    /// <summary>What a write tool hands back, in the shape the guard has to preserve.</summary>
    /// <remarks>
    /// A double rather than a real response type, and not only to keep this project free of the
    /// assembly that needs TIA Portal to build. <see cref="ModelContextProtocol.GuardedTool"/> is
    /// generic over <see cref="ModelContextProtocol.ResponseMessage"/> and knows nothing about any
    /// particular tool, so a test that named one would be asserting about WriteScl by accident.
    /// </remarks>
    internal sealed class StubToolResponse : ModelContextProtocol.ResponseMessage
    {
        public StubToolResponse(IReadOnlyList<string> generatedBlocks)
        {
            GeneratedBlocks = generatedBlocks;
        }

        /// <summary>The payload a refusal must not produce.</summary>
        public IReadOnlyList<string> GeneratedBlocks { get; }
    }

    /// <summary>A lookup that breaks the way a real one breaks: it reports, it does not throw.</summary>
    internal sealed class BrokenHardwareLookup : IHardwareLookup
    {
        internal const string Reason = "the lookup could not be started: node is not installed";

        public HardwareContext Describe(string question)
        {
            return HardwareContext.Unavailable(Reason);
        }
    }

}
