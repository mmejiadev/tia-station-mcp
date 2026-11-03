using Microsoft.Extensions.Logging;
using Siemens.Engineering.HmiUnified.UI.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Siemens
{
    /// <remarks>
    /// WinCC Unified faceplate instances: reading them, and placing new ones. The faceplate type itself is
    /// drawn by hand in TIA Portal; see <see cref="UnifiedFaceplateWriter"/> and <see cref="UnifiedFaceplateRules"/>
    /// for what was measured.
    /// </remarks>
    public partial class Portal
    {
        /// <summary>Reads every faceplate instance on a WinCC Unified HMI's screens.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software, e.g. <c>PC-System_1/HMI_RT_1</c>.</param>
        /// <returns><c>{ faceplates: [ { screen, name, type, version, left, top, width, height, interface } ] }</c>.</returns>
        /// <exception cref="PortalException">The path is not a WinCC Unified HMI.</exception>
        public JsonObject GetUnifiedFaceplates(string hmiPath)
        {
            try
            {
                return new JsonObject { ["faceplates"] = UnifiedFaceplateReader.Read(AllScreens(RequireUnifiedSoftware(hmiPath))) };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(ex, "GetUnifiedFaceplates", hmiPath, string.Empty);
            }
        }

        /// <summary>Moves every instance of a faceplate type on a WinCC Unified HMI to another released version, all or none.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <param name="typeName">The faceplate's library type.</param>
        /// <param name="version">The released version, or null for the newest.</param>
        /// <param name="backupDirectory">Where each instance's version before is listed.</param>
        /// <returns><c>{ version, faceplates }</c>: the version moved to, and the instances, read back.</returns>
        /// <exception cref="PortalException">The type has no instance here or no such released version, the version would lose what an interface held (see <see cref="UnifiedFaceplateMoveRules"/>), or TIA Portal refused one or its reading back; every instance moved is moved back, with what its interface held.</exception>
        /// <remarks>
        /// Measured 2026-11-01: <c>UpdateProject</c> takes only a PLC or a classic HMI, not a Unified one, while writing a
        /// container's <c>ContainedType</c> moves it and keeps what its interface was given. Instances stay on the version
        /// they show until moved, so a release alone changes nothing on a screen.
        /// </remarks>
        public JsonObject UpdateUnifiedFaceplates(string hmiPath, string typeName, Version? version, string backupDirectory)
        {
            _logger?.LogInformation("Moving faceplates of {Type} on {HmiPath}...", typeName, hmiPath);

            try
            {
                var hmi = RequireUnifiedSoftware(hmiPath);
                var found = RequireLibraryType(typeName);
                var number = LibraryPublishRules.ReleasedVersion(found.Type.Name, LibraryTypeReader.Versions(found.Type), version);
                var instances = AllScreens(hmi).SelectMany(screen => UnifiedFaceplateReader.On(screen).Select(faceplate => (Screen: screen.Name, Faceplate: faceplate)))
                    .Where(entry => string.Equals(FaceplateTypeReference.Parse(entry.Faceplate.ContainedType)?.TypeName, found.Type.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (instances.Count == 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"'{hmiPath}' has no instance of '{found.Type.Name}'; PlaceUnifiedFaceplates places one");
                }

                SaveLibraryBackup(backupDirectory, "faceplate-versions.json", new JsonArray(instances.Select(entry => (JsonNode)UnifiedFaceplateReader.Describe(entry.Screen, entry.Faceplate)).ToArray()));

                return new JsonObject
                {
                    ["version"] = LibraryVersionNumber.Format(number),
                    ["faceplates"] = MoveFaceplates(instances, FaceplateTypeReference.Format(found.Type.Name, number))
                };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "UpdateUnifiedFaceplates"), "UpdateUnifiedFaceplates", hmiPath, string.Empty);
            }
        }

        /// <remarks>
        /// The reading back is inside the undo: a new version may have changed the interface, and an instance that
        /// cannot be read is not reported as moved.
        /// </remarks>
        private static JsonArray MoveFaceplates(IReadOnlyList<(string Screen, HmiFaceplateContainer Faceplate)> instances, string reference)
        {
            var moved = new List<(string Name, Action Delete)>();

            try
            {
                foreach (var (screen, faceplate) in instances)
                {
                    var before = ReadInterface(faceplate);
                    moved.Add(($"faceplate '{faceplate.Name}' on screen '{screen}', to be moved back to {faceplate.ContainedType}", MoveBack(faceplate, before)));
                    faceplate.ContainedType = reference;
                    UnifiedFaceplateMoveRules.CheckKept(faceplate.Name, reference, AsText(before), AsText(ReadInterface(faceplate)));
                }

                return new JsonArray(instances.Select(entry => (JsonNode)UnifiedFaceplateReader.Describe(entry.Screen, entry.Faceplate)).ToArray());
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                // Everything the request could get wrong was refused already, so what else fails here is TIA Portal or the environment.
                throw UnifiedRollback.UndoChanges(moved, failure as PortalException ?? new PortalException(PortalErrorCode.WriteFailed, $"TIA Portal failed moving the faceplates to {reference}: {failure.Message.Trim()}", null, failure), "moves");
            }
        }

        private static List<(string Name, object Value)> ReadInterface(HmiFaceplateContainer faceplate)
        {
            return faceplate.Interface.Select(property => (property.PropertyName, property.Value)).ToList();
        }

        private static List<(string Name, string Value)> AsText(IReadOnlyList<(string Name, object Value)> values)
        {
            return values.Select(entry => (entry.Name, Convert.ToString(entry.Value, CultureInfo.InvariantCulture) ?? string.Empty)).ToList();
        }

        /// <remarks>
        /// What the interface holds goes back too, as a version with another interface may have dropped it. Only a
        /// value that differs is written; a property the version before lacks throws, so the undo names the instance.
        /// </remarks>
        private static Action MoveBack(HmiFaceplateContainer faceplate, IReadOnlyList<(string Name, object Value)> values)
        {
            var type = faceplate.ContainedType;

            return () =>
            {
                faceplate.ContainedType = type;

                foreach (var (name, value) in values)
                {
                    var property = faceplate.Interface.Find(name) ?? throw new InvalidOperationException($"{type} has no interface property '{name}'");
                    if (!Equals(property.Value, value))
                    {
                        property.Value = value;
                    }
                }
            };
        }

        /// <summary>Places faceplate instances on a WinCC Unified screen, all of them or none.</summary>
        /// <param name="hmiPath">Full path to the Unified HMI software.</param>
        /// <param name="list">The screen and the faceplates.</param>
        /// <param name="backupDirectory">Where the screen's items before the change are listed.</param>
        /// <returns>The faceplates placed, read back.</returns>
        /// <exception cref="PortalException">
        /// The screen, a type, a released version, a tag or an interface property does not exist; a name is
        /// taken; or TIA Portal refused one. In every case nothing is left placed.
        /// </exception>
        public JsonObject PlaceUnifiedFaceplates(string hmiPath, UnifiedFaceplateList list, string backupDirectory)
        {
            _logger?.LogInformation("Placing Unified faceplates on {HmiPath}...", hmiPath);

            try
            {
                var requested = list ?? throw new PortalException(PortalErrorCode.InvalidParams, "A faceplate list is required");
                var hmi = RequireUnifiedSoftware(hmiPath);
                var screen = FindUnifiedScreen(hmi, hmiPath, requested.Screen);
                var writer = new UnifiedFaceplateWriter(hmi, screen, requested, RequireProjectLibrary());

                writer.Check();
                writer.Place(backupDirectory);

                var placed = requested.Faceplates.Select(faceplate => faceplate.Name).ToList();

                return new JsonObject
                {
                    ["faceplates"] = new JsonArray(UnifiedFaceplateReader.On(screen).Where(found => placed.Contains(found.Name)).Select(found => (JsonNode)UnifiedFaceplateReader.Describe(screen.Name, found)).ToArray())
                };
            }
            catch (Exception ex)
            {
                throw DecorateHmiFailure(AsHmiWriteFailure(ex, "PlaceUnifiedFaceplates"), "PlaceUnifiedFaceplates", hmiPath, string.Empty);
            }
        }
    }
}
