using Siemens.Engineering.HmiUnified.UI.Controls;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Library;
using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.Siemens
{
    /// <summary>Reads the faceplate instances on a WinCC Unified HMI's screens, each interface value as the text it holds.</summary>
    /// <remarks>
    /// Not the shape PlaceUnifiedFaceplates takes: an interface property holds a tag's name and a fixed text
    /// alike, as a string, and says nothing of which it is (measured 2026-10-30), so a value cannot be read
    /// back as <c>{"tag": ...}</c>; and each faceplate names its screen, which the list gives once.
    /// </remarks>
    internal static class UnifiedFaceplateReader
    {
        /// <summary>Every faceplate on the screens given.</summary>
        /// <param name="screens">The screens, screen groups included, as the portal walks them.</param>
        /// <returns>One object per faceplate.</returns>
        public static JsonArray Read(IEnumerable<HmiScreen> screens)
        {
            return new JsonArray(screens.SelectMany(screen => On(screen).Select(faceplate => (JsonNode)Describe(screen.Name, faceplate))).ToArray());
        }

        /// <summary>The faceplates on one screen.</summary>
        /// <param name="screen">The screen.</param>
        /// <returns>Its faceplate containers.</returns>
        public static IEnumerable<HmiFaceplateContainer> On(HmiScreen screen)
        {
            return screen.ScreenItems.OfType<HmiFaceplateContainer>();
        }

        /// <summary>Describes one faceplate.</summary>
        /// <param name="screen">The screen it is on.</param>
        /// <param name="faceplate">The container.</param>
        /// <returns><c>{ screen, name, type, version, left, top, width, height, interface }</c>.</returns>
        public static JsonObject Describe(string screen, HmiFaceplateContainer faceplate)
        {
            var reference = FaceplateTypeReference.Parse(faceplate.ContainedType);
            var values = new JsonObject();

            foreach (var property in faceplate.Interface)
            {
                values[property.PropertyName] = UnifiedFaceplateInterfaceText.Read(Convert.ToString(property.Value, CultureInfo.InvariantCulture));
            }

            return new JsonObject
            {
                ["screen"] = screen,
                ["name"] = faceplate.Name,
                ["type"] = reference?.TypeName ?? faceplate.ContainedType,
                ["version"] = reference == null ? null : LibraryVersionNumber.Format(reference.Value.Version),
                ["left"] = faceplate.Left,
                ["top"] = faceplate.Top,
                ["width"] = faceplate.Width,
                ["height"] = faceplate.Height,
                ["interface"] = values
            };
        }
    }
}
