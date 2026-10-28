using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>Removes what an all-or-nothing Unified write made before it failed, and says what is left.</summary>
    /// <remarks>
    /// A delete that fails must not hide the failure that explains what went wrong, and the caller must
    /// learn what is left, so a retry does not stumble on it: compare RemoveStationAfter. Once the session
    /// has ended there is nothing to undo with; the caller must reopen the project.
    /// </remarks>
    internal static class UnifiedRollback
    {
        /// <summary>Whether the TIA Portal session is gone, so nothing can be undone.</summary>
        /// <param name="failure">The failure.</param>
        /// <returns>True when the session has ended.</returns>
        public static bool IsSessionEnded(Exception failure)
        {
            return failure is NonRecoverableException || failure is EngineeringObjectDisposedException;
        }

        /// <summary>Deletes what was made, last first, and builds the failure to throw.</summary>
        /// <param name="made">What was made, in the order it was made, each with its name and its delete.</param>
        /// <param name="failure">The failure that stopped the write.</param>
        /// <param name="kinds">What was made, as the message names it, e.g. <c>tags, tables and groups</c>.</param>
        /// <returns>The failure, its code kept, saying whether anything is left.</returns>
        public static PortalException Undo(IReadOnlyList<(string Name, Action Delete)> made, Exception failure, string kinds)
        {
            var left = Enumerable.Reverse(made).Where(entry => !TryDelete(entry.Delete)).Select(entry => entry.Name).ToList();
            var code = (failure as PortalException)?.Code ?? PortalErrorCode.WriteFailed;
            var outcome = left.Count == 0
                ? $"Nothing was created: the {kinds} made before it were removed again."
                : $"Removing what was made before it failed for {string.Join(", ", left)}; delete those in TIA Portal before trying again.";

            return new PortalException(code, $"{failure.Message.Trim()} {outcome}", null, failure);
        }

        /// <summary>Deletes one object, reporting rather than throwing when TIA Portal refuses.</summary>
        /// <param name="delete">The delete.</param>
        /// <returns>True when it was deleted.</returns>
        public static bool TryDelete(Action delete)
        {
            try
            {
                delete();
                return true;
            }
            catch (Exception deleteFailure) when (!IsSessionEnded(deleteFailure))
            {
                // Not swallowed: the caller names every object whose delete failed.
                return false;
            }
        }
    }
}
