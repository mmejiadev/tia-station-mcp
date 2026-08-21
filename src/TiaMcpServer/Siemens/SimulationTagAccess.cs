#if PLCSIM_AVAILABLE
using Siemens.Simatic.Simulation.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Reads and writes tags of a virtual controller, given a live instance handle.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="SimulationRuntime"/> because that class owns controller lifetimes
    /// and this one owns the mapping between PLC data types and .NET ones. The handle is passed in
    /// and never stored: holding one here would duplicate the ownership rule that class exists to
    /// enforce.
    ///
    /// The PLCSIM API exposes one method per primitive type — <c>ReadBool</c>, <c>ReadInt16</c> and
    /// so on — and the tag list says which one applies. That mapping is a dictionary rather than a
    /// switch so an unmapped type is a lookup that finds nothing, and finding nothing is a refusal.
    /// </remarks>
    internal static class SimulationTagAccess
    {
        // Everything the controller can offer: inputs and outputs, markers, timers, counters and
        // data blocks. Asking for less would mean a caller could not observe an instance DB, which
        // is where a function block's state lives and therefore where the whole cell state is.
        private const ETagListDetails AllAreas = ETagListDetails.IOMCTDB;

        // False, deliberately. The API's default is HMI-visible tags only, and generated code that
        // nobody has marked for an HMI would then be invisible to the very tools meant to observe
        // it — the failure would read as "the tag does not exist".
        private const bool AllTagsNotOnlyHmiVisible = false;

        private static readonly Dictionary<EPrimitiveDataType, TagCodec> Codecs =
            new Dictionary<EPrimitiveDataType, TagCodec>
            {
                [EPrimitiveDataType.Bool] = new TagCodec(
                    (instance, name) => instance.ReadBool(name),
                    (instance, name, text) => instance.WriteBool(name, SimulationTagValueParser.ToBool(text))),
                [EPrimitiveDataType.Int8] = new TagCodec(
                    (instance, name) => instance.ReadInt8(name),
                    (instance, name, text) => instance.WriteInt8(name, SimulationTagValueParser.ToSInt(text))),
                [EPrimitiveDataType.Int16] = new TagCodec(
                    (instance, name) => instance.ReadInt16(name),
                    (instance, name, text) => instance.WriteInt16(name, SimulationTagValueParser.ToInt(text))),
                [EPrimitiveDataType.Int32] = new TagCodec(
                    (instance, name) => instance.ReadInt32(name),
                    (instance, name, text) => instance.WriteInt32(name, SimulationTagValueParser.ToDInt(text))),
                [EPrimitiveDataType.Int64] = new TagCodec(
                    (instance, name) => instance.ReadInt64(name),
                    (instance, name, text) => instance.WriteInt64(name, SimulationTagValueParser.ToLInt(text))),
                [EPrimitiveDataType.UInt8] = new TagCodec(
                    (instance, name) => instance.ReadUInt8(name),
                    (instance, name, text) => instance.WriteUInt8(name, SimulationTagValueParser.ToUSInt(text))),
                [EPrimitiveDataType.UInt16] = new TagCodec(
                    (instance, name) => instance.ReadUInt16(name),
                    (instance, name, text) => instance.WriteUInt16(name, SimulationTagValueParser.ToUInt(text))),
                [EPrimitiveDataType.UInt32] = new TagCodec(
                    (instance, name) => instance.ReadUInt32(name),
                    (instance, name, text) => instance.WriteUInt32(name, SimulationTagValueParser.ToUDInt(text))),
                [EPrimitiveDataType.UInt64] = new TagCodec(
                    (instance, name) => instance.ReadUInt64(name),
                    (instance, name, text) => instance.WriteUInt64(name, SimulationTagValueParser.ToULInt(text))),
                [EPrimitiveDataType.Float] = new TagCodec(
                    (instance, name) => instance.ReadFloat(name),
                    (instance, name, text) => instance.WriteFloat(name, SimulationTagValueParser.ToReal(text))),
                [EPrimitiveDataType.Double] = new TagCodec(
                    (instance, name) => instance.ReadDouble(name),
                    (instance, name, text) => instance.WriteDouble(name, SimulationTagValueParser.ToLReal(text))),
                [EPrimitiveDataType.Char] = new TagCodec(
                    (instance, name) => instance.ReadChar(name),
                    (instance, name, text) => instance.WriteChar(name, SimulationTagValueParser.ToSInt(text))),
                [EPrimitiveDataType.WChar] = new TagCodec(
                    (instance, name) => instance.ReadWChar(name).ToString(),
                    (instance, name, text) => instance.WriteWChar(name, SimulationTagValueParser.ToWChar(text)))
            };

        /// <summary>Rebuilds the tag list from the program the controller holds.</summary>
        private static void RefreshTagList(IInstance instance)
        {
            instance.UpdateTagList(AllAreas, AllTagsNotOnlyHmiVisible);
        }

        /// <summary>Lists the tags of the program the controller holds.</summary>
        /// <param name="instance">A live handle to the controller.</param>
        /// <param name="nameFilter">Case-insensitive substring the name must contain, or null for all.</param>
        /// <param name="limit">Maximum number of entries to return.</param>
        public static SimulationTagList ListTags(IInstance instance, string? nameFilter, int limit)
        {
            RefreshTagList(instance);

            var all = instance.TagInfos;
            var matching = all.Where(tag => Matches(tag, nameFilter)).ToList();

            var page = matching
                .OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(Describe)
                .ToList();

            return new SimulationTagList(page, matching.Count, all.Length);
        }

        /// <summary>Reads one tag by name.</summary>
        public static SimulationTagValue Read(IInstance instance, string tagName)
        {
            var tag = FindTag(instance, tagName);
            var codec = FindCodec(tag);

            return new SimulationTagValue(tag.Name, tag.DataType.ToString(), codec.Read(instance, tag.Name));
        }

        /// <summary>Writes one tag by name and reports what the controller holds afterwards.</summary>
        /// <remarks>
        /// The value is read back rather than echoed. A cyclic program that assigns the same tag
        /// every scan overwrites what was just written, and a caller told "written: TRUE" while the
        /// controller holds FALSE would look for the fault in the wrong place. Read-back is also
        /// how a write to a tag the program drives is discovered to be pointless.
        /// </remarks>
        public static SimulationTagValue Write(IInstance instance, string tagName, string value)
        {
            var tag = FindTag(instance, tagName);
            var codec = FindCodec(tag);

            codec.Write(instance, tag.Name, value);

            return new SimulationTagValue(tag.Name, tag.DataType.ToString(), codec.Read(instance, tag.Name));
        }

        private static bool Matches(STagInfo tag, string? nameFilter)
        {
            if (string.IsNullOrWhiteSpace(nameFilter))
            {
                return true;
            }

            return tag.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static SimulationTagInfo Describe(STagInfo tag)
        {
            return new SimulationTagInfo(
                tag.Name,
                tag.Area.ToString(),
                tag.DataType.ToString(),
                IsReadable(tag));
        }

        private static bool IsReadable(STagInfo tag)
        {
            return !IsAggregate(tag) && Codecs.ContainsKey(tag.PrimitiveDataType);
        }

        /// <summary>Whether a tag is an array, and so has no value of its own.</summary>
        /// <remarks>
        /// The shape is in <c>Dimension</c> and nowhere else: an array of Bool reports
        /// <c>PrimitiveDataType.Bool</c> exactly like a scalar does, so a check on the primitive
        /// type cannot tell them apart. That is why this is consulted on every read and write and
        /// not only when listing — the first version checked it only when listing, and reading an
        /// array then reached <c>ReadBool</c>, threw inside the PLCSIM API, and was reported as an
        /// operation failure. Which tells the caller to retry something that can never succeed.
        /// </remarks>
        private static bool IsAggregate(STagInfo tag)
        {
            return tag.Dimension != null && tag.Dimension.Length > 0;
        }

        /// <summary>Finds a tag, rebuilding the list once if it is not there.</summary>
        /// <remarks>
        /// The list is a snapshot of the program taken when it was built, so a tag missing after a
        /// fresh download is the commonest reason for "no such tag". Rebuilding once and looking
        /// again costs one API call and removes a whole class of confusing failure.
        /// </remarks>
        private static STagInfo FindTag(IInstance instance, string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "tagName is required");
            }

            var found = Lookup(instance, tagName) ?? RefreshAndLookup(instance, tagName);

            if (found.HasValue)
            {
                return found.Value;
            }

            throw new PortalException(
                PortalErrorCode.NotFound,
                $"No tag named '{tagName}' in the program this controller holds. It knows " +
                $"{instance.TagInfos.Length} tag(s); call ListSimulationTags to see them. A controller " +
                "that has never been downloaded to has none at all.");
        }

        private static STagInfo? RefreshAndLookup(IInstance instance, string tagName)
        {
            RefreshTagList(instance);

            return Lookup(instance, tagName);
        }

        private static STagInfo? Lookup(IInstance instance, string tagName)
        {
            var tags = instance.TagInfos;

            foreach (var tag in tags)
            {
                if (string.Equals(tag.Name, tagName, StringComparison.Ordinal))
                {
                    return tag;
                }
            }

            // Second pass, case-insensitively: SCL is case-insensitive, so a caller spelling
            // PieceID where the program says PieceId has made no mistake a compiler would report.
            foreach (var tag in tags)
            {
                if (string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    return tag;
                }
            }

            return null;
        }

        private static TagCodec FindCodec(STagInfo tag)
        {
            if (IsAggregate(tag))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"Tag '{tag.Name}' is an array of {tag.DataType} and has no single value. Read or " +
                    "write one element at a time, by the element's own name.");
            }

            if (Codecs.TryGetValue(tag.PrimitiveDataType, out var codec))
            {
                return codec;
            }

            throw new PortalException(
                PortalErrorCode.InvalidParams,
                $"Tag '{tag.Name}' is a {tag.DataType} and has no single value this server can read or " +
                "write. A struct and a string are reached through their members, which are separate " +
                "tags in the same list.");
        }

        /// <summary>The pair of API calls that read and write one primitive type.</summary>
        private sealed class TagCodec
        {
            private readonly Func<IInstance, string, object> _read;
            private readonly Action<IInstance, string, string> _write;

            /// <summary>Creates a codec from the read and write calls for one primitive type.</summary>
            public TagCodec(Func<IInstance, string, object> read, Action<IInstance, string, string> write)
            {
                _read = read;
                _write = write;
            }

            public object Read(IInstance instance, string tagName) => _read(instance, tagName);

            public void Write(IInstance instance, string tagName, string value) => _write(instance, tagName, value);
        }
    }
}
#endif
