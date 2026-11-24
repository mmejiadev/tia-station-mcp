using System;
using System.Collections.Generic;

namespace TiaMcpServer.Scl
{
    /// <summary>What the PEEK and POKE rules know of a program's data blocks: which exist, and which have optimized access.</summary>
    /// <remarks>
    /// A parameter object rather than two loose collections of numbers, which would be easy to pass in the wrong order: one
    /// says a DB is fine to name, the other that it is not. When nothing was read from the project, which blocks exist is
    /// <see cref="Unknown"/>, and a DB is then never said to be missing - the absence of a list is not an empty program.
    /// </remarks>
    public sealed class ProgramDataBlocks
    {
        private readonly HashSet<int>? _existing;
        private readonly HashSet<int> _optimized;

        private ProgramDataBlocks(HashSet<int>? existing, HashSet<int> optimized)
        {
            _existing = existing;
            _optimized = optimized;
        }

        /// <summary>Nothing read from the project: no DB is said to be missing or optimized, only the source text is judged.</summary>
        public static ProgramDataBlocks Unknown { get; } = new ProgramDataBlocks(null, new HashSet<int>());

        /// <summary>The data blocks read from a program.</summary>
        /// <param name="existing">The number of every data block the program holds.</param>
        /// <param name="optimized">The numbers of those with optimized access; it may hold only the ones the source names.</param>
        /// <returns>What the rules know.</returns>
        /// <exception cref="ArgumentNullException">Either collection is null.</exception>
        public static ProgramDataBlocks Of(IEnumerable<int> existing, IEnumerable<int> optimized)
        {
            if (existing == null)
            {
                throw new ArgumentNullException(nameof(existing));
            }

            if (optimized == null)
            {
                throw new ArgumentNullException(nameof(optimized));
            }

            return new ProgramDataBlocks(new HashSet<int>(existing), new HashSet<int>(optimized));
        }

        /// <summary>Whether a data block number is known to have optimized access.</summary>
        /// <param name="number">The number, as written in the source.</param>
        /// <returns>True only when the project says so.</returns>
        public bool IsOptimized(long number)
        {
            return number >= 0 && number <= int.MaxValue && _optimized.Contains((int)number);
        }

        /// <summary>Whether a data block number is known to name no data block of the program.</summary>
        /// <param name="number">The number, as written in the source.</param>
        /// <returns>True only when the program's data blocks were read and none has the number.</returns>
        public bool IsMissing(long number)
        {
            return _existing != null && !(number >= 0 && number <= int.MaxValue && _existing.Contains((int)number));
        }
    }
}
