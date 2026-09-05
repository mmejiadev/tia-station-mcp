using System;
using System.Collections.Generic;
using Siemens.Engineering.SW.Blocks;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Turns Openness blocks into descriptions the rest of the server can hold on to.
    /// </summary>
    /// <remarks>
    /// The translation itself is dull. Where it happens is not: doing it here is what keeps
    /// <c>PlcBlock</c> out of <c>ModelContextProtocol/</c>, which CLAUDE.md requires and which the
    /// MCP layer had been getting wrong at eight separate sites, each with its own copy of the same
    /// dozen assignments.
    /// </remarks>
    public static class BlockDescriber
    {
        /// <summary>Describes one block.</summary>
        /// <param name="block">The block to read.</param>
        /// <param name="path">Its full path in the project, which only the portal can work out.</param>
        /// <returns>The description.</returns>
        public static BlockDescription Describe(PlcBlock block, string path)
        {
            if (block == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "There is no block to describe");
            }

            return new BlockDescription
            {
                Name = block.Name,
                Path = path,
                TypeName = block.GetType().Name,
                Namespace = block.Namespace,
                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                IsConsistent = block.IsConsistent,
                HeaderName = block.HeaderName,
                ModifiedDate = block.ModifiedDate,
                IsKnowHowProtected = block.IsKnowHowProtected,
                Description = block.ToString(),
                Attributes = EngineeringAttributeReader.Read(block)
            };
        }

        /// <summary>Describes a group and everything under it.</summary>
        /// <param name="group">The group to walk.</param>
        /// <param name="groupPath">The path of that group, empty for the root.</param>
        /// <returns>The group, its blocks and its subgroups.</returns>
        /// <remarks>
        /// The path of each block is built on the way down rather than walked back up from the
        /// block, which is both exact and one Openness call instead of one per ancestor.
        /// </remarks>
        public static BlockGroupDescription DescribeGroup(PlcBlockGroup group, string groupPath)
        {
            if (group == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "There is no group to describe");
            }

            var blocks = new List<BlockDescription>();
            foreach (var block in group.Blocks)
            {
                blocks.Add(Describe(block, Join(groupPath, block.Name)));
            }

            var groups = new List<BlockGroupDescription>();
            foreach (var subGroup in group.Groups)
            {
                groups.Add(DescribeGroup(subGroup, Join(groupPath, subGroup.Name)));
            }

            return new BlockGroupDescription
            {
                Name = group.Name,
                Blocks = blocks,
                Groups = groups
            };
        }

        private static string Join(string groupPath, string name)
        {
            return string.IsNullOrEmpty(groupPath) ? name : $"{groupPath}/{name}";
        }
    }
}
