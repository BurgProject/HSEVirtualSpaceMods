using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HSE.Mechanics.Components
{
    [HSESerializableStruct, GhostEnabledBit]
    public struct PhysicsWaypointFollower : IComponentData, IEnableableComponent, IPhysicsMotionComponent
    {
        [GhostField] public float Speed;
        [GhostField] public float ReachedDistance;
        [GhostField] public bool Loop;
        [GhostField] public int Index;
    }

    [HSESerializableStruct]
    public struct PhysicsWaypointElement : IBufferElementData, IPhysicsMotionComponent
    {
        [GhostField] public float3 Position;
    }
}
