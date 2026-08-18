using System;
using System.Threading;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Exclusive access to TIA Portal, held until it is disposed.
    /// </summary>
    /// <remarks>
    /// Handed out by <see cref="OpennessGate.Enter"/> and by nothing else, which is why its
    /// constructor is internal: a lease nobody took releases a lock nobody holds.
    ///
    /// A class rather than a struct on purpose. A struct is copied by assignment, and two copies each
    /// releasing the same monitor is a <see cref="SynchronizationLockException"/> at the second one.
    /// One allocation per tool call is nothing next to a call into TIA Portal.
    /// </remarks>
    public sealed class OpennessLease : IDisposable
    {
        private readonly object _sync;

        private bool _released;

        internal OpennessLease(object sync)
        {
            _sync = sync;
        }

        /// <summary>Releases exclusive access to TIA Portal.</summary>
        /// <remarks>
        /// Idempotent, so a second dispose is not an exception. A lease released twice would throw
        /// from inside a <c>using</c>, hiding whatever the tool was really reporting.
        /// </remarks>
        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            Monitor.Exit(_sync);
        }
    }
}
