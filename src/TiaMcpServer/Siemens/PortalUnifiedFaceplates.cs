using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json.Nodes;
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
