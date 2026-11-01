using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Library
{
    /// <summary>Removes items that may depend on each other, in passes, without knowing who depends on whom.</summary>
    /// <remarks>
    /// A library type another one still depends on cannot be deleted first, and the order a library is walked
    /// in says nothing of who depends on whom. So every pass deletes what it can, and passes repeat while one
    /// removes anything: a chain of n dependencies goes in at most n passes, and whatever no pass can remove is
    /// given back to be named, never hidden.
    /// </remarks>
    public static class RepeatedRemoval
    {
        /// <summary>Removes every item it can.</summary>
        /// <typeparam name="T">The item.</typeparam>
        /// <param name="items">The items, in any order.</param>
        /// <param name="tryRemove">Removes one item; false when it could not.</param>
        /// <returns>The items left.</returns>
        public static IReadOnlyList<T> Run<T>(IEnumerable<T> items, Func<T, bool> tryRemove)
        {
            if (tryRemove == null)
            {
                throw new ArgumentNullException(nameof(tryRemove));
            }

            var remaining = (items ?? Array.Empty<T>()).ToList();
            var removedAny = true;

            while (remaining.Count > 0 && removedAny)
            {
                var stillThere = remaining.Where(item => !tryRemove(item)).ToList();
                removedAny = stillThere.Count < remaining.Count;
                remaining = stillThere;
            }

            return remaining;
        }
    }
}
