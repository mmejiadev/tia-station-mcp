using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>The ready-made Custom Web Controls this server carries: pilot light, motor, tank, valve, gauge, conveyor, button.</summary>
    /// <remarks>
    /// Each is embedded as <c>Catalog\Name.json</c>, the definition <see cref="CustomWebControlReader"/> reads, with its
    /// markup, style and script beside it as <c>Name.html</c>, <c>Name.css</c> and <c>Name.js</c>, so they are written and
    /// reviewed as the languages they are rather than as strings inside JSON. A catalogue control goes through the same
    /// reader as one an author writes: the catalogue gets no check the author does not.
    /// </remarks>
    public static class CustomWebControlCatalog
    {
        /// <summary>The name prefix of every catalogue control, kept for the catalogue alone.</summary>
        public const string Namespace = "TiaStation.";

        private const string ResourceFolder = ".CustomWebControls.Catalog.";
        private const string DefinitionExtension = ".json";

        private static readonly Assembly Holder = typeof(CustomWebControlCatalog).Assembly;

        /// <summary>The catalogue's names, e.g. <c>PilotLight</c>, in order.</summary>
        /// <returns>The names.</returns>
        public static IReadOnlyList<string> Names()
        {
            return Holder.GetManifestResourceNames()
                .Where(resource => resource.Contains(ResourceFolder) && resource.EndsWith(DefinitionExtension, StringComparison.Ordinal))
                .Select(resource => resource.Substring(resource.IndexOf(ResourceFolder, StringComparison.Ordinal) + ResourceFolder.Length))
                .Select(file => file.Substring(0, file.Length - DefinitionExtension.Length))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Reads one catalogue control.</summary>
        /// <param name="name">Its catalogue name, case aside, e.g. <c>Tank</c>.</param>
        /// <returns>Its checked definition.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the catalogue has no such control.</exception>
        public static CustomWebControlDefinition Get(string name)
        {
            var found = Names().FirstOrDefault(known => string.Equals(known, (name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"The catalogue has no control '{name}'. It has: {string.Join(", ", Names())}");

            var definition = JsonNode.Parse(Resource(found + DefinitionExtension))!.AsObject();
            definition["html"] = Resource(found + ".html");
            definition["css"] = Resource(found + ".css");
            definition["script"] = Resource(found + ".js");

            return CustomWebControlReader.Read(definition.ToJsonString());
        }

        /// <summary>Refuses a written definition named in the catalogue's namespace.</summary>
        /// <param name="definition">A definition the caller wrote, not one of the catalogue's.</param>
        /// <returns>The definition.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when its name starts with <see cref="Namespace"/>.</exception>
        /// <remarks>
        /// Review of 2026-11-16: a policy allowing <c>custom-web-controls/TiaStation.*</c> means "the catalogue". A written
        /// definition under that prefix would pass it and, its package id following from the name, replace a catalogue
        /// control with its own script.
        /// </remarks>
        public static CustomWebControlDefinition RequireWritten(CustomWebControlDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return !definition.Identity.Name.StartsWith(Namespace, StringComparison.OrdinalIgnoreCase)
                ? definition
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{definition.Identity.Name}': names starting with '{Namespace}' are the catalogue's; give your control another prefix, e.g. 'Plant.'");
        }

        private static string Resource(string file)
        {
            var resource = Holder.GetManifestResourceNames().FirstOrDefault(found => found.EndsWith(ResourceFolder + file, StringComparison.Ordinal))
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"The catalogue is missing '{file}'");

            using var stream = Holder.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }
}
