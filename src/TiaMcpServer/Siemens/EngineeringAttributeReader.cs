using System;
using System.Collections.Generic;
using Siemens.Engineering;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads the attribute bag of a TIA Portal object into values that outlive it.
    /// </summary>
    /// <remarks>
    /// This used to live in the MCP layer, which meant the MCP layer took an
    /// <see cref="IEngineeringObject"/> as a parameter and so had to reference Openness. It belongs
    /// here: reading attributes is an Openness operation, and what the layer above wants is the
    /// result of it.
    /// </remarks>
    public static class EngineeringAttributeReader
    {
        /// <summary>Reads every attribute Openness exposes on an object.</summary>
        /// <param name="engineeringObject">The object to read, or null.</param>
        /// <returns>The attributes. An empty list when there is no object.</returns>
        public static IReadOnlyList<ObjectAttribute> Read(IEngineeringObject? engineeringObject)
        {
            var attributes = new List<ObjectAttribute>();

            if (engineeringObject == null)
            {
                return attributes;
            }

            foreach (var information in engineeringObject.GetAttributeInfos())
            {
                attributes.Add(new ObjectAttribute(
                    information.Name,
                    engineeringObject.GetAttribute(information.Name),
                    Enum.GetName(typeof(EngineeringAttributeAccessMode), information.AccessMode)));
            }

            return attributes;
        }
    }
}
