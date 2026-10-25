using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>The named functions an event may run, written as the WinCC Unified JavaScript they stand for.</summary>
    /// <remarks>
    /// Each one is the runtime call measured on TIA Portal V20 on 2026-10-23 — written into an event,
    /// passed <c>SyntaxCheck()</c> and compiled: <c>Tags("T").Write(1)</c>, <c>Tags("T").Write(0)</c>,
    /// <c>HMIRuntime.UI.SysFct.ChangeScreen("S", null)</c>, and with a screen window path relative to the
    /// screen the item is on, <c>"../Screen window_3"</c>, on a class project. To confirm on V21.
    /// </remarks>
    public static class UnifiedEventFunctions
    {
        private static readonly Dictionary<string, Func<JsonElement, string, string>> Functions =
            new Dictionary<string, Func<JsonElement, string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["setBit"] = (entry, what) => $"Tags({Literal(RequireText(entry, "tag", what))}).Write(1);",
                ["resetBit"] = (entry, what) => $"Tags({Literal(RequireText(entry, "tag", what))}).Write(0);",
                ["setTag"] = (entry, what) => $"Tags({Literal(RequireText(entry, "tag", what))}).Write({RequireNumber(entry, "value", what)});",
                ["changeScreen"] = (entry, what) => $"HMIRuntime.UI.SysFct.ChangeScreen({Literal(RequireText(entry, "screen", what))}, {Window(entry, what)});"
            };

        /// <summary>The JavaScript for one function entry.</summary>
        /// <param name="entry">The entry: <c>{ "function": "setBit", "tag": "Motor" }</c>.</param>
        /// <param name="what">The entry, as a refusal names it.</param>
        /// <returns>One line of JavaScript.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for an unknown function or a missing argument.</exception>
        public static string Write(JsonElement entry, string what)
        {
            var name = entry.TryGetProperty("function", out var function) && function.ValueKind == JsonValueKind.String ? function.GetString()!.Trim() : string.Empty;

            return Functions.TryGetValue(name, out var write)
                ? write(entry, what)
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'function' must be one of: {string.Join(", ", Functions.Keys)}; or give a 'script'");
        }

        private static string Window(JsonElement entry, string what)
        {
            if (!entry.TryGetProperty("window", out var window) || window.ValueKind == JsonValueKind.Null)
            {
                return "null";
            }

            return Literal(RequireText(entry, "window", what));
        }

        private static string RequireText(JsonElement entry, string field, string what)
        {
            return entry.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!.Trim()
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{field}' is required, as text");
        }

        private static string RequireNumber(JsonElement entry, string field, string what)
        {
            return entry.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
                ? number.ToString("R", CultureInfo.InvariantCulture)
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{field}' is required, as a number");
        }

        /// <summary>A JavaScript string literal a name cannot break out of.</summary>
        /// <remarks>
        /// Quotes and backslashes are escaped, and every control character and the two line separators
        /// U+2028 and U+2029 (line terminators inside older JavaScript strings) are written as <c>\uXXXX</c>.
        /// </remarks>
        private static string Literal(string text)
        {
            var escaped = string.Concat(text.Select(character => character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                _ when character < ' ' || character == (char)0x2028 || character == (char)0x2029 => $"\\u{(int)character:X4}",
                _ => character.ToString()
            }));

            return $"\"{escaped}\"";
        }
    }
}
