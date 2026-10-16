using System.Collections.Generic;

namespace TiaMcpServer.Hmi
{
    /// <summary>What every screen item has: a name, a place, a look and its animations.</summary>
    public sealed class HmiItemFrame
    {
        /// <summary>Creates a frame.</summary>
        /// <param name="name">The item's object name, unique on its screen.</param>
        /// <param name="box">Where it sits and how big it is.</param>
        /// <param name="look">What it overrides of its style.</param>
        /// <param name="animations">What changes about it in runtime; possibly none.</param>
        public HmiItemFrame(string name, HmiBox box, HmiLook look, IReadOnlyList<HmiAnimation> animations)
        {
            Name = name;
            Box = box;
            Look = look;
            Animations = animations;
        }

        /// <summary>The item's object name, unique on its screen.</summary>
        public string Name { get; }

        /// <summary>Where it sits and how big it is.</summary>
        public HmiBox Box { get; }

        /// <summary>What it overrides of its style.</summary>
        public HmiLook Look { get; }

        /// <summary>What changes about it in runtime.</summary>
        public IReadOnlyList<HmiAnimation> Animations { get; }
    }
}
