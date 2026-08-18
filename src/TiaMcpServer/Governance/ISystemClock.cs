using System;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// Where this layer gets the time from.
    /// </summary>
    /// <remarks>
    /// An interface rather than <c>DateTimeOffset.UtcNow</c> at the point of use, because plan
    /// expiry and audit timestamps are both behaviour worth testing, and neither can be tested
    /// against a clock that cannot be moved.
    /// </remarks>
    public interface ISystemClock
    {
        /// <summary>The current moment, in UTC.</summary>
        /// <remarks>
        /// UTC deliberately: an audit trail read across a daylight-saving boundary must still put
        /// its entries in the order they happened.
        /// </remarks>
        DateTimeOffset UtcNow { get; }
    }
}
