using System;
using System.Collections.Generic;
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
}
