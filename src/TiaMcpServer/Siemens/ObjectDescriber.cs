using Siemens.Engineering;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads the name and attributes of a TIA Portal object into a description that outlives it.
    /// </summary>
    /// <remarks>
    /// Takes <see cref="IEngineeringObject"/> rather than each concrete type, which is the one place
    /// in this layer where that is the right call: the four things it describes have nothing in
    /// common except being engineering objects, and a per-type overload would be four copies of the
    /// same two lines. The name is passed in because the interface does not declare one — every
    /// concrete type has it, and the caller is holding the typed object anyway.
    /// </remarks>
    public static class ObjectDescriber
    {
        /// <summary>Describes one object.</summary>
        /// <param name="engineeringObject">The object to read.</param>
        /// <param name="name">Its name, taken from the typed object the caller holds.</param>
        /// <returns>The description.</returns>
        /// <exception cref="PortalException">There is no object to describe.</exception>
        public static ObjectDescription Describe(IEngineeringObject engineeringObject, string name)
        {
            if (engineeringObject == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "There is no object to describe");
            }

            return new ObjectDescription
            {
                Name = name,
                Description = engineeringObject.ToString(),
                Attributes = EngineeringAttributeReader.Read(engineeringObject)
            };
        }
    }
}
