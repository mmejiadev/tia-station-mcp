using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>Reads a property kind from what a definition or a manifest writes, and writes it back.</summary>
    public static class CustomWebControlPropertyKinds
    {
        /// <summary>The <c>$ref</c> WinCC Unified gives a colour property in a manifest.</summary>
        public const string ColorReference = "http://tia.siemens.com/wincc-unified/types/s/color";

        /// <summary>The words accepted for a kind.</summary>
        public const string Accepted = "boolean, string, number, integer, color";

        /// <summary>Reads a kind.</summary>
        /// <param name="text">The kind as written: <c>boolean</c>, <c>string</c>, <c>number</c>, <c>integer</c> or <c>color</c>.</param>
        /// <param name="what">The property, named in a refusal.</param>
        /// <returns>The kind.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> for any other word.</exception>
        public static CustomWebControlPropertyKind Parse(string? text, string what)
        {
            return (text ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "BOOLEAN" => CustomWebControlPropertyKind.Boolean,
                "STRING" => CustomWebControlPropertyKind.Text,
                "NUMBER" => CustomWebControlPropertyKind.Number,
                "INTEGER" => CustomWebControlPropertyKind.WholeNumber,
                "COLOR" or "COLOUR" => CustomWebControlPropertyKind.Color,
                _ => throw new PortalException(PortalErrorCode.InvalidParams, $"{what} has type '{text}'; use one of {Accepted}")
            };
        }

        /// <summary>The JSON schema type a manifest gives a kind; a colour has none, being a <c>$ref</c>.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>E.g. <c>boolean</c>; null for a colour.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> for a kind this does not know.</exception>
        public static string? SchemaType(CustomWebControlPropertyKind kind)
        {
            return kind switch
            {
                CustomWebControlPropertyKind.Boolean => "boolean",
                CustomWebControlPropertyKind.Text => "string",
                CustomWebControlPropertyKind.Number => "number",
                CustomWebControlPropertyKind.WholeNumber => "integer",
                CustomWebControlPropertyKind.Color => null,
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised property kind: {kind}")
            };
        }

        /// <summary>The .NET type an Openness interface property of this kind takes.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>E.g. <see cref="bool"/>, or <see cref="System.Drawing.Color"/> for a colour.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidState"/> for a kind this does not know.</exception>
        /// <remarks>Measured 2026-11-15: a Custom Web Control's interface took a string for a string property and a <c>System.Drawing.Color</c> for a colour one.</remarks>
        public static Type ClrType(CustomWebControlPropertyKind kind)
        {
            return kind switch
            {
                CustomWebControlPropertyKind.Boolean => typeof(bool),
                CustomWebControlPropertyKind.Text => typeof(string),
                CustomWebControlPropertyKind.Number => typeof(double),
                CustomWebControlPropertyKind.WholeNumber => typeof(int),
                CustomWebControlPropertyKind.Color => typeof(System.Drawing.Color),
                _ => throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised property kind: {kind}")
            };
        }
    }
}
