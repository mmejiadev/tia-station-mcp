using TiaMcpServer.UnifiedFaceplates;

namespace TiaMcpServer.CustomWebControls
{
    /// <summary>One Custom Web Control to place on a screen: its name there, which control, where, and what its interface gets.</summary>
    public sealed class CustomWebControlPlacement
    {
        /// <summary>Creates the placement.</summary>
        /// <param name="name">The screen item's name, e.g. <c>Pump_1</c>.</param>
        /// <param name="control">The control's <c>identity.name</c>, e.g. <c>TiaStation.Motor</c>.</param>
        /// <param name="frame">Where it goes and how big; a size left out takes the control's default.</param>
        /// <param name="interface">What its properties get: fixed values, tags bound, and scripts for its events.</param>
        public CustomWebControlPlacement(string name, string control, UnifiedFaceplateFrame frame, CustomWebControlInterface @interface)
        {
            Name = name;
            Control = control;
            Frame = frame;
            Interface = @interface;
        }

        /// <summary>The screen item's name.</summary>
        public string Name { get; }

        /// <summary>The control's <c>identity.name</c>.</summary>
        public string Control { get; }

        /// <summary>Where it goes and how big.</summary>
        public UnifiedFaceplateFrame Frame { get; }

        /// <summary>What its properties and events get.</summary>
        public CustomWebControlInterface Interface { get; }
    }
}
