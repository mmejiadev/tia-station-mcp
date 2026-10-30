using System;
using System.Linq;
using TiaMcpServer.Library;

namespace TiaMcpServer.UnifiedFaceplates
{
    /// <summary>The change plan's line for a faceplate list: every instance, its type and what its interface is given.</summary>
    public static class UnifiedFaceplateSummary
    {
        /// <summary>Describes a list.</summary>
        /// <param name="list">The list.</param>
        /// <returns>E.g. <c>on Principal: FP_M1 (FACEPLATE_MOTOR_V1 0.0.1; Motor = tag M1, Nom = "Motor 1")</c>.</returns>
        public static string Of(UnifiedFaceplateList list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            return $"on {list.Screen}: " + string.Join("; ", list.Faceplates.Select(Describe));
        }

        private static string Describe(UnifiedFaceplateDescription faceplate)
        {
            var version = faceplate.Version == null ? "newest released" : LibraryVersionNumber.Format(faceplate.Version);
            var values = faceplate.Interface.Select(entry => entry.Value.IsTag ? $"{entry.Key} = tag {entry.Value.Value}" : $"{entry.Key} = \"{entry.Value.Value}\"");
            var interfaceText = faceplate.Interface.Count == 0 ? string.Empty : "; " + string.Join(", ", values);

            return $"{faceplate.Name} ({faceplate.TypeName} {version}{interfaceText})";
        }
    }
}
