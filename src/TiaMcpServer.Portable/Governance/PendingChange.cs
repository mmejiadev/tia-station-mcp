using System;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// A plan and the work it would do.
    /// </summary>
    /// <remarks>
    /// The action is held here rather than reconstructed at confirmation time, and that is the
    /// point: what a person confirms is the work that was described to them, not a fresh attempt
    /// to build the same work again from arguments that may since have changed.
    /// </remarks>
    public sealed class PendingChange
    {
        /// <summary>Pairs a plan with its work.</summary>
        /// <param name="plan">The plan.</param>
        /// <param name="execute">What running it does, returning whatever the caller reports.</param>
        /// <exception cref="ArgumentNullException">Either argument is null.</exception>
        public PendingChange(ChangePlan plan, Func<string> execute)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        /// <summary>The plan.</summary>
        public ChangePlan Plan { get; }

        /// <summary>What running it does.</summary>
        public Func<string> Execute { get; }
    }
}
