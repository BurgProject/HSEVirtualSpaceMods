using HSE.Core.Components;
using Unity.Entities;
using UnityEngine;

namespace HSE.Mechanics.Authorings
{
    /// <summary>
    /// Marks this object's position (and its Y rotation) as the room's spawn and R-respawn point. Set by the graph's
    /// "Respawn Point" node or added by hand. One per room: with none (or several) players use the origin.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RespawnPointAuthoring : MonoBehaviour
    {
        private sealed class Baker : Baker<RespawnPointAuthoring>
        {
            public override void Bake(RespawnPointAuthoring authoring)
            {
                // A separate plain entity: the object itself may be turned into a ghost, but the point must exist in
                // both worlds as soon as the SubScene loads (the first player spawns right away)
                var transform = GetComponent<Transform>();
                var entity = CreateAdditionalEntity(TransformUsageFlags.None, false, "RespawnPoint");
                AddComponent(entity, new RespawnPoint
                {
                    Position = transform.position,
                    Rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0),
                });
            }
        }
    }
}
