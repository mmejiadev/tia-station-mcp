using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Turns SCL text into blocks inside a PLC program.
    /// </summary>
    /// <remarks>
    /// This is the half of the project that reading a project cannot provide. Openness has no API
    /// for "create a block from this text": the only route is to write the text to a file, register
    /// that file as an external source, and ask TIA Portal to generate blocks from it.
    ///
    /// Two consequences worth knowing. The external source is a real object that stays in the
    /// project tree, so it is deleted afterwards — otherwise every generation litters the project
    /// with one more entry. And generation reports failure by producing nothing rather than by
    /// throwing, so an empty result is treated as an error here.
    /// </remarks>
    public sealed class SclBlockGenerator
    {
        private const string SourceExtension = ".scl";
        private const string SourceNamePrefix = "tia_station_mcp_";

        private readonly PlcSoftware _software;
        private readonly ILogger? _logger;

        /// <summary>Creates a generator for one PLC software container.</summary>
        /// <param name="software">The PLC software the blocks are generated into.</param>
        /// <param name="logger">Optional logger.</param>
        public SclBlockGenerator(PlcSoftware software, ILogger? logger = null)
        {
            _software = software ?? throw new ArgumentNullException(nameof(software));
            _logger = logger;
        }

        /// <summary>
        /// Generates blocks from SCL source text.
        /// </summary>
        /// <param name="sclCode">The SCL source. May declare more than one block or type.</param>
        /// <returns>The names of the blocks and of the PLC data types that were generated.</returns>
        /// <exception cref="PortalException">
        /// The source is empty, or TIA Portal accepted the source but generated nothing, which is
        /// how it reports that the SCL does not parse.
        /// </exception>
        public SclGeneration Generate(string sclCode)
        {
            if (string.IsNullOrWhiteSpace(sclCode))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "sclCode is required");
            }

            var sourceFile = WriteToTemporaryFile(sclCode);

            try
            {
                return GenerateFrom(sourceFile);
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        private SclGeneration GenerateFrom(string sourceFile)
        {
            var sourceName = SourceNamePrefix + DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);

            var externalSource = _software.ExternalSourceGroup.ExternalSources.CreateFromFile(sourceName, sourceFile);

            try
            {
                // KeepOnError leaves the existing blocks untouched when the source does not
                // compile, instead of half-replacing a working program with a broken one.
                var generated = externalSource.GenerateBlocksFromSource(GenerateBlockOption.KeepOnError);

                var result = Sort(generated);

                if (result.Blocks.Count + result.Types.Count == 0)
                {
                    throw new PortalException(
                        PortalErrorCode.InvalidParams,
                        "TIA Portal generated no blocks or types from the source. The SCL is most likely invalid; compile the software to see why.");
                }

                _logger?.LogInformation("Generated from SCL: blocks {Blocks}; types {Types}", string.Join(", ", result.Blocks), string.Join(", ", result.Types));

                return result;
            }
            finally
            {
                // The external source is scaffolding, not part of the program. Leaving it behind
                // would add an entry to the project tree on every single generation.
                externalSource.Delete();
            }
        }

        /// <remarks>
        /// A source that declares only a TYPE generates a PlcType and no block. Counting blocks alone
        /// refused it as invalid SCL after TIA Portal had already created the type.
        /// </remarks>
        private static SclGeneration Sort(IList<IEngineeringObject> generated)
        {
            var blocks = new List<string>();
            var types = new List<string>();

            foreach (var item in generated)
            {
                if (item is PlcBlock block)
                {
                    blocks.Add(block.Name);
                }

                if (item is PlcType type)
                {
                    types.Add(type.Name);
                }
            }

            return new SclGeneration(blocks, types);
        }

        private static string WriteToTemporaryFile(string sclCode)
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + SourceExtension);

            // TIA Portal reads the source with the system's ANSI expectations for SCL; writing a
            // BOM here makes the first declaration fail to parse.
            File.WriteAllText(path, sclCode, new System.Text.UTF8Encoding(false));

            return path;
        }
    }
}
