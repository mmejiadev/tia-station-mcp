using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.UI.Base;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiaMcpServer.UnifiedScreens;
using UnifiedHmiSoftware = Siemens.Engineering.HmiUnified.HmiSoftware;

namespace TiaMcpServer.Siemens
{
    /// <summary>Changes some items of a WinCC Unified screen and leaves the rest as it is: all of the change, or none.</summary>
    /// <remarks>
    /// <c>DrawUnifiedScreen</c> deletes every item and draws the description, so a screen holding what it cannot draw
    /// back - a faceplate, a Custom Web Control - could not be changed by it at all, and adding one button meant
    /// describing the whole screen. An edit touches only the items it names, and backs up each as its undo needs: an item
    /// removed or replaced whole, checked to draw back; a property set in place - of any item, a faceplate's too - by its
    /// value before (<see cref="UnifiedScreenEditValues"/>). Each step done puts its own undo on a list
    /// (<see cref="UnifiedRollback"/>): when TIA Portal refuses a later step, only what was done is undone.
    /// </remarks>
    internal sealed class UnifiedScreenEditor
    {
        private const string BackupFileName = "screen-edit-before.json";

        private readonly HmiScreen _screen;
        private readonly UnifiedScreenEdit _edit;
        private readonly Language _language;
        private readonly UnifiedPropertyWriter _properties;
        private readonly UnifiedScreenItemWriter _items;
        private readonly UnifiedScreenEditValues _values = new UnifiedScreenEditValues();

        /// <summary>The items to draw, given the screen's global definitions once <see cref="Check"/> has read them.</summary>
        /// <remarks>Mutable as it needs TIA Portal read, which the constructor does not do.</remarks>
        private IReadOnlyList<UnifiedItemDescription> _drawn;

        /// <summary>Creates the editor of one screen.</summary>
        /// <param name="screen">The screen, outside any screen group.</param>
        /// <param name="edit">The change, its drawing already through the checks a drawing goes through.</param>
        /// <param name="language">The project's editing language.</param>
        public UnifiedScreenEditor(HmiScreen screen, UnifiedScreenEdit edit, Language language)
        {
            _screen = screen;
            _edit = edit;
            _language = language;
            _properties = new UnifiedPropertyWriter(language);
            _items = new UnifiedScreenItemWriter(_properties);
            _drawn = edit.Draw;
        }

        /// <summary>Checks what a drawing's checks do not cover: the items named, and the properties set in place.</summary>
        /// <param name="software">The WinCC Unified software, for its text lists.</param>
        /// <exception cref="PortalException">An item is missing, or a property set does not fit, is linked or animated, or names a missing text list.</exception>
        public void Check(UnifiedHmiSoftware software)
        {
            RequireItemsExist(_edit.Touched);
            _edit.Properties.ToList().ForEach(setting => RequireStatic(_screen, setting, "the screen's"));

            foreach (var item in _edit.Set)
            {
                var target = Find(item.Name)!;
                foreach (var setting in item.Properties)
                {
                    UnifiedPropertyWriter.Check(target.GetType(), setting);
                    RequireStatic(target, setting, $"'{item.Name}'");
                }
            }

            Portal.RequireTextListsExist(software, _edit.SetTextLists);
            _drawn = UnifiedScreenGlobals.Shared(_edit.Draw, UnifiedExistingGlobals.Of(_screen));
        }

        /// <summary>Writes the backup, refusing an edit whose backup would not put back what it changes.</summary>
        /// <param name="backupDirectory">The change's backup folder.</param>
        /// <returns>The backup file, and the items to draw back from it.</returns>
        public (string File, UnifiedScreenDescription Redrawn) BackUp(string backupDirectory)
        {
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "backupDirectory is required: the items an edit touches are read into it first");
            }

            var redrawn = _edit.Remove.Concat(_edit.Draw.Select(item => item.Name)).Select(Find).OfType<HmiScreenItemBase>().ToList();
            var read = new UnifiedScreenReader(_language).ReadSome(_screen, redrawn, withScreenProperties: false);
            UnifiedBackupCheck.RequireItemsRestorable(_screen, redrawn, read);

            read["values"] = ReadValues();

            Directory.CreateDirectory(backupDirectory);
            var file = Path.Combine(backupDirectory, BackupFileName);
            File.WriteAllText(file, read.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            read.Remove("values");

            return (file, UnifiedScreenDescriptionReader.Parse(read.ToJsonString()));
        }

        /// <summary>Rehearses the drawn items' scripts and animations on a throw-away screen.</summary>
        /// <param name="software">The WinCC Unified software.</param>
        /// <remarks>After the backup's checks, which are cheaper and may refuse the edit first.</remarks>
        public void Rehearse(UnifiedHmiSoftware software)
        {
            UnifiedRehearsal.Run(software, _screen.Name, _drawn);
        }

        /// <summary>Applies the edit; undoes what was done of it when TIA Portal refuses a step.</summary>
        /// <param name="redrawn">The items removed or replaced, as the backup read them.</param>
        public void Apply(UnifiedScreenDescription redrawn)
        {
            var done = new List<(string Name, Action Undo)>();

            try
            {
                SetScreen(done);
                Remove(redrawn, done);
                SetItems(done);
                Draw(redrawn, done);
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                throw Undone(UnifiedRollback.UndoChanges(done, failure, "changes to the screen"), failure);
            }
        }

        private void SetScreen(List<(string Name, Action Undo)> done)
        {
            foreach (var setting in _edit.Properties)
            {
                var old = _values.Old(null, setting);
                SetNamed(_screen, setting, "the screen's");
                done.Add(($"the screen's {setting.Path}", () => _properties.Apply(_screen, old)));
            }
        }

