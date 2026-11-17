using Siemens.Engineering.HmiUnified.UI.Base;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Enum;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.CustomWebControls;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Siemens
{
    /// <summary>Places Custom Web Controls on a WinCC Unified screen: all of them, or none.</summary>
    /// <remarks>
    /// Measured 2026-11-15 on TIA Portal V21: <c>ScreenItems.Create&lt;HmiCustomWebControlContainer&gt;(name, identityName)</c>
    /// places a control by its manifest name (its GUID is refused); it comes as a 640 x 320 window with a title bar, so the
    /// size is always written - the placement's, else the manifest's - and <c>WindowFlags</c> set to none. A fixed value is
    /// the interface property's <c>Value</c>; a tag is a <c>TagDynamization</c> on the interface property itself (on the
    /// container it is "not supported"); an event takes a script through <c>EventHandlers.Create(name)</c>, whose setter
    /// takes broken JavaScript without a word (see UnifiedEventWriter), so each is syntax-checked where it is written.
    /// Everything else is checked before the first is created, and every control placed is deleted again when a later one
    /// fails, a broken script included.
    /// </remarks>
    internal sealed class CustomWebControlWriter
    {
        private readonly HmiScreen _screen;
        private readonly CustomWebControlPlacementList _list;
        private readonly IReadOnlyDictionary<string, CustomWebControlSummary> _installed;
        private readonly Func<string, bool> _hmiHasTag;

        /// <summary>Creates the writer.</summary>
        /// <param name="screen">The screen to place on.</param>
        /// <param name="list">What to place.</param>
        /// <param name="installed">The project's installed controls, by name.</param>
        /// <param name="hmiHasTag">Whether the HMI has a tag of that name.</param>
        public CustomWebControlWriter(HmiScreen screen, CustomWebControlPlacementList list, IReadOnlyDictionary<string, CustomWebControlSummary> installed, Func<string, bool> hmiHasTag)
        {
            _screen = screen;
            _list = list;
            _installed = installed;
            _hmiHasTag = hmiHasTag;
        }

        /// <summary>Checks every placement before anything is created.</summary>
        /// <exception cref="PortalException">A control is not installed, a name is taken on the screen, or a property, value or event does not fit.</exception>
        public void Check()
        {
            foreach (var placement in _list.Placements)
            {
                if (_screen.ScreenItems.Find(placement.Name) != null)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"Screen '{_screen.Name}' already has an item '{placement.Name}'");
                }

                CustomWebControlPlacementRules.Check(placement, Control(placement));
                RequireTags(placement);
            }
        }

        /// <remarks>
        /// TIA Portal takes any tag name and only the compile refuses it (measured 2026-11-15: "The tag 'M1.KM' for
        /// dynamization of the property 'IsOn' does not exist"), so the tag is looked for first. Only its root: a member of
        /// a structured tag (<c>M1.KM</c>) is not a tag of its own to Openness, as for faceplates.
        /// </remarks>
        private void RequireTags(CustomWebControlPlacement placement)
        {
            foreach (var bound in placement.Interface.Tags)
            {
                var root = UnifiedFaceplateInterfaceValue.Tag(bound.Value.Trim()).RootTag()!;

                if (!_hmiHasTag(root))
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"'{placement.Name}.{bound.Key}' is bound to '{bound.Value}', and the HMI has no tag '{root}' (CreateUnifiedTags creates one)");
                }
            }
        }

        /// <summary>Places every control; deletes those placed when one fails.</summary>
        /// <param name="backupDirectory">Where the screen's items before the change are listed.</param>
        public void Place(string backupDirectory)
        {
            UnifiedScreenBackup.SaveItems(_screen, backupDirectory);
            var made = new List<(string Name, Action Delete)>();

            try
            {
                foreach (var placement in _list.Placements)
                {
                    UnifiedRollback.Create($"control '{placement.Name}' ({placement.Control})", made, () => _screen.ScreenItems.Create<HmiCustomWebControlContainer>(placement.Name, placement.Control), (created => created.Delete, created => Write(created, placement)));
                }
            }
            catch (Exception failure) when (!UnifiedRollback.IsSessionEnded(failure))
            {
                throw UnifiedRollback.Undo(made, failure, "custom web controls");
            }
        }

        private CustomWebControlSummary Control(CustomWebControlPlacement placement)
        {
            return _installed.TryGetValue(placement.Control, out var control)
                ? control
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{placement.Control}' is not installed in this project (InstallCustomWebControl installs it); installed: {(_installed.Count == 0 ? "none" : string.Join(", ", _installed.Keys))}");
        }

        private void Write(HmiCustomWebControlContainer created, CustomWebControlPlacement placement)
        {
            var control = Control(placement);

            created.Left = placement.Frame.Left;
            created.Top = placement.Frame.Top;
            created.Width = placement.Frame.Width ?? (uint)Math.Max(control.DefaultWidth, CustomWebControlRules.MinimumSize);
            created.Height = placement.Frame.Height ?? (uint)Math.Max(control.DefaultHeight, CustomWebControlRules.MinimumSize);
            created.WindowFlags = HmiWindowFlag.None;

            foreach (var name in placement.Interface.Values.Keys)
            {
                Loaded(created.Interface.Find(name), placement, name).Value = CustomWebControlPlacementRules.ValueOf(placement, control, name);
            }

            foreach (var tag in placement.Interface.Tags)
            {
                Loaded(created.Interface.Find(tag.Key), placement, tag.Key).Dynamizations.Create<TagDynamization>(tag.Key).Tag = tag.Value.Trim();
            }

            foreach (var script in placement.Interface.Events)
            {
                var code = created.EventHandlers.Create(script.Key).Script;
                code.ScriptCode = script.Value;

                var errors = code.SyntaxCheck()?.Errors?.ToList() ?? new List<string>();
                if (errors.Count > 0)
                {
                    throw new PortalException(PortalErrorCode.InvalidParams, $"The '{script.Key}' script of '{placement.Name}' is not valid JavaScript: {string.Join("; ", errors)}. Nothing was placed.");
                }
            }
        }

        /// <remarks>
        /// The placement is checked against the manifest on disk; TIA Portal places from the one it loaded when the project
        /// was opened. A package installed or replaced since, in a TIA Portal with a window, leaves the two apart (review of
        /// 2026-11-16), which would otherwise surface as a null reference.
        /// </remarks>
        private static T Loaded<T>(T? found, CustomWebControlPlacement placement, string name)
            where T : class
        {
            return found
                ?? throw new PortalException(PortalErrorCode.InvalidState, $"TIA Portal has not loaded '{placement.Control}' with a property '{name}', though its package declares it: the package is newer than the open project. Close the project and open it again, then place it. Nothing was placed.");
        }
    }
}
