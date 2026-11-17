using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>Writes a Custom Web Control's <c>manifest.json</c>.</summary>
    /// <remarks>
    /// The shape of Siemens' own controls in TIA Portal V21 (<c>Data\Hmi\CustomControls</c>), measured 2026-11-15:
    /// <c>mver</c> 1.2.0; an identity with name, version, display name, <c>type: guid://GUID</c> and the start page; an
    /// environment for PC and Unified Edge runtimes with the HMI extension and the default size; and the API contract of
    /// methods, events and properties, a colour being a <c>$ref</c>. A pilot light written this way was recognised,
    /// placed, bound, compiled with no error and worked in runtime. No icon is written: <c>Siemens.Context</c> has none
    /// either, and an icon of Siemens' is not this server's to copy.
    /// </remarks>
    public static class CustomWebControlManifestWriter
    {
        /// <summary>The manifest version Siemens' V21 controls declare.</summary>
        public const string ManifestVersion = "1.2.0";

        /// <summary>The page the control starts from, inside its package.</summary>
        public const string StartPage = "./control/index.html";

        private const string ControlVersion = "1.0.0";
        private const string TiaVersion = "17.0.0";
        private const string RuntimeClasses = "PC,UnifiedEdge";
        private const string HmiExtensionVersion = "~1.0.0";
        private const string Author = "tia-station-mcp";

        /// <summary>Writes the manifest.</summary>
        /// <param name="definition">The checked definition.</param>
        /// <returns>The manifest, indented JSON in UTF-8 without a byte order mark.</returns>
        public static byte[] Write(CustomWebControlDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("mver", ManifestVersion);
                writer.WriteStartObject("control");
                WriteIdentity(writer, definition);
                WriteEnvironment(writer, definition.Content);
                WriteMetadata(writer, definition.Identity);
                WriteContracts(writer, definition);
                writer.WriteStartObject("types");
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return stream.ToArray();
        }

        /// <summary>The manifest's text, for reading back in tests and answers.</summary>
        /// <param name="definition">The checked definition.</param>
        /// <returns>The manifest as text.</returns>
        public static string WriteText(CustomWebControlDefinition definition)
        {
            return Encoding.UTF8.GetString(Write(definition));
        }

        private static void WriteIdentity(Utf8JsonWriter writer, CustomWebControlDefinition definition)
        {
            writer.WriteStartObject("identity");
            writer.WriteString("name", definition.Identity.Name);
            writer.WriteString("version", ControlVersion);
            writer.WriteString("displayname", definition.Identity.DisplayName);
            writer.WriteString("type", "guid://" + definition.Identity.PackageId.ToString("D").ToUpperInvariant());
            writer.WriteString("start", StartPage);
            writer.WriteEndObject();
        }

        private static void WriteEnvironment(Utf8JsonWriter writer, CustomWebControlContent content)
        {
            writer.WriteStartObject("environment");
            writer.WriteStartObject("x_tia");
            writer.WriteString("version", TiaVersion);
            writer.WriteString("class", RuntimeClasses);
            writer.WriteEndObject();
            writer.WriteStartObject("extensions");
            writer.WriteStartObject("HMI");
            writer.WriteBoolean("mandatory", true);
            writer.WriteString("version", HmiExtensionVersion);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteStartObject("prerequisites");
            writer.WriteStartObject("renderingspace");
            writer.WriteNumber("defaultwidth", content.Width);
            writer.WriteNumber("defaultheight", content.Height);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static void WriteMetadata(Utf8JsonWriter writer, CustomWebControlIdentity identity)
        {
            writer.WriteStartObject("metadata");
            writer.WriteString("author", Author);
            writer.WriteString("description", identity.Description);
            writer.WriteStartArray("keywords");
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteContracts(Utf8JsonWriter writer, CustomWebControlDefinition definition)
        {
            writer.WriteStartObject("contracts");
            writer.WriteStartObject("api");
            writer.WriteStartObject("methods");
            writer.WriteEndObject();
            WriteEvents(writer, definition);
            WriteProperties(writer, definition);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static void WriteEvents(Utf8JsonWriter writer, CustomWebControlDefinition definition)
        {
            writer.WriteStartObject("events");
            foreach (var fired in definition.Events)
            {
                writer.WriteStartObject(fired.Name);
                writer.WriteString("description", string.Empty);
                writer.WriteStartObject("arguments");
                foreach (var argument in fired.Arguments)
                {
                    writer.WriteStartObject(argument.Name);
                    WriteKind(writer, argument.Kind);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void WriteProperties(Utf8JsonWriter writer, CustomWebControlDefinition definition)
        {
            writer.WriteStartObject("properties");
            foreach (var property in definition.Properties)
            {
                writer.WriteStartObject(property.Name);
                WriteKind(writer, property.Kind);
                writer.WritePropertyName("default");
                property.Default.WriteTo(writer);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void WriteKind(Utf8JsonWriter writer, CustomWebControlPropertyKind kind)
        {
            var schemaType = CustomWebControlPropertyKinds.SchemaType(kind);

            if (schemaType == null)
            {
                writer.WriteString("$ref", CustomWebControlPropertyKinds.ColorReference);
                return;
            }

            writer.WriteString("type", schemaType);
        }
    }
}
