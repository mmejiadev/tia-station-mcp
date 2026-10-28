using System;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedTags;

namespace TiaMcpServer.UnifiedAlarms
{
    /// <summary>The JSON reading the Unified alarm readers share, on top of <see cref="UnifiedTagJson"/>.</summary>
    internal static class UnifiedAlarmJson
    {
        /// <summary>The highest priority TIA Portal V20 takes for an alarm or a class; 17 is refused (measured 2026-10-25).</summary>
        public const byte MaxPriority = 16;

        /// <summary>A required name, trimmed.</summary>
        public static string RequireName(JsonElement owner, string what)
        {
            var name = UnifiedTagJson.OptionalString(owner, "name");

            return string.IsNullOrWhiteSpace(name)
                ? throw new PortalException(PortalErrorCode.InvalidParams, $"{what} needs a 'name'")
                : name!.Trim();
        }

        /// <summary>An enum value written by name, e.g. <c>raiseClear</c>, in any case; never a number.</summary>
        public static T? OptionalEnum<T>(JsonElement owner, string field, string what)
            where T : struct, Enum
        {
            var given = UnifiedTagJson.OptionalString(owner, field);
            if (given == null)
            {
                return null;
            }

            var names = Enum.GetNames(typeof(T));
            var known = names.FirstOrDefault(name => name.Equals(given.Trim(), StringComparison.OrdinalIgnoreCase));

            return known != null
                ? (T)Enum.Parse(typeof(T), known)
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{field}' is not '{given}'; use one of: {string.Join(", ", names.Select(Camel))}");
        }

        /// <summary>A priority from 0 to <see cref="MaxPriority"/>.</summary>
        public static byte? OptionalPriority(JsonElement owner, string what)
        {
            if (!owner.TryGetProperty("priority", out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetByte(out var priority) && priority <= MaxPriority
                ? priority
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'priority' runs from 0 to {MaxPriority}; got {value.GetRawText()}");
        }

        /// <summary>A name in the spelling a description uses, e.g. <c>raiseClear</c> for <c>RaiseClear</c>.</summary>
        public static string Camel(string name)
        {
            return string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
