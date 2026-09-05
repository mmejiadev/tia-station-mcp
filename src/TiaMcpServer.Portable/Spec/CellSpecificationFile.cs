using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Spec
{
    /// <summary>
    /// Reads a cell specification from its JSON file.
    /// </summary>
    /// <remarks>
    /// A real file rather than a specification built in code, for the reason the write policy is one
    /// too: loading is where a specification will actually be got wrong. A key spelled differently, a
    /// station name with a space in it, a count left at zero. Tests that construct the object never
    /// exercise any of that.
    ///
    /// Comments are allowed and trailing commas are tolerated, because these files are edited by a
    /// person deciding what a cell is and a person needs to be able to write down why.
    /// </remarks>
    public static class CellSpecificationFile
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>Loads a cell from a file.</summary>
        /// <param name="path">Path to the JSON file.</param>
        /// <returns>The cell.</returns>
        /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
        /// <exception cref="PortalException">The file is missing, unreadable, or does not describe a cell.</exception>
        public static CellSpecification Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A cell specification needs a path", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"No cell specification at '{path}'. Cells live in spec/cells/.");
            }

            return Parse(Read(path), path);
        }

        private static string Read(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"The cell specification at '{path}' could not be read: {exception.Message}",
                    null,
                    exception);
            }
        }

        private static CellSpecification Parse(string json, string path)
        {
            Document? document;

            try
            {
                document = JsonSerializer.Deserialize<Document>(json, Options);
            }
            catch (JsonException exception)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"The cell specification at '{path}' is not valid JSON: {exception.Message}",
                    null,
                    exception);
            }

            if (document == null)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"The cell specification at '{path}' is empty.");
            }

            try
            {
                return Build(document);
            }
            catch (ArgumentException exception)
            {
                // The specification types validate; this turns their complaint into one that names the
                // file, because "a station must have a name" is not actionable without knowing which
                // file to open.
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    $"The cell specification at '{path}' is not usable: {exception.Message}",
                    null,
                    exception);
            }
        }

        private static CellSpecification Build(Document document)
        {
            var stations = (document.Stations ?? new List<StationDocument>())
                .Select(station => new StationSpecification(
                    station.Name ?? string.Empty,
                    station.WorkSteps,
                    station.DwellCycles))
                .ToList();

            return new CellSpecification(document.Cell ?? string.Empty, stations);
        }

        // CA1812 reports these as never instantiated, and it cannot see otherwise: the only thing
        // that creates them is System.Text.Json, by reflection. Suppressed here and nowhere else.
#pragma warning disable CA1812

        /// <summary>The file's shape, and nothing more.</summary>
        /// <remarks>
        /// A separate type from <see cref="CellSpecification"/> on purpose. This one mirrors the JSON,
        /// with everything nullable because a file can omit anything; the domain type refuses to exist
        /// in an invalid state. Deserialising straight into the domain type would mean giving it
        /// setters and a parameterless constructor, and then nothing would guarantee its invariants.
        /// </remarks>
        private sealed class Document
        {
            [JsonPropertyName("cell")]
            public string? Cell { get; set; }

            [JsonPropertyName("stations")]
            public List<StationDocument>? Stations { get; set; }
        }

        private sealed class StationDocument
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("workSteps")]
            public int WorkSteps { get; set; }

            [JsonPropertyName("dwellCycles")]
            public int DwellCycles { get; set; }
        }

#pragma warning restore CA1812
    }
}
