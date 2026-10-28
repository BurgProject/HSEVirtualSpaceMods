using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Mechanics.Components
{
    [HSESerializableStruct]
    public struct PickUpSettings : IComponentData, ITransformMotionComponent
    {
        public float3 BoundsCenter;
        public float3 BoundsExtents;
        public float SurfaceOffset;
    }
}
