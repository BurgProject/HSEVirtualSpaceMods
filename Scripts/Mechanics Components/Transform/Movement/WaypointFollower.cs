
using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Mechanics.Components
{
    [HSESerializableStruct]
    public struct WaypointFollower: IComponentData, IEnableableComponent, ITransformMotionComponent
    {   
        public float MovementSpeed;
        public float WaypointReachedDistance;

        public bool ShoodStopOnEnd;
    }

    [HSESerializableStruct]
    public struct WaypointBuffer: IBufferElementData, ITransformMotionComponent
    {
        public float3 Position;
    }
}
