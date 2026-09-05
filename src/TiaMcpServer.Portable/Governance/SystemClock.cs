using System;

namespace TiaMcpServer.Governance
{
    /// <summary>The machine's clock.</summary>
    public sealed class SystemClock : ISystemClock
    {
        /// <inheritdoc />
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