        private void Remove(UnifiedScreenDescription redrawn, List<(string Name, Action Undo)> done)
        {
            foreach (var name in _edit.Remove)
            {
                Find(name)!.Delete();
                done.Add(($"'{name}'", () => DrawBack(redrawn, name)));
            }
        }

        private void SetItems(List<(string Name, Action Undo)> done)
        {
            foreach (var item in _edit.Set)
            {
                var target = Find(item.Name)!;

                foreach (var setting in item.Properties)
                {
                    var old = _values.Old(item.Name, setting);
                    SetNamed(target, setting, $"'{item.Name}'");
                    done.Add(($"'{item.Name}' {setting.Path}", () => _properties.Apply(target, old)));
                }
            }
        }

        /// <remarks>
        /// The undo goes on the list before the item is touched: a draw refused half-way leaves an item to delete. The
        /// screen's global definitions are written once at the end, imports included (see UnifiedEventWriter.Errors), and
        /// put back as they were on an undo.
        /// </remarks>
        private void Draw(UnifiedScreenDescription redrawn, List<(string Name, Action Undo)> done)
        {
            var scripted = UnifiedScreenGlobals.HoldsEvents(_drawn);
            if (scripted)
            {
                var before = UnifiedEventWriter.ScreenGlobals(_screen, withItems: true);
                done.Add(("the screen's global definitions", () => UnifiedEventWriter.WriteScreenGlobals(_screen, before)));
            }

            DrawEach(redrawn, done);

            if (scripted)
            {
                UnifiedEventWriter.WriteScreenGlobals(_screen, UnifiedScreenGlobals.EventDefinitions(_drawn));
            }
        }

        private void DrawEach(UnifiedScreenDescription redrawn, List<(string Name, Action Undo)> done)
        {
            foreach (var item in _drawn)
            {
                var existed = Find(item.Name) != null;
                done.Add(($"'{item.Name}'", existed ? () => DrawBack(redrawn, item.Name) : () => Find(item.Name)?.Delete()));

                Find(item.Name)?.Delete();
                Named(() => _items.Draw(_screen, item), $"item '{item.Name}'");
            }
        }

        private void DrawBack(UnifiedScreenDescription redrawn, string name)
        {
            Find(name)?.Delete();
            _items.Draw(_screen, redrawn.Items.First(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)));
        }

        private void SetNamed(object target, UnifiedPropertySetting setting, string owner)
        {
            Named(() => _properties.Apply(target, setting), $"{owner} {setting.Path}");
        }

        /// <remarks>TIA Portal's refusal names neither the item nor the property, so they are named here, as a request to fix.</remarks>
        private static void Named(Action step, string what)
        {
            try
            {
                step();
            }
            catch (Exception refused) when (!(refused is PortalException) && !UnifiedRollback.IsSessionEnded(refused))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"TIA Portal refused {what}: {refused.Message.Trim()}", null, refused);
            }
        }

        /// <remarks>Review of 2026-11-17: what was drawn back now lies above the others, which "nothing was changed" alone would hide.</remarks>
        private PortalException Undone(PortalException undone, Exception failure)
        {
            return _edit.Remove.Count == 0 && _edit.Draw.Count == 0
                ? undone
                : new PortalException(undone.Code, $"{undone.Message} Items deleted or replaced were drawn back on top of the others.", null, failure);
        }

        /// <remarks>One reader per owner, so a property it could not read is known to be that owner's.</remarks>
        private JsonObject ReadValues()
        {
            var values = new JsonObject();

            if (_edit.Properties.Count > 0)
            {
                var reader = new UnifiedScreenReader(_language);
                var screen = reader.ReadSome(_screen, Array.Empty<HmiScreenItemBase>(), withScreenProperties: true);
                _values.Read(null, (screen["properties"] as JsonObject, reader.Unreadable), _edit.Properties, _screen);
                values["screen"] = screen;
            }

            foreach (var item in _edit.Set)
            {
                var target = Find(item.Name)!;
                var reader = new UnifiedScreenReader(_language);
                var read = reader.ReadSome(_screen, new[] { target }, withScreenProperties: false);
                _values.Read(item.Name, (read["items"]![0]!["properties"] as JsonObject, reader.Unreadable), item.Properties, target);
                values[item.Name] = read;
            }

            return values;
        }

        /// <remarks>
        /// A property driven by a tag or an animation shows that, not a value set on it: the set would change nothing in
        /// runtime and say nothing (review of 2026-11-17).
        /// </remarks>
        private static void RequireStatic(object target, UnifiedPropertySetting setting, string owner)
        {
            var holder = UnifiedPropertyWriter.ResolveOwner(target, setting.Segments);
            var property = setting.Segments[setting.Segments.Count - 1];

            if (holder.GetType().GetProperty(UnifiedScreenItemWriter.DynamizationsProperty)?.GetValue(holder) is DynamizationBaseComposition dynamizations
                && dynamizations.Any(dynamization => string.Equals(dynamization.PropertyName, property, StringComparison.OrdinalIgnoreCase)))
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Nothing was changed: {owner} {setting.Path} is linked to a tag or animated, so a value set on it would not show; draw the item again in 'draw' without that link");
            }
        }

        private void RequireItemsExist(IEnumerable<string> names)
        {
            var missing = names.Where(name => Find(name) == null).ToList();

            if (missing.Count > 0)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"Nothing was changed: '{_screen.Name}' has no item {string.Join(", ", missing.Select(name => $"'{name}'"))}. Items: {string.Join(", ", _screen.ScreenItems.Select(item => item.Name))}");
            }
        }

        /// <remarks>Case aside, as every name in an edit is compared (review of 2026-11-17): Openness's own <c>Find</c> is not documented either way.</remarks>
        private HmiScreenItemBase? Find(string name)
        {
            return _screen.ScreenItems.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
