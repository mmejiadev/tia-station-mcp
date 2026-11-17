using System;
using System.Linq;
using TiaMcpServer.Siemens;
using TiaMcpServer.UnifiedScreens;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>Checks a placement against the control's manifest, and turns its fixed values into what Openness takes.</summary>
    /// <remarks>
    /// Everything is checked before the first control is created, so a list with one wrong name places nothing. A fixed
    /// value is converted by the property's kind as the manifest declares it - a colour from '0, 200, 83' to a
    /// <c>System.Drawing.Color</c>, as measured on 2026-11-15 - with the conversion screens use, so both accept the same
    /// spellings. A property of a kind this server does not set can still be bound to a tag.
    /// </remarks>
    public static class CustomWebControlPlacementRules
    {
        /// <summary>Refuses a placement that names what the control does not have.</summary>
        /// <param name="placement">The placement.</param>
        /// <param name="control">What the installed control's manifest says.</param>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> naming the property or event, and what the control has.</exception>
        public static void Check(CustomWebControlPlacement placement, CustomWebControlSummary control)
        {
            RequireBoth(placement, control);

            foreach (var name in placement.Interface.Values.Keys.Concat(placement.Interface.Tags.Keys))
            {
                RequireProperty(placement, control, name);
            }

            foreach (var fixedValue in placement.Interface.Values)
            {
                ValueOf(placement, control, fixedValue.Key);
            }

            var unknownEvent = placement.Interface.Events.Keys.FirstOrDefault(name => !control.Events.Contains(name, StringComparer.Ordinal));
            if (unknownEvent != null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{placement.Control}' fires no event '{unknownEvent}'; it fires: {Listed(control.Events)}");
            }
        }

        /// <summary>The value Openness takes for one fixed property.</summary>
        /// <param name="placement">The placement.</param>
        /// <param name="control">What the installed control's manifest says.</param>
        /// <param name="property">The property, one of the placement's fixed values.</param>
        /// <returns>A bool, string, double, int or <c>System.Drawing.Color</c>.</returns>
        /// <exception cref="PortalException"><see cref="PortalErrorCode.InvalidParams"/> when the value does not fit, or the kind is not one this server sets.</exception>
        public static object ValueOf(CustomWebControlPlacement placement, CustomWebControlSummary control, string property)
        {
            RequireBoth(placement, control);

            var kind = RequireProperty(placement, control, property)
                ?? throw new PortalException(PortalErrorCode.InvalidParams, $"'{placement.Control}.{property}' is of a kind this server does not set; bind it to a tag instead");

            return UnifiedPropertyConverter.Convert(CustomWebControlPropertyKinds.ClrType(kind), placement.Interface.Values[property], $"'{placement.Name}.{property}'");
        }

        private static CustomWebControlPropertyKind? RequireProperty(CustomWebControlPlacement placement, CustomWebControlSummary control, string name)
        {
            var found = control.Properties.Where(property => string.Equals(property.Name, name, StringComparison.Ordinal)).ToList();

            return found.Count == 1
                ? found[0].Kind
                : throw new PortalException(PortalErrorCode.InvalidParams, $"'{placement.Control}' has no property '{name}'; it has: {Listed(control.Properties.Select(property => property.Name))}");
        }

        private static void RequireBoth(CustomWebControlPlacement placement, CustomWebControlSummary control)
        {
            if (placement == null)
            {
                throw new ArgumentNullException(nameof(placement));
            }

            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }
        }

        private static string Listed(System.Collections.Generic.IEnumerable<string> names)
        {
            var list = names.ToList();

            return list.Count == 0 ? "none" : string.Join(", ", list);
        }
    }
}
