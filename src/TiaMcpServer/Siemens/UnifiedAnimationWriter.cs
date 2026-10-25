using Siemens.Engineering;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Flashing;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Script;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Tag;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.Siemens
{
    /// <summary>Writes and checks the animations of a WinCC Unified screen item: range tables, flashing, scripts and text lists.</summary>
    /// <remarks>
    /// Measured 2026-10-25 on TIA Portal V20. A range table is a tag dynamization whose
    /// <c>ValueConverter.MappingTable</c> is set to <c>Range</c> and given one <c>MappingTableEntryRange</c>
    /// per range; each entry's value is of the animated property's own type (a <c>Color</c>, a
    /// <c>Boolean</c>, a <c>UInt32</c>…). Two things are never done here, because each ended the TIA Portal
    /// session in the probe: creating a <c>MappingTableEntrySimple</c>, in a range table or an expression
    /// one, and reading <c>GetAttributeInfos()</c>. Which properties take a dynamization is only known to
    /// TIA Portal (<c>Text</c> and <c>Alignment</c> refuse one), so every animation is rehearsed on a
    /// throw-away screen before anything is drawn.
    /// </remarks>
    internal static class UnifiedAnimationWriter
    {
        private const string RehearsalItemPrefix = "Rehearsal_";

        /// <summary>Refuses an animation the item's type cannot take as described, before anything is changed.</summary>
        /// <param name="itemType">The item's Openness type.</param>
        /// <param name="animation">The animation.</param>
        /// <param name="itemName">The item, as a refusal names it.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming what is wrong.</exception>
        public static void Check(Type itemType, UnifiedAnimation animation, string itemName)
        {
            var property = UnifiedScreenItemWriter.RequireLinkable(itemType, animation.Path);
            var what = $"'{itemName}' {animation.Path}";

            switch (animation)
            {
                case UnifiedRangeAnimation ranges:
                    CheckRanges(property, ranges, what);
                    break;
                case UnifiedFlashingAnimation flashing:
                    RequireColour(property, what, "flashing");
                    UnifiedPropertyConverter.Convert(typeof(Color), flashing.Color, $"{what} flashing colour");
                    UnifiedPropertyConverter.Convert(typeof(Color), flashing.Alternate, $"{what} alternate colour");
                    break;
                case UnifiedScriptAnimation _:
                case UnifiedListAnimation _:
                    break;
                default:
                    throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised animation: {animation.GetType().Name}");
            }
        }

        /// <summary>Draws every animation of the description on a throw-away screen, and names each one TIA Portal refuses.</summary>
        /// <param name="scratch">A screen made for the purpose, deleted by the caller.</param>
        /// <param name="items">The items about to be drawn.</param>
        /// <returns>One reason per refused animation; empty when TIA Portal takes them all.</returns>
        public static IReadOnlyList<string> Rehearse(HmiScreen scratch, IReadOnlyList<UnifiedItemDescription> items)
        {
            var refused = new List<string>();
            var animated = items.Where(item => item.Animations.Count > 0).ToList();

            for (var index = 0; index < animated.Count; index++)
            {
                var stand = UnifiedScreenItemWriter.Create(scratch, animated[index].TypeName, RehearsalItemPrefix + index);
                refused.AddRange(animated[index].Animations.Select(animation => Refusal(stand, animation, animated[index].Name)).OfType<string>());
            }

            return refused;
        }

        /// <summary>Writes an animation onto a new item.</summary>
        /// <param name="item">The item, just created.</param>
        /// <param name="animation">The animation, already passed through <see cref="Check"/>.</param>
        /// <param name="itemName">The item, as a refusal names it.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when TIA Portal finds a script broken.</exception>
        public static void Write(object item, UnifiedAnimation animation, string itemName)
        {
            var owner = UnifiedPropertyWriter.ResolveOwner(item, animation.Segments);
            var property = UnifiedPropertyWriter.ResolveOnType(owner.GetType(), new[] { animation.Segments[animation.Segments.Count - 1] }, animation.Path);
            var dynamizations = (DynamizationBaseComposition)owner.GetType().GetProperty(UnifiedScreenItemWriter.DynamizationsProperty)!.GetValue(owner);

            // The property's own spelling: the description's is matched case aside, Openness's name is exact.
            var name = property.Name;
            var what = $"'{itemName}' {animation.Path}";

            switch (animation)
            {
                case UnifiedRangeAnimation ranges:
                    WriteRanges(dynamizations.Create<TagDynamization>(name), property.PropertyType, ranges, what);
                    break;
                case UnifiedFlashingAnimation flashing:
                    WriteFlashing(dynamizations.Create<FlashingDynamization>(name), flashing, what);
                    break;
                case UnifiedScriptAnimation script:
                    WriteScript(dynamizations.Create<ScriptDynamization>(name), script, what);
                    break;
                case UnifiedListAnimation list:
                    var dynamization = dynamizations.Create<ResourceListDynamization>(name);
                    dynamization.Tag = list.Tag;
                    dynamization.ResourceList = list.List;
                    break;
                default:
                    throw new PortalException(PortalErrorCode.InvalidState, $"Unrecognised animation: {animation.GetType().Name}");
            }
        }

        /// <remarks>
        /// A refusal by TIA Portal arrives as an <see cref="EngineeringTargetInvocationException"/> ("Dynamization
        /// type not supported", "Creation of Tag dynamization entries is not allowed for this property"), and
        /// is what the rehearsal is for; an ended session is not caught and stops the draw.
        /// </remarks>
        private static string? Refusal(object stand, UnifiedAnimation animation, string itemName)
        {
            try
            {
                Write(stand, animation, itemName);
                return null;
            }
            catch (PortalException refused) when (refused.Code == PortalErrorCode.InvalidParams)
            {
                return refused.Message;
            }
            catch (EngineeringTargetInvocationException refused)
            {
                return $"TIA Portal refuses to animate '{itemName}' {animation.Path}: {refused.Message.Trim()}";
            }
        }

        private static void CheckRanges(PropertyInfo property, UnifiedRangeAnimation animation, string what)
        {
            if (property.PropertyType == typeof(MultilingualText) || !UnifiedPropertyConverter.Supports(property.PropertyType))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} is a {property.PropertyType.Name}, which takes no value ranges; a text is changed through a script");
            }

            foreach (var range in animation.Ranges)
            {
                var where = $"{what}, range {range.From}..{range.To}";
                UnifiedPropertyConverter.Convert(property.PropertyType, range.Value, where);

                if (range.Flashing != null)
                {
                    RequireColour(property, where, "flashing");
                    UnifiedPropertyConverter.Convert(typeof(Color), range.Flashing.Alternate, $"{where} alternate colour");
                }
            }
        }

        private static void RequireColour(PropertyInfo property, string what, string feature)
        {
            if (property.PropertyType != typeof(Color))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"{what} is a {property.PropertyType.Name}: only a colour takes {feature} (TIA Portal refuses it elsewhere)");
            }
        }

        private static void WriteRanges(TagDynamization link, Type valueType, UnifiedRangeAnimation animation, string what)
        {
            link.Tag = animation.Tag;
            var table = link.ValueConverter.MappingTable;
            table.ConditionType = ConditionType.Range;

            foreach (var range in animation.Ranges)
            {
                var entry = table.Entries.Create<MappingTableEntryRange>();
                entry.From = range.From;
                entry.To = range.To;
                entry.Value = UnifiedPropertyConverter.Convert(valueType, range.Value, what);

                if (range.Flashing != null)
                {
                    entry.Flashing = true;
                    entry.FlashingRate = Named<FlashingRate>(range.Flashing.Rate);
                    entry.AlternateValue = UnifiedPropertyConverter.Convert(typeof(Color), range.Flashing.Alternate, what);
                }
            }
        }

        private static void WriteFlashing(FlashingDynamization dynamization, UnifiedFlashingAnimation flashing, string what)
        {
            dynamization.Color = (Color)UnifiedPropertyConverter.Convert(typeof(Color), flashing.Color, what);
            dynamization.AlternateColor = (Color)UnifiedPropertyConverter.Convert(typeof(Color), flashing.Alternate, what);
            dynamization.FlashingRate = Named<FlashingRate>(flashing.Rate);
            dynamization.FlashingCondition = Named<FlashingCondition>(flashing.Condition);
        }

        /// <remarks>Like an event script, the code's setter takes broken JavaScript; only <c>SyntaxCheck()</c> reports it.</remarks>
        private static void WriteScript(ScriptDynamization dynamization, UnifiedScriptAnimation script, string what)
        {
            dynamization.ScriptCode = script.Script;
            if (script.GlobalDefinitions != null)
            {
                dynamization.GlobalDefinitionAreaScriptCode = script.GlobalDefinitions;
            }

            dynamization.Trigger.Type = script.Trigger == UnifiedScriptAnimation.AutomaticTrigger ? TriggerType.AutomaticTags : Named<TriggerType>(script.Trigger);
            if (script.TriggerTags.Count > 0)
            {
                dynamization.Trigger.Tags = script.TriggerTags.ToArray();
            }

            var errors = dynamization.SyntaxCheck()?.Errors?.ToList() ?? new List<string>();
            if (errors.Count > 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"the script animating {what} is not valid JavaScript: {string.Join("; ", errors)}");
            }
        }

        /// <remarks>The names come from the description reader, which spells them as Openness V20 does; another version may not.</remarks>
        private static TEnum Named<TEnum>(string name)
            where TEnum : struct
        {
            return Enum.GetNames(typeof(TEnum)).Contains(name, StringComparer.Ordinal)
                ? (TEnum)Enum.Parse(typeof(TEnum), name)
                : throw new PortalException(PortalErrorCode.InvalidState, $"This TIA Portal's Openness has no {typeof(TEnum).Name} '{name}'");
        }
    }
}
