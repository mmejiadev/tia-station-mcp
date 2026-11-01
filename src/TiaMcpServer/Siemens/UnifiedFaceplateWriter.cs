using Siemens.Engineering.HmiUnified.UI.Controls;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.Library.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedFaceplates;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Places faceplate instances on a WinCC Unified screen, all of a list or none of it.</summary>
    /// <remarks>
    /// Measured 2026-10-30 on TIA Portal V20: an instance is created with its type reference (see
    /// <see cref="FaceplateTypeReference"/>) at the size its type was drawn at, and each interface property
    /// is given its value as a string, a tag's name or a text. Nothing on the screen is changed or removed:
    /// the instances are only added, and an undo deletes exactly those.
    /// </remarks>
    internal sealed class UnifiedFaceplateWriter
    {
        private const string ScreenItemsFile = "screen-items.json";

        private readonly HmiScreen _screen;
        private readonly UnifiedFaceplateList _list;
        private readonly UnifiedFaceplateRules _rules;

        /// <summary>Creates a writer, reading what the list is checked against once.</summary>
        /// <param name="hmi">The WinCC Unified software.</param>
        /// <param name="screen">The screen to place on.</param>
        /// <param name="list">The faceplates.</param>
        /// <param name="projectLibrary">The project library's type folder.</param>
        public UnifiedFaceplateWriter(UnifiedHmiSoftware hmi, HmiScreen screen, UnifiedFaceplateList list, LibraryTypeFolder projectLibrary)
        {
            _screen = screen;
            _list = list;

            var released = LibraryTypeReader.ByName(projectLibrary).ToDictionary(
                entry => entry.Value.Type.Name,
                entry => (IReadOnlyList<Version>)entry.Value.Type.Versions.Where(LibraryTypeReader.IsReleased).Select(version => version.VersionNumber).ToList(),
                StringComparer.OrdinalIgnoreCase);
            var state = new UnifiedFaceplateState(screen.ScreenItems.Select(item => item.Name).ToList(), released, hmi.Tags.Select(tag => tag.Name).ToList());

            _rules = new UnifiedFaceplateRules(state);
        }

        /// <summary>Refuses a list that cannot be placed as it stands, before anything is changed.</summary>
        public void Check()
        {
            _rules.Check(_list);
        }

        /// <summary>Places every faceplate; removes those placed when one fails.</summary>
        /// <param name="backupDirectory">Where the screen's items before the change are listed.</param>
        public void Place(string backupDirectory)
        {
            SaveScreenItems(backupDirectory);
            var made = new List<(string Name, Action Delete)>();

            try
            {
                foreach (var faceplate in _list.Faceplates)
                {
                    var reference = FaceplateTypeReference.Format(_rules.TypeNameOf(faceplate), _rules.VersionOf(faceplate));
                    UnifiedRollback.Create($"faceplate '{faceplate.Name}' ({reference})", made, () => _screen.ScreenItems.Create<HmiFaceplateContainer>(faceplate.Name, reference), (created => created.Delete, created => Write(created, faceplate)));
                }
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                throw UnifiedRollback.Undo(made, failure, "faceplates");
            }
        }

        private void SaveScreenItems(string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: the screen's items are listed before any is added");
            }

            var items = new JsonObject
            {
                ["screen"] = _screen.Name,
                ["items"] = new JsonArray(_screen.ScreenItems.Select(item => (JsonNode)item.Name).ToArray())
            };

            Directory.CreateDirectory(backupDirectory);
            File.WriteAllText(Path.Combine(backupDirectory, ScreenItemsFile), items.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private void Write(HmiFaceplateContainer created, UnifiedFaceplateDescription faceplate)
        {
            created.Left = faceplate.Frame.Left;
            created.Top = faceplate.Frame.Top;
            created.Width = faceplate.Frame.Width ?? created.Width;
            created.Height = faceplate.Frame.Height ?? created.Height;

            foreach (var entry in faceplate.Interface)
            {
                var property = created.Interface.Find(entry.Key)
                    ?? throw new PortalException(
                        PortalErrorCode.InvalidParams,
                        $"Faceplate '{faceplate.Name}' ({faceplate.TypeName}) has no interface property '{entry.Key}'; it has: {string.Join(", ", created.Interface.Select(found => found.PropertyName))}");

                property.Value = _rules.ValueOf(entry.Value);
            }
        }
    }
}
