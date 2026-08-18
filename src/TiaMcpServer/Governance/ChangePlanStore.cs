using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// Holds plans that are waiting to be confirmed.
    /// </summary>
    /// <remarks>
    /// In memory and for this session only, deliberately. A plan that survives a restart is a
    /// permission granted in a context that no longer exists — the project may have moved on, and
    /// the person who approved it may not be at the machine any more.
    ///
    /// Taking a plan removes it: a confirmation is spent when it is used. Replaying one would let
    /// a single approval authorise a second write nobody looked at.
    /// </remarks>
    public sealed class ChangePlanStore
    {
        private readonly Dictionary<PlanId, PendingChange> _pending = new Dictionary<PlanId, PendingChange>();
        private readonly ISystemClock _clock;

        /// <summary>Creates a store.</summary>
        /// <param name="clock">Where expiry is measured against.</param>
        /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
        public ChangePlanStore(ISystemClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>Remembers a plan until it is confirmed or expires.</summary>
        /// <param name="plan">The plan.</param>
        /// <param name="execute">What running it does.</param>
        /// <exception cref="ArgumentNullException"><paramref name="plan"/> or <paramref name="execute"/> is null.</exception>
        public void Add(ChangePlan plan, Func<string> execute)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            _pending[plan.Id] = new PendingChange(plan, execute);
        }

        /// <summary>Takes a plan out to run it.</summary>
        /// <param name="id">Which plan.</param>
        /// <returns>The plan and what running it does.</returns>
        /// <remarks>
        /// Removes it whether or not the caller goes on to run it. A plan handed out twice is a
        /// confirmation counted twice.
        /// </remarks>
        /// <exception cref="PortalException">No such plan, or it has expired.</exception>
        public PendingChange Take(PlanId id)
        {
            if (!_pending.TryGetValue(id, out var pending))
            {
                throw new PortalException(
                    PortalErrorCode.NotFound,
                    $"No plan '{id}' is waiting. It may have been confirmed already, or expired, or never existed.");
            }

            _pending.Remove(id);

            if (!pending.Plan.IsConfirmableAt(_clock.UtcNow))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"Plan '{id}' expired at {pending.Plan.Expiry:u}. Propose the change again and confirm the new plan: " +
                    "an old confirmation may no longer describe what would happen.");
            }

            return pending;
        }

        /// <summary>The plans still waiting, oldest first.</summary>
        /// <returns>Every unexpired plan.</returns>
        public IReadOnlyList<ChangePlan> Pending()
        {
            var now = _clock.UtcNow;

            return _pending.Values
                .Select(pending => pending.Plan)
                .Where(plan => plan.IsConfirmableAt(now))
                .OrderBy(plan => plan.Expiry)
                .ToList();
        }

        /// <summary>Forgets plans that can no longer be confirmed.</summary>
        /// <returns>How many were dropped.</returns>
        public int PurgeExpired()
        {
            var now = _clock.UtcNow;
            var expired = _pending.Where(entry => !entry.Value.Plan.IsConfirmableAt(now)).Select(entry => entry.Key).ToList();

            foreach (var id in expired)
            {
                _pending.Remove(id);
            }

            return expired.Count;
        }
    }
}
