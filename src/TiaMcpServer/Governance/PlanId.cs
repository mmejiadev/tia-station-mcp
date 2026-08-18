using System;
using System.Security.Cryptography;

namespace TiaMcpServer.Governance
{
    /// <summary>
    /// Identifies one planned change.
    /// </summary>
    /// <remarks>
    /// A type of its own rather than a bare <see cref="Guid"/> or string, because confirming the
    /// wrong plan is exactly the class of mistake that must be impossible, and the compiler makes
    /// it impossible for free.
    ///
    /// Rendered short and in groups: in Workshop Mode a person reads this off one screen and types
    /// it on another, and a 36-character GUID invites copy-paste or, worse, approximation.
    /// Ambiguous characters are left out of the alphabet for the same reason.
    /// </remarks>
    public readonly struct PlanId : IEquatable<PlanId>
    {
        // No I, O, 0 or 1: they are the characters people mistype when reading aloud or off a
        // screen, which is precisely how this identifier is used.
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int GroupLength = 3;
        private const int GroupCount = 2;

        private readonly string _value;

        private PlanId(string value)
        {
            _value = value;
        }

        /// <summary>The identifier as text, for example <c>K7M-2QX</c>.</summary>
        public string Value => _value ?? string.Empty;

        /// <summary>Creates a new identifier.</summary>
        /// <returns>A fresh identifier.</returns>
        /// <remarks>
        /// From a cryptographic generator rather than <see cref="Random"/>. This is not a secret —
        /// it is printed, read aloud and typed back — but guessing a pending plan's id is guessing
        /// a confirmation, and "it is only a handle" is the argument that turns into an incident.
        /// The cost of the stronger generator here is nothing.
        /// </remarks>
        public static PlanId Create()
        {
            var groups = new string[GroupCount];

            using (var generator = RandomNumberGenerator.Create())
            {
                for (var group = 0; group < GroupCount; group++)
                {
                    groups[group] = NextGroup(generator);
                }
            }

            return new PlanId(string.Join("-", groups));
        }

        /// <summary>Reads an identifier back from text.</summary>
        /// <param name="value">The text form.</param>
        /// <returns>The identifier.</returns>
        /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
        public static PlanId Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A plan id cannot be empty", nameof(value));
            }

            // Case-insensitive on purpose: a person typing this back should not be defeated by
            // shift. Whitespace goes for the same reason.
            return new PlanId(value.Trim().ToUpperInvariant());
        }

        /// <inheritdoc />
        public bool Equals(PlanId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is PlanId other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Value;
        }

        /// <summary>Compares two identifiers.</summary>
        /// <param name="left">First identifier.</param>
        /// <param name="right">Second identifier.</param>
        /// <returns>True when they are the same.</returns>
        public static bool operator ==(PlanId left, PlanId right)
        {
            return left.Equals(right);
        }

        /// <summary>Compares two identifiers.</summary>
        /// <param name="left">First identifier.</param>
        /// <param name="right">Second identifier.</param>
        /// <returns>True when they differ.</returns>
        public static bool operator !=(PlanId left, PlanId right)
        {
            return !left.Equals(right);
        }

        private static string NextGroup(RandomNumberGenerator generator)
        {
            var bytes = new byte[GroupLength];

            generator.GetBytes(bytes);

            var characters = new char[GroupLength];

            for (var index = 0; index < GroupLength; index++)
            {
                // The alphabet has 32 entries and a byte has 256 values, so the modulo divides
                // evenly and introduces no bias. That is why the alphabet is a power of two.
                characters[index] = Alphabet[bytes[index] % Alphabet.Length];
            }

            return new string(characters);
        }
    }
}
