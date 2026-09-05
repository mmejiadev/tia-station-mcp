using System;

namespace TiaMcpServer.Spec
{
    /// <summary>
    /// One station in a cell, as the specification describes it.
    /// </summary>
    /// <remarks>
    /// The name is what a person reads in TIA Portal and on the machine, so it is the station's
    /// identity rather than an index. That is the reason the coordinator declares named instances
    /// instead of an array: "Drilling" tells a technician where to stand, "Stations[2]" does not.
    /// </remarks>
    public sealed class StationSpecification
    {
        private const int MinimumWorkSteps = 1;
        private const int MinimumDwellCycles = 1;

        /// <summary>Describes one station.</summary>
        /// <param name="name">Its name, used for the instance and the tags. Must be a valid SCL identifier.</param>
        /// <param name="workSteps">How many steps its sequence has.</param>
        /// <param name="dwellCycles">How many scan cycles each step occupies.</param>
        /// <exception cref="ArgumentException">The name is empty, or a count is below one.</exception>
        public StationSpecification(string name, int workSteps, int dwellCycles)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A station must have a name", nameof(name));
            }

            SclIdentifier.Require(name, "station", nameof(name));

            if (workSteps < MinimumWorkSteps)
            {
                throw new ArgumentException(
                    $"A station must have at least {MinimumWorkSteps} work step; '{name}' declares {workSteps}",
                    nameof(workSteps));
            }

            if (dwellCycles < MinimumDwellCycles)
            {
                throw new ArgumentException(
                    $"A step must occupy at least {MinimumDwellCycles} cycle; '{name}' declares {dwellCycles}",
                    nameof(dwellCycles));
            }

            Name = name;
            WorkSteps = workSteps;
            DwellCycles = dwellCycles;
        }

        /// <summary>The station's name, as it appears in the generated code.</summary>
        public string Name { get; }

        /// <summary>How many steps its sequence has.</summary>
        /// <remarks>
        /// The generated sequence walks these steps and does nothing in them. That is deliberate and
        /// is the honest shape for generated code: what a station physically does is cylinders,
        /// sensors and safety, and none of that can be inferred from a JSON file. The steps are where
        /// that work goes, and until it does the block still exercises the whole handshake.
        /// </remarks>
        public int WorkSteps { get; }

        /// <summary>How many scan cycles each step occupies.</summary>
        /// <remarks>
        /// Without this a sequence completes in one scan and nothing is observable in a watch table.
        /// A count of cycles rather than a time because the target is PLCSIM Advanced, where a timer
        /// measures the simulation host's clock and tells you less than a scan count does.
        /// </remarks>
        public int DwellCycles { get; }
    }
}
