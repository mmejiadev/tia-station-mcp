using Siemens.Engineering.SW.Types;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Turns Openness types into descriptions the rest of the server can hold on to.
    /// </summary>
    /// <remarks>
    /// The same job <see cref="BlockDescriber"/> does, for the other half of a PLC program. Kept as
    /// its own class rather than folded in beside the blocks: they share not one property, and a
    /// single "describer" of two unrelated shapes is a class named after a verb instead of a
    /// responsibility.
    /// </remarks>
    public static class TypeDescriber
    {
        /// <summary>Describes one user-defined type.</summary>
        /// <param name="type">The type to read.</param>
        /// <param name="path">Its full path in the project, which only the portal can work out.</param>
        /// <returns>The description.</returns>
        /// <exception cref="PortalException">There is no type to describe.</exception>
        public static TypeDescription Describe(PlcType type, string path)
        {
            if (type == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "There is no type to describe");
            }

            return new TypeDescription
            {
                Name = type.Name,
                Path = path,
                TypeName = type.GetType().Name,
                Namespace = type.Namespace,
                IsConsistent = type.IsConsistent,
                ModifiedDate = type.ModifiedDate,
                IsKnowHowProtected = type.IsKnowHowProtected,
                Description = type.ToString(),
                Attributes = EngineeringAttributeReader.Read(type)
            };
        }
    }
}
