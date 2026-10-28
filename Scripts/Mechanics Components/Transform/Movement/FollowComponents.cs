using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Mechanics.Components
{
    public struct FollowTarget : IComponentData
    {
        public float3 Offset;
        public Entity TargetEntity;
    }
}
