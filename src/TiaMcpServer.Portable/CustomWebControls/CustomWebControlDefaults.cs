using System.Drawing;
using System.Globalization;
using System.Text.Json;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>Checks a property's default against its kind, and gives one when none is written.</summary>
    /// <remarks>
    /// The default is what the control shows in TIA Portal's editor and in runtime until something is set or bound, and the
    /// manifest declares it, so a default of the wrong kind would be a control that disagrees with its own interface. A
    /// colour is written as screens write one and stored as the ARGB number a manifest holds; an ARGB number is taken too.
    /// </remarks>
    public static class CustomWebControlDefaults
    {
        /// <summary>Opaque black, the colour default when none is written.</summary>
        public const uint OpaqueBlack = 0xFF000000;

        /// <summary>Checks a default, or gives the kind's own.</summary>
        /// <param name="kind">The property's kind.</param>
        /// <param name="given">The default written, or null.</param>
        /// <param name="what">The property, in a refusal.</param>
        /// <returns>The default as JSON, a colour as its ARGB number.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when it does not fit the kind.</exception>
        public static JsonElement Check(CustomWebControlPropertyKind kind, JsonElement? given, string what)
        {
            if (given == null || given.Value.ValueKind == JsonValueKind.Null)
            {
                return Of(kind);
            }

            var value = given.Value;

            return kind switch
            {
                CustomWebControlPropertyKind.Boolean => value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False ? value.Clone() : throw Refuse(what, "true or false"),
                CustomWebControlPropertyKind.Text => value.ValueKind == JsonValueKind.String ? value.Clone() : throw Refuse(what, "a text"),
                CustomWebControlPropertyKind.Number => value.ValueKind == JsonValueKind.Number ? value.Clone() : throw Refuse(what, "a number"),
                CustomWebControlPropertyKind.WholeNumber => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _) ? value.Clone() : throw Refuse(what, "a whole number"),
                CustomWebControlPropertyKind.Color => Number(ToArgb(value, what)),
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised property kind: {kind}")
            };
        }

        /// <summary>A colour as the ARGB number a manifest and a script hold.</summary>
        /// <param name="color">The colour.</param>
        /// <returns>Its ARGB number, alpha in the top byte.</returns>
        public static uint Argb(Color color)
        {
            return unchecked((uint)color.ToArgb());
        }

        private static uint ToArgb(JsonElement value, string what)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var argb))
            {
                return argb;
            }

            return Argb((Color)UnifiedPropertyConverter.Convert(typeof(Color), value, what));
        }

        private static JsonElement Of(CustomWebControlPropertyKind kind)
        {
            return kind switch
            {
                CustomWebControlPropertyKind.Boolean => Json("false"),
                CustomWebControlPropertyKind.Text => Json("\"\""),
                CustomWebControlPropertyKind.Number => Json("0"),
                CustomWebControlPropertyKind.WholeNumber => Json("0"),
                CustomWebControlPropertyKind.Color => Number(OpaqueBlack),
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised property kind: {kind}")
            };
        }

        private static JsonElement Number(uint value)
        {
            return Json(value.ToString(CultureInfo.InvariantCulture));
        }

        private static JsonElement Json(string text)
        {
            using var document = JsonDocument.Parse(text);

            return document.RootElement.Clone();
        }

        private static PortalException Refuse(string what, string expected)
        {
            return new PortalException(PortalErrorCode.InvalidParams, $"The default of {what} must be {expected}");
        }
    }
}
