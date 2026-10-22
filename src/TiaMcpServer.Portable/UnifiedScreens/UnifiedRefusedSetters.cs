using System.Collections.Generic;

namespace TiaMcpServer.UnifiedScreens
{
    /// <summary>Properties Openness V20 gives a setter that TIA Portal then refuses.</summary>
    /// <remarks>
    /// The setter is there in <c>Siemens.Engineering.dll</c>, so reflection alone calls them writable.
    /// Each entry was refused by TIA Portal, with the message kept here, when every item type read back
    /// was drawn again on 2026-10-21 (<c>Test44UnifiedScreens</c>). The same property on another type may
    /// well be settable (a button's <c>HotKey</c> was), so an entry is never widened to every type that
    /// has the name.
    /// </remarks>
    public static class UnifiedRefusedSetters
    {
        private static readonly Dictionary<(string OwnerType, string Property), string> Measured = new Dictionary<(string, string), string>
        {
            [("HmiToggleSwitch", "HotKey")] = "Set access is not available",
            [("HmiTouchArea", "ShowFocusVisual")] = "Property not supported"
        };

        /// <summary>Why a property cannot be set on a type although it has a setter, or null when it can.</summary>
        /// <param name="ownerType">The Openness type the property is read on, e.g. <c>HmiToggleSwitch</c>.</param>
        /// <param name="property">The property's name.</param>
        /// <returns>The reason, or null.</returns>
        public static string? ReasonFor(string ownerType, string property)
        {
            return Measured.TryGetValue((ownerType, property), out var message)
                ? $"TIA Portal refuses to set it (\"{message}\"), though Openness has a setter"
                : null;
        }
    }
}
