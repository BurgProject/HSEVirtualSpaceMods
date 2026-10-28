using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Mechanics.Components
{
    [HSESerializableStruct]
    public struct RotateAroundPoint : IComponentData, IEnableableComponent, ITransformMotionComponent
    {
        public float3 CenterPoint;
        public float3 Axis;
        public float Radius;
        public float Speed;
        public float Angle;
    }
}
