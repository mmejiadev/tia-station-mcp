using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Siemens
{
    /// <summary>The values an edit sets in place had before it, to put each back on its own.</summary>
    /// <remarks>
    /// Read with the reader <c>GetUnifiedScreen</c> uses, which leaves out a value that is not there: a property still
    /// null, a text with no entry in the editing language (review of 2026-11-17). Such a text or string is put back as
    /// empty, which is how the editor shows it - a rich text then reads <c>&lt;body&gt;&lt;p/&gt;&lt;/body&gt;</c> rather than
    /// nothing (measured 2026-11-17 on V21, a tooltip); anything else not read, or read with an error, refuses the edit
    /// before it starts, as it could not be put back.
    /// </remarks>
    internal sealed class UnifiedScreenEditValues
    {
        private const string ScreenKey = "";

        private readonly Dictionary<(string Owner, string Path), UnifiedPropertySetting> _old = new Dictionary<(string, string), UnifiedPropertySetting>();

        /// <summary>Takes the values before of one owner's settings, refusing a setting whose value could not be put back.</summary>
        /// <param name="owner">The item's name; null for the screen.</param>
        /// <param name="read">What the reader read of the owner's properties, and what it could not read.</param>
        /// <param name="settings">The settings the edit makes on it.</param>
        /// <param name="target">The owner itself, for a property's type.</param>
        public void Read(string? owner, (JsonObject? Properties, IReadOnlyList<string> Unreadable) read, IEnumerable<UnifiedPropertySetting> settings, object target)
        {
            foreach (var setting in settings)
            {
                _old[(owner ?? ScreenKey, setting.Path.ToUpperInvariant())] = Before(owner, read, setting, target);
            }
        }

        /// <summary>The value a setting had before.</summary>
        /// <param name="owner">The item's name; null for the screen.</param>
        /// <param name="setting">The setting.</param>
        /// <returns>A setting that puts the value back.</returns>
        public UnifiedPropertySetting Old(string? owner, UnifiedPropertySetting setting)
        {
            return _old[(owner ?? ScreenKey, setting.Path.ToUpperInvariant())];
        }

        private static UnifiedPropertySetting Before(string? owner, (JsonObject? Properties, IReadOnlyList<string> Unreadable) read, UnifiedPropertySetting setting, object target)
        {
            var found = read.Properties?.FirstOrDefault(property => string.Equals(property.Key, setting.Path, StringComparison.OrdinalIgnoreCase));
            if (found?.Value != null)
            {
                return new UnifiedPropertySetting(setting.Path, Element(found.Value.Value!));
            }

            var unreadable = read.Unreadable.Any(entry => entry.StartsWith(setting.Path + ":", StringComparison.OrdinalIgnoreCase));
            var type = UnifiedPropertyWriter.ResolveOnType(target.GetType(), setting.Segments, setting.Path).PropertyType;

            return !unreadable && (type == typeof(string) || type == typeof(MultilingualText))
                ? new UnifiedPropertySetting(setting.Path, Element(JsonValue.Create(string.Empty)!))
                : throw new PortalException(PortalErrorCode.InvalidState, $"Nothing was changed: {(owner == null ? "the screen's" : $"'{owner}'")} {setting.Path} could not be read, so it could not be put back");
        }

        private static JsonElement Element(JsonNode value)
        {
            using var document = JsonDocument.Parse(value.ToJsonString());

            return document.RootElement.Clone();
        }
    }
}
