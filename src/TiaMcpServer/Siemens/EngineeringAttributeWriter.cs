using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads and writes the named attributes of any engineering object: a device item's cycle and
    /// protection level, a watch table row's address and format, and whatever else Openness keeps
    /// in an attribute bag rather than in a settable property.
    /// </summary>
    /// <remarks>
    /// The pair of <see cref="EngineeringAttributeReader"/>, which reads the whole bag; this one
    /// writes one entry of it.
    ///
    /// It was called DeviceParameterConfigurator until 2026-09-22, when the watch table rows of
    /// phase 8 turned out to need exactly this and to be no kind of device. The signature already
    /// took an <c>IEngineeringObject</c>, so only the name was wrong, and a name that lies about
    /// what a class accepts is how the same safety check comes to be written twice.
    ///
    /// Openness exposes these settings as an attribute bag rather than as typed properties, which
    /// makes one pair of tools cover everything an object has instead of one tool per setting. The
    /// price is that nothing is checked at compile time, so the checking happens here: the
    /// attribute must exist, it must be writable, and the value must fit the type it already holds.
    ///
    /// A read-only attribute is refused before TIA is asked. It is the commonest mistake with a bag
    /// like this — most of what an item reports cannot be set — and Openness answers it with the
    /// same unhelpful failure it gives for a misspelt name.
    /// </remarks>
    public sealed class EngineeringAttributeWriter
    {
        // How much of a misspelt name has to match for a parameter to be offered in its place, and
        // how many are offered: four letters separate StartupMode from ProtectionLevel, and ten
        // still fit in a message somebody reads.
        private const int SimilarPrefixLength = 4;
        private const int MaxSimilarNames = 10;

        // Openness has four access modes: None, Read, Write and ReadWrite. Two of them can be
        // written, and treating only ReadWrite as writable would refuse a parameter TIA accepts.
        private static readonly string[] WritableModes = { "ReadWrite", "Write" };

        private readonly ILogger? _logger;

        /// <summary>Creates a parameter configurator.</summary>
        /// <param name="logger">Optional logger.</param>
        public EngineeringAttributeWriter(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>Reads the parameters of an item that can actually be changed.</summary>
        /// <param name="engineeringObject">The device item.</param>
        /// <returns>Its writable attributes, with the values they hold.</returns>
        /// <remarks>
        /// The writable subset rather than everything, because this read exists to aim the write.
        /// GetDeviceItemInfo still prints the whole bag for anybody wanting to look around.
        /// </remarks>
        public static IReadOnlyList<ObjectAttribute> ReadWritable(IEngineeringObject engineeringObject)
        {
            return EngineeringAttributeReader.Read(engineeringObject)
                .Where(attribute => IsWritable(attribute))
                .ToList();
        }

        /// <summary>Sets one parameter of a device item.</summary>
        /// <param name="engineeringObject">The device item.</param>
        /// <param name="attributeName">The parameter, as GetDeviceParameters names it.</param>
        /// <param name="value">The value, as text; it is converted to the type in place.</param>
        /// <returns>The parameter as it stands afterwards, read back rather than echoed.</returns>
        /// <exception cref="PortalException">
        /// The parameter does not exist, it cannot be written, the value does not fit, or TIA
        /// Portal refused it.
        /// </exception>
        /// <remarks>
        /// From the lookup onwards Openness is handed the attribute's own spelling, never the
        /// caller's. The lookup ignores case and nothing documents that Openness does, so passing
        /// on what the caller typed could turn a name that was found into one TIA says it does not
        /// know.
        /// </remarks>
        public ObjectAttribute Set(IEngineeringObject engineeringObject, string attributeName, string value)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "parameterName is required");
            }

            var current = RequireWritable(engineeringObject, attributeName);

            var converted = ParameterValueParser.Parse(current.Value, value, current.Name);

            Apply(engineeringObject, converted, current);

            var after = engineeringObject.GetAttribute(current.Name);

            _logger?.LogInformation("{Parameter} set to {Value}", current.Name, after);

            return new ObjectAttribute(current.Name, after, current.AccessMode);
        }

        /// <remarks>
        /// Names near misses when the parameter is not there. An attribute bag has dozens of
        /// entries with names like <c>StartupMode</c> and <c>Startup</c>, and "no such parameter"
        /// on its own sends somebody to read all of them.
        /// </remarks>
        private static ObjectAttribute RequireWritable(IEngineeringObject engineeringObject, string attributeName)
        {
            var all = EngineeringAttributeReader.Read(engineeringObject);

            var found = all.FirstOrDefault(
                attribute => string.Equals(attribute.Name, attributeName, StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                throw new PortalException(
                    PortalErrorCode.NotFound,
                    $"No parameter called '{attributeName}'. Ones with similar names: {FindSimilarNames(all, attributeName)}");
            }

            if (!IsWritable(found))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"'{found.Name}' is {found.AccessMode ?? "not writable"} and holds '{ParameterValueFormatter.Format(found.Value)}'. " +
                    "Most of what a device reports describes it rather than configures it; " +
                    "call GetDeviceParameters for the ones that can be set.");
            }

            return found;
        }

        private static bool IsWritable(ObjectAttribute attribute)
        {
            return WritableModes.Any(mode => string.Equals(attribute.AccessMode, mode, StringComparison.Ordinal));
        }

        private static string FindSimilarNames(IReadOnlyList<ObjectAttribute> all, string attributeName)
        {
            var start = attributeName.Length < SimilarPrefixLength
                ? attributeName
                : attributeName.Substring(0, SimilarPrefixLength);

            var near = all
                .Where(attribute => attribute.Name.IndexOf(start, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(attribute => attribute.Name)
                .Take(MaxSimilarNames)
                .ToList();

            return near.Count == 0 ? "none; call GetDeviceParameters to see them" : string.Join(", ", near);
        }

        /// <remarks>
        /// Every failure of the call is reported as a refused value, and that is a guess rather than
        /// a measurement: the Openness documentation names no exception for <c>SetAttribute</c>, so
        /// nothing yet tells a value the device will not take apart from TIA Portal failing.
        /// </remarks>
        private static void Apply(IEngineeringObject engineeringObject, object value, ObjectAttribute current)
        {
            try
            {
                engineeringObject.SetAttribute(current.Name, value);
            }
            catch (Exception failure)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"TIA Portal refused '{ParameterValueFormatter.Format(value)}' for '{current.Name}', " +
                    $"which holds '{ParameterValueFormatter.Format(current.Value)}'. " +
                    "A parameter can be writable and still refuse a value the device does not allow.",
                    null,
                    failure);
            }
        }
    }
}
