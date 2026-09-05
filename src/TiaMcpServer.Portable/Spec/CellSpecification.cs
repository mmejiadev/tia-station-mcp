using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Spec
{
    /// <summary>
    /// A cell: an ordered line of stations a piece travels through.
    /// </summary>
    /// <remarks>
    /// Order is the whole content of this type. A cell is not a set of stations, it is a sequence,
    /// and which station follows which is what the handshake is about.
    ///
    /// **The server knows nothing about any particular cell.** This describes the shape of a cell in
    /// general; the four-station cell of the coursework is a JSON file in <c>spec/cells/</c>, and
    /// changing it changes no code.
    /// </remarks>
    public sealed class CellSpecification
    {
        private readonly List<StationSpecification> _stations;

        /// <summary>Describes a cell.</summary>
        /// <param name="name">The cell's name, used for the coordinator block.</param>
        /// <param name="stations">Its stations, in the order a piece visits them.</param>
        /// <exception cref="ArgumentException">The name is empty, there are no stations, or two share a name.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="stations"/> is null.</exception>
        public CellSpecification(string name, IEnumerable<StationSpecification> stations)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A cell must have a name", nameof(name));
            }

            SclIdentifier.Require(name, "cell", nameof(name));

            if (stations == null)
            {
                throw new ArgumentNullException(nameof(stations));
            }

            _stations = stations.ToList();

            if (_stations.Count == 0)
            {
                throw new ArgumentException($"Cell '{name}' declares no stations", nameof(stations));
            }

            // Two stations of the same name would generate two instances of the same name, and the
            // compiler would report a duplicate declaration in generated code nobody wrote by hand.
            // Refusing here names the actual mistake.
            var duplicate = _stations
                .GroupBy(station => station.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicate != null)
            {
                throw new ArgumentException(
                    $"Cell '{name}' declares the station '{duplicate.Key}' more than once",
                    nameof(stations));
            }

            Name = name;
        }

        /// <summary>The cell's name.</summary>
        public string Name { get; }

        /// <summary>Its stations, in the order a piece visits them.</summary>
        public IReadOnlyList<StationSpecification> Stations => _stations;

        /// <summary>Each place a piece moves from one station to the next.</summary>
        /// <returns>N-1 handovers for N stations, in order. Empty for a single station.</returns>
        /// <remarks>
        /// Computed rather than stored: a handover is a consequence of the order, not a separate fact
        /// that could disagree with it.
        /// </remarks>
        public IReadOnlyList<StationHandover> Handovers()
        {
            return Enumerable
                .Range(0, Math.Max(0, _stations.Count - 1))
                .Select(index => new StationHandover(_stations[index], _stations[index + 1], index + 1))
                .ToList();
        }
    }
}
