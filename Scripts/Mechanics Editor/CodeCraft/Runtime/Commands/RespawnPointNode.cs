using System;
using System.Linq;
using GraphProcessor;
using HSE.Mechanics.Authorings;

namespace HSE.CodeCraft
{
    /// <summary>The Target CraftObject's position becomes the room's spawn and R-respawn point (RespawnPointAuthoring).</summary>
    [Serializable, NodeMenuItem("Hybrid/Players/Respawn Point")]
    public sealed class RespawnPointAuthoringNode : AuthoringAdapterNode<RespawnPointAuthoring>
    {
        protected override bool ValidateSettings() =>
            graph.nodes.Count(n => n is RespawnPointAuthoringNode && n.IsConnected) <= 1 ||
            Fail("Use one Respawn Point node per room.");

        protected override bool Matches(RespawnPointAuthoring authoring) => true;

        protected override void CopyTo(RespawnPointAuthoring authoring) { }
    }
}
