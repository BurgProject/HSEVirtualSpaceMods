
using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Mechanics.Components
{
    [HSESerializableStruct]
    public struct MoveAlongVectorData : IComponentData, IEnableableComponent, ITransformMotionComponent
    {
        public float3 Direction;   
        public float Speed;        
        public float MaxDistance;  
        public float DistanceTraveled;
    }
}
