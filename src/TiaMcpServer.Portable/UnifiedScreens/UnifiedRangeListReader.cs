using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Reads the <c>ranges</c> of a range animation, refusing the ones TIA Portal would take and never match.</summary>
    /// <remarks>
    /// <code>"ranges": [ { "from": 0, "to": 20, "value": "yellow" }, { "from": 80, "to": 100, "value": "red", "flashing": { "rate": "fast", "alternate": "white" } } ]</code>
    /// <c>to</c> left out is <c>from</c>: one value, as a Bool's 0 and 1. TIA Portal V20 takes a range
    /// whose start is past its end, and ranges that overlap (measured 2026-10-25); the first never
    /// matches and the second leaves which value wins to the runtime, so both are refused here.
    /// </remarks>
    public static class UnifiedRangeListReader
    {
        private static readonly string[] Fields = { "from", "to", "value", "flashing" };

        private static readonly string[] FlashingFields = { "rate", "alternate" };

        /// <summary>Reads the ranges.</summary>
        /// <param name="ranges">The <c>ranges</c> JSON.</param>
        /// <param name="what">The animation, as a refusal names it.</param>
        /// <returns>The ranges, in the order given.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming what is wrong.</exception>
        public static IReadOnlyList<UnifiedValueRange> Read(JsonElement ranges, string what)
        {
            if (ranges.ValueKind != JsonValueKind.Array || ranges.GetArrayLength() == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'ranges' must list at least one range, as [{{ \"from\": 0, \"value\": \"red\" }}]");
            }

            var read = ranges.EnumerateArray().Select((range, index) => ReadRange(range, $"{what}, range {index + 1}")).ToList();
            RequireApart(read, what);

            return read;
        }

        private static UnifiedValueRange ReadRange(JsonElement range, string what)
        {
            if (range.ValueKind != JsonValueKind.Object)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be a JSON object");
            }

            var stray = range.EnumerateObject().Select(property => property.Name).Where(name => !Fields.Contains(name)).ToList();
            if (stray.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} has fields this tool does not set: {string.Join(", ", stray)}");
            }

            var from = Bound(range, "from", what) ?? throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'from' is required");
            var to = Bound(range, "to", what) ?? from;
            if (to < from)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} runs from {from} to {to}, which no value is in; write the smaller bound in 'from'");
            }

            var value = range.TryGetProperty("value", out var given) && given.ValueKind != JsonValueKind.Null
                ? given
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'value' is required, the value the property takes in the range");

            return new UnifiedValueRange(from, to, value, ReadFlashing(range, what));
        }

        /// <remarks>A bound is a whole number: TIA Portal V20 refuses a floating-point bound on every tag type.</remarks>
        private static long? Bound(JsonElement range, string field, string what)
        {
            if (!range.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var bound)
                ? bound
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{field}' takes a whole number, the only bound TIA Portal takes; got {value.GetRawText()}");
        }

        private static UnifiedRangeFlashing? ReadFlashing(JsonElement range, string what)
        {
            if (!range.TryGetProperty("flashing", out var flashing) || flashing.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var stray = flashing.ValueKind == JsonValueKind.Object ? flashing.EnumerateObject().Select(property => property.Name).Where(name => !FlashingFields.Contains(name)).ToList() : null;
            if (stray == null || stray.Count > 0 || !flashing.TryGetProperty("alternate", out var alternate))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'flashing' is {{ \"alternate\": colour, \"rate\": \"slow\" | \"medium\" | \"fast\" }}");
            }

            var rate = UnifiedAnimationListReader.OneOf(flashing, "rate", UnifiedFlashingAnimation.Rates, "Medium", $"{what}: 'flashing'");

            return new UnifiedRangeFlashing(rate, alternate);
        }

        private static void RequireApart(IReadOnlyList<UnifiedValueRange> ranges, string what)
        {
            var ordered = ranges.OrderBy(range => range.From).ToList();

            for (var index = 1; index < ordered.Count; index++)
            {
                if (ordered[index].From <= ordered[index - 1].To)
                {
                    throw new PortalException(
                        PortalErrorCode.InvalidParams,
                        $"{what}: the ranges {ordered[index - 1].From}..{ordered[index - 1].To} and {ordered[index].From}..{ordered[index].To} overlap, so which value wins would be left to the runtime");
                }
            }
        }
    }
}
