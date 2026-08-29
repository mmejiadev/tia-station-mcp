using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TiaMcpServer.Knowledge
{
    /// <summary>
    /// What the documentation says about the equipment a change touches, or an honest statement
    /// that it says nothing.
    /// </summary>
    /// <remarks>
    /// **The gap is never filled.** Every way of constructing this class either carries excerpts
    /// somebody can go and read, or says plainly that there are none. There is no path that
    /// produces a sentence about hardware which is not a span of an indexed document, and that is
    /// what makes the cardinal rule of the knowledge layer testable rather than aspirational.
    ///
    /// Immutable, and the citations are exposed as a read-only view rather than the list behind
    /// them: this is evidence attached to a plan, and evidence a later caller can append to is not
    /// evidence.
    /// </remarks>
    public sealed class HardwareContext
    {
        /// <summary>
        /// The whole of the answer when nothing can be cited.
        /// </summary>
        /// <remarks>
        /// One wording, in one place, so a test can pin it. It matches what the harness prints for
        /// the same situation; two sentences that drift apart would let a reader think the two
        /// surfaces know different things.
        /// </remarks>
        public const string NotFoundAnswer =
            "Not found in the indexed corpus. Open the manufacturer's manual for this equipment.";

        private static readonly HardwareContext NothingFound = new HardwareContext(
            HardwareContextOutcome.NotFound,
            new ReadOnlyCollection<HardwareCitation>(Array.Empty<HardwareCitation>()),
            string.Empty);

        private HardwareContext(
            HardwareContextOutcome outcome,
            IReadOnlyList<HardwareCitation> citations,
            string reason)
        {
            Outcome = outcome;
            Citations = citations;
            Reason = reason;
        }

        /// <summary>What the lookup was able to answer.</summary>
        public HardwareContextOutcome Outcome { get; }

        /// <summary>Why nothing could be looked up, or empty when a lookup did happen.</summary>
        public string Reason { get; }

        /// <summary>The excerpts, empty unless the outcome is <see cref="HardwareContextOutcome.Cited"/>.</summary>
        public IReadOnlyList<HardwareCitation> Citations { get; }

        /// <summary>The answer when the index could cite something.</summary>
        /// <param name="citations">The excerpts, in the order the index ranked them.</param>
        /// <returns>A cited context.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="citations"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="citations"/> is empty.</exception>
        public static HardwareContext Cited(IEnumerable<HardwareCitation> citations)
        {
            if (citations == null)
            {
                throw new ArgumentNullException(nameof(citations));
            }

            var found = citations.ToArray();

            if (found.Length == 0)
            {
                throw new ArgumentException(
                    "A cited context with no citations is a not-found answer",
                    nameof(citations));
            }

            return new HardwareContext(
                HardwareContextOutcome.Cited,
                new ReadOnlyCollection<HardwareCitation>(found),
                string.Empty);
        }

        /// <summary>The answer when the index was asked and had nothing to offer.</summary>
        /// <returns>A not-found context.</returns>
        public static HardwareContext NotFound()
        {
            return NothingFound;
        }

        /// <summary>The answer when no lookup could be performed at all.</summary>
        /// <param name="reason">Why not, in a sentence a person can act on.</param>
        /// <returns>An unavailable context carrying the reason.</returns>
        /// <exception cref="ArgumentException"><paramref name="reason"/> is empty.</exception>
        /// <remarks>
        /// The reason is required rather than optional. "No hardware context" with no explanation is
        /// the shape a silently broken lookup would take, and it would be indistinguishable from a
        /// machine that was never given an index.
        /// </remarks>
        public static HardwareContext Unavailable(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("An unavailable context has to say why", nameof(reason));
            }

            return new HardwareContext(
                HardwareContextOutcome.Unavailable,
                new ReadOnlyCollection<HardwareCitation>(Array.Empty<HardwareCitation>()),
                reason);
        }

        /// <summary>
        /// Builds a context with an outcome none of the factories can produce, so the exhaustive
        /// switch in <see cref="Summarise"/> can be tested.
        /// </summary>
        /// <param name="outcome">The outcome to stand in, including one no case handles.</param>
        /// <returns>A context carrying that outcome and nothing else.</returns>
        /// <remarks>
        /// Internal, and it exists only for the test that asserts an unrecognised outcome throws.
        /// That rule cannot be checked through the public factories, because their whole job is to
        /// make such a value unreachable — and a safety rule nobody asserts is one that quietly
        /// stops holding.
        /// </remarks>
        internal static HardwareContext WithOutcome(HardwareContextOutcome outcome)
        {
            return new HardwareContext(
                outcome,
                new ReadOnlyCollection<HardwareCitation>(Array.Empty<HardwareCitation>()),
                string.Empty);
        }

        /// <summary>A one-line summary for a plan a person is reading.</summary>
        /// <returns>The description.</returns>
        /// <exception cref="InvalidOperationException">The outcome is not one this method knows.</exception>
        /// <remarks>
        /// Exhaustive by construction: a fourth outcome added later throws here instead of falling
        /// through to a reassuring default. A plan that quietly described an unrecognised state as
        /// "no hardware context" would be the silent default the governance layer forbids.
        ///
        /// A named method rather than a <c>ToString</c> override, because this one is allowed to
        /// throw and <c>ToString</c> is not: a debugger or a log formatter calls <c>ToString</c> on
        /// its own initiative, and a type that can throw from there fails in places nobody wrote.
        /// </remarks>
        public string Summarise()
        {
            switch (Outcome)
            {
                case HardwareContextOutcome.Cited:
                    return $"{Citations.Count} citation(s): {string.Join("; ", Citations.Select(one => one.ToString()))}";

                case HardwareContextOutcome.NotFound:
                    return NotFoundAnswer;

                case HardwareContextOutcome.Unavailable:
                    return $"No hardware context: {Reason}";

                default:
                    throw new InvalidOperationException($"Unrecognised hardware context outcome: {Outcome}");
            }
        }
    }
}
