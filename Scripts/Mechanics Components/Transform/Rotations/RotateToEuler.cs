using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Mechanics.Components
{
    [HSESerializableStruct]
    public struct RotateToEuler : IComponentData, IEnableableComponent, ITransformMotionComponent
    {
        public float3 TargetEuler;
        public float3 Threshold;
        public float Speed;
    }
}
