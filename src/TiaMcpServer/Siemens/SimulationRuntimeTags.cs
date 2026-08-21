using System.Collections.Generic;
#if PLCSIM_AVAILABLE
using Microsoft.Extensions.Logging;
#endif

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Tag access on a virtual controller: what makes a running program observable.
    /// </summary>
    /// <remarks>
    /// Split from <c>SimulationRuntime.cs</c> by functional area rather than made a class of its
    /// own, because it needs the instance handles that class holds and holding them anywhere else
    /// would break the ownership rule its remarks explain. Lifetime is there; observation is here.
    ///
    /// A tag list exists only once a program has been downloaded: it is read from the controller,
    /// not from the project. So every method here reports an empty list or "no such tag" on a
    /// controller nobody has downloaded to, which is the truth rather than a failure.
    /// </remarks>
    public sealed partial class SimulationRuntime
    {
        /// <summary>How many tags a list returns when the caller does not say.</summary>
        /// <remarks>
        /// A CPU's tag list runs to thousands of entries and the caller is usually a language
        /// model. Returning all of them would spend a context window on tags nobody asked about,
        /// so the default is a page and <c>nameFilter</c> is how you find what you want.
        /// </remarks>
        public const int DefaultTagLimit = 200;

        /// <summary>Lists the tags of the program a virtual controller holds.</summary>
        /// <param name="instanceName">The virtual controller to ask.</param>
        /// <param name="nameFilter">Case-insensitive substring the name must contain, or null for all.</param>
        /// <param name="limit">Maximum entries to return; at least one.</param>
        /// <returns>The matching tags, ordered by name, with the counts they were taken from.</returns>
        /// <exception cref="PortalException">The runtime is unavailable, or there is no such instance.</exception>
        public SimulationTagList ListTags(string instanceName, string? nameFilter = null, int limit = DefaultTagLimit)
        {
            RequireRuntime();
            RequireName(instanceName);
            RequireLimit(limit);

#if PLCSIM_AVAILABLE
            return Execute("Tag list", instanceName, () =>
            {
                SimulationTagList? tags = null;

                UseInstance(instanceName, instance => tags = SimulationTagAccess.ListTags(instance, nameFilter, limit));

                _logger?.LogDebug("Listed {Count} of {Total} tag(s) of {Name}", tags!.Items.Count, tags.TotalCount, instanceName);

                return tags!;
            });
#else
            throw new PortalException(PortalErrorCode.InvalidState, UnavailableMessage);
#endif
        }

        /// <summary>Reads tags of a virtual controller by name.</summary>
        /// <param name="instanceName">The virtual controller to read from.</param>
        /// <param name="tagNames">Fully qualified tag names with no quotes, e.g. <c>DB_Cell.Feeder.Step</c>.</param>
        /// <returns>One value per name, in the order asked for.</returns>
        /// <remarks>
        /// Several names in one call rather than one per call, and deliberately: the values are
        /// read through a single handle within one call, so a caller watching a handshake sees a
        /// set of values from nearly the same moment rather than from four round trips. It is not a
        /// consistent snapshot — the controller keeps scanning — and nothing here pretends it is.
        /// </remarks>
        /// <exception cref="PortalException">The runtime is unavailable, there is no such instance, or a name is not a tag.</exception>
        public IReadOnlyList<SimulationTagValue> ReadTags(string instanceName, IReadOnlyList<string> tagNames)
        {
            RequireRuntime();
            RequireName(instanceName);
            RequireTagNames(tagNames);

#if PLCSIM_AVAILABLE
            return Execute("Tag read", instanceName, () =>
            {
                var values = new List<SimulationTagValue>(tagNames.Count);

                UseInstance(instanceName, instance =>
                {
                    foreach (var tagName in tagNames)
                    {
                        values.Add(SimulationTagAccess.Read(instance, tagName));
                    }
                });

                return (IReadOnlyList<SimulationTagValue>)values;
            });
#else
            throw new PortalException(PortalErrorCode.InvalidState, UnavailableMessage);
#endif
        }

        /// <summary>Writes one tag of a virtual controller.</summary>
        /// <param name="instanceName">The virtual controller to write to.</param>
        /// <param name="tagName">The fully qualified tag name.</param>
        /// <param name="value">The value as text; it is parsed as the tag's declared type.</param>
        /// <returns>What the controller holds after the write, read back rather than echoed.</returns>
        /// <remarks>
        /// One tag per call, unlike the read. A write is a change to what a controller is doing, so
        /// it is one auditable action with one target; a batch would record several changes as one
        /// line and there would be no way to say which of them a refusal refused.
        /// </remarks>
        /// <exception cref="PortalException">The runtime is unavailable, there is no such instance or tag, or the value does not fit the type.</exception>
        public SimulationTagValue WriteTag(string instanceName, string tagName, string value)
        {
            RequireRuntime();
            RequireName(instanceName);

#if PLCSIM_AVAILABLE
            return Execute("Tag write", instanceName, () =>
            {
                SimulationTagValue? written = null;

                UseInstance(instanceName, instance => written = SimulationTagAccess.Write(instance, tagName, value));

                _logger?.LogInformation("Wrote {Value} to {Tag} on {Name}", value, tagName, instanceName);

                return written!;
            });
#else
            throw new PortalException(PortalErrorCode.InvalidState, UnavailableMessage);
#endif
        }

        private static void RequireLimit(int limit)
        {
            if (limit < 1)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "limit must be at least 1");
            }
        }

        private static void RequireTagNames(IReadOnlyList<string> tagNames)
        {
            if (tagNames == null || tagNames.Count == 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "at least one tag name is required");
            }
        }
    }
}
