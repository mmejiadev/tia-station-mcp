using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>The named functions an event may run, written as the WinCC Unified JavaScript they stand for.</summary>
    /// <remarks>
    /// Each one is the runtime call measured on TIA Portal V20 on 2026-10-23 — written into an event,
    /// passed <c>SyntaxCheck()</c> and compiled: <c>Tags("T").Write(1)</c>, <c>Tags("T").Write(0)</c>,
    /// <c>HMIRuntime.UI.SysFct.ChangeScreen("S", null)</c>, and with a screen window path relative to the
    /// screen the item is on, <c>"../Screen window_3"</c>, on a class project. To confirm on V21.
    /// Added 2026-11-18: <c>increaseValue</c>, <c>decreaseValue</c> and <c>invertBit</c> read the tag and write it
    /// back; <c>callModule</c> calls a global script module's function the way the TIA Portal V21 help shows - the
    /// module imported in the event's global definitions as <c>import * as M from 'M';</c>, then <c>M.f(...)</c>.
    /// </remarks>
    public static class UnifiedEventFunctions
    {
        private static readonly Dictionary<string, Func<JsonElement, string, string>> Functions =
            new Dictionary<string, Func<JsonElement, string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["setBit"] = (entry, what) => $"Tags({Literal(RequireText(entry, "tag", what))}).Write(1);",
                ["resetBit"] = (entry, what) => $"Tags({Literal(RequireText(entry, "tag", what))}).Write(0);",
                ["setTag"] = (entry, what) => $"Tags({Literal(RequireText(entry, "tag", what))}).Write({RequireNumber(entry, "value", what)});",
                ["changeScreen"] = (entry, what) => $"HMIRuntime.UI.SysFct.ChangeScreen({Literal(RequireText(entry, "screen", what))}, {Window(entry, what)});",
                ["increaseValue"] = (entry, what) => Step(entry, what, "+"),
                ["decreaseValue"] = (entry, what) => Step(entry, what, "-"),
                ["invertBit"] = (entry, what) => $"Tags({Literal(RequireText(entry, "tag", what))}).Write(!Tags({Literal(RequireText(entry, "tag", what))}).Read());",
                ["callModule"] = (entry, what) => $"{RequireIdentifier(entry, "module", what)}.{RequireIdentifier(entry, "call", what)}({Arguments(entry, what)});"
            };

        private const string CallModule = "callModule";

        private static readonly Regex Identifier = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

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

        /// <summary>What a function entry needs in the event's global definitions: the import of the module it calls.</summary>
        /// <param name="entry">The entry.</param>
        /// <param name="what">The entry, as a refusal names it.</param>
        /// <returns>The import line, or null when the function needs none.</returns>
        public static string? Globals(JsonElement entry, string what)
        {
            var name = entry.TryGetProperty("function", out var function) && function.ValueKind == JsonValueKind.String ? function.GetString()!.Trim() : string.Empty;
            if (!string.Equals(name, CallModule, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var module = RequireIdentifier(entry, "module", what);

            // As the TIA Portal V21 help writes it, single quotes: the same import written with other quotes elsewhere
            // on the screen would be a second declaration of the name. The module is a checked name, nothing to escape.
            return $"import * as {module} from '{module}';";
        }

        private static string Step(JsonElement entry, string what, string sign)
        {
            var tag = Literal(RequireText(entry, "tag", what));

            return $"Tags({tag}).Write(Tags({tag}).Read() {sign} {RequireNumber(entry, "value", what)});";
        }

        private static string RequireIdentifier(JsonElement entry, string field, string what)
        {
            var name = RequireText(entry, field, what);

            return Identifier.IsMatch(name)
                ? name
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: '{field}' must be a JavaScript name - letters, digits and '_', not starting with a digit");
        }

        /// <remarks>Numbers, texts and true or false only: anything else is not a value an event would pass.</remarks>
        private static string Arguments(JsonElement entry, string what)
        {
            if (!entry.TryGetProperty("arguments", out var arguments) || arguments.ValueKind == JsonValueKind.Null)
            {
                return string.Empty;
            }

            return arguments.ValueKind == JsonValueKind.Array
                ? string.Join(", ", arguments.EnumerateArray().Select(argument => Argument(argument, what)))
                : throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: 'arguments' must be a list");
        }

        private static string Argument(JsonElement argument, string what)
        {
            return argument.ValueKind switch
            {
                JsonValueKind.Number => argument.GetDouble().ToString("R", CultureInfo.InvariantCulture),
                JsonValueKind.String => Literal(argument.GetString()!),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => throw new PortalException(PortalErrorCode.InvalidParams, $"{what}: an argument is a number, a text, true or false; got {argument.GetRawText()}")
            };
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
