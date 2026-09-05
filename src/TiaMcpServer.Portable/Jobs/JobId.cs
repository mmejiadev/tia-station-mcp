using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Jobs
{
    /// <summary>
    /// Identifies one long operation.
    /// </summary>
    /// <remarks>
    /// Its own type rather than a bare string, for the same reason <c>PlanId</c> is: a caller
    /// polling one job with another's identifier is exactly the class of mistake the compiler can
    /// make impossible for free.
    /// </remarks>
    public readonly struct JobId : IEquatable<JobId>
    {
        private JobId(string value)
        {
            Value = value;
        }

        /// <summary>The identifier as the caller sees it.</summary>
        public string Value { get; }

        /// <summary>Mints a new identifier.</summary>
        /// <returns>The identifier.</returns>
        public static JobId Create()
        {
            return new JobId(Guid.NewGuid().ToString("N"));
        }

        /// <summary>Reads an identifier a caller sent back.</summary>
        /// <param name="value">The identifier as text.</param>
        /// <returns>The identifier.</returns>
        /// <exception cref="PortalException"><paramref name="value"/> is not one of ours.</exception>
        public static JobId Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !Guid.TryParseExact(value, "N", out _))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"'{value}' is not a job id. Job ids come from the tool that started the job.");
            }

            return new JobId(value);
        }

        /// <inheritdoc />
        public bool Equals(JobId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is JobId other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>Compares two identifiers.</summary>
        /// <param name="left">First identifier.</param>
        /// <param name="right">Second identifier.</param>
        /// <returns>True when they are the same.</returns>
        public static bool operator ==(JobId left, JobId right)
        {
            return left.Equals(right);
        }

        /// <summary>Compares two identifiers.</summary>
        /// <param name="left">First identifier.</param>
        /// <param name="right">Second identifier.</param>
        /// <returns>True when they differ.</returns>
        public static bool operator !=(JobId left, JobId right)
        {
            return !left.Equals(right);
        }
    }
}
