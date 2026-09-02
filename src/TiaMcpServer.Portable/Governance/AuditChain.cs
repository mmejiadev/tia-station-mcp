using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// Links each audit entry to the one before it, so that editing the past is detectable.
    /// </summary>
    /// <remarks>
    /// The trail was append-only because nothing in this code rewrites it. That is a statement about
    /// the code, not about the file: a `.jsonl` on disk can be edited by anyone with a text editor,
    /// and until this class existed nothing could tell afterwards.
    ///
    /// Each line carries a sequence number, the hash of the previous line, and its own hash over
    /// both plus its fields. Changing one entry changes its hash, which breaks the link the next
    /// entry records — so a single edit invalidates the whole tail, and removing a line leaves a gap
    /// in the sequence. **This detects tampering; it does not prevent it**, and it does not stop
    /// somebody who recomputes the whole chain. Preventing that needs a key this machine does not
    /// have and a place to keep it that is not this machine.
    ///
    /// The hash is taken over a canonical form built here rather than over the JSON text of the
    /// line. Two JSON serialisers can order keys or escape characters differently and produce
    /// different bytes for the same record, which would report tampering that never happened - the
    /// worst possible failure for a check whose only value is that people believe it.
    /// </remarks>
    public static class AuditChain
    {
        /// <summary>The hash recorded for the first entry of a chain, which has no predecessor.</summary>
        public const string Root = "";

        /// <summary>Computes the hash that links one entry to its predecessor.</summary>
        /// <param name="sequence">This entry's position, counting from one.</param>
        /// <param name="previousHash">The previous entry's hash, or <see cref="Root"/> for the first.</param>
        /// <param name="fields">The entry's values, in a fixed order the caller does not vary.</param>
        /// <returns>The hash, lowercase hexadecimal.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fields"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="sequence"/> is below one.</exception>
        public static string Link(long sequence, string previousHash, IReadOnlyList<string> fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            if (sequence < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "A chain counts from one");
            }

            using (var sha = SHA256.Create())
            {
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(Canonical(sequence, previousHash, fields))));
            }
        }

        /// <summary>
        /// Builds the exact string that gets hashed.
        /// </summary>
        /// <param name="sequence">This entry's position.</param>
        /// <param name="previousHash">The previous entry's hash.</param>
        /// <param name="fields">The entry's values, in order.</param>
        /// <returns>A form that depends on the values and on nothing else.</returns>
        /// <remarks>
        /// A JSON array, so the length of every value is encoded by the escaping and no value can be
        /// split or joined with its neighbour. Concatenating with a separator would let a field
        /// containing that separator impersonate two fields, which is how a naive chain is forged
        /// without breaking a single hash.
        /// </remarks>
        private static string Canonical(long sequence, string previousHash, IReadOnlyList<string> fields)
        {
            var parts = new List<string>(fields.Count + 2)
            {
                sequence.ToString(CultureInfo.InvariantCulture),
                previousHash ?? Root
            };

            parts.AddRange(fields);

            return JsonSerializer.Serialize(parts);
        }

        private static string Hex(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);

            foreach (var value in bytes)
            {
                text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }
    }
}
