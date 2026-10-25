using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedTags
{
    /// <summary>The JSON reading the Unified tag readers share: objects, known fields, strings and values.</summary>
    internal static class UnifiedTagJson
    {
        /// <summary>Refuses a field nobody reads, rather than ignoring it.</summary>
        public static void RequireKnownFields(JsonElement owner, IReadOnlyCollection<string> known, string what)
        {
            var unknown = owner.EnumerateObject().Select(property => property.Name).Where(name => !known.Contains(name)).ToList();

            if (unknown.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} has fields this tool does not set: {string.Join(", ", unknown)}. It takes: {string.Join(", ", known)}");
            }
        }

        public static JsonElement RequireObject(JsonElement element, string what)
        {
            return element.ValueKind == JsonValueKind.Object
                ? element
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be a JSON object");
        }

        public static string? OptionalString(JsonElement owner, string field)
        {
            if (!owner.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{field}' must be a string; got {value.GetRawText()}");
        }

        public static bool? OptionalBool(JsonElement owner, string field)
        {
            if (!owner.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False
                ? value.GetBoolean()
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{field}' must be true or false; got {value.GetRawText()}");
        }

        /// <summary>A value as the text TIA Portal takes: a number as written, true and false as 1 and 0, a string as it is.</summary>
        public static string ValueText(JsonElement value, string what)
        {
            return value.ValueKind switch
            {
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "1",
                JsonValueKind.False => "0",
                JsonValueKind.String => value.GetString()!,
                _ => throw new PortalException(PortalErrorCode.InvalidParams, $"{what} must be a number, true or false, or a string; got {value.GetRawText()}")
            };
        }

        /// <summary>A pair of numbers written <c>[start, end]</c>.</summary>
        public static (double Start, double End) NumberPair(JsonElement owner, string field, string what)
        {
            if (!owner.TryGetProperty(field, out var pair) || pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2 || pair.EnumerateArray().Any(number => number.ValueKind != JsonValueKind.Number))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{field}' must be [start, end], two numbers");
            }

            return (pair[0].GetDouble(), pair[1].GetDouble());
        }
    }
}
