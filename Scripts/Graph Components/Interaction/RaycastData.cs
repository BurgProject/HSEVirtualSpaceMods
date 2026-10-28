using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HSE.Core.Components
{
    [HSESerializableStruct(StateOnly = true)]
    public struct RaycastData: IComponentData, IEnableableComponent{
        public Entity EntityFrom;
    }
    public struct RaycastConstantData: IComponentData{
        public Entity entity;
    }

    [GhostComponent]
    public struct MovingPlatformSingletone : IComponentData
    {
       [GhostField] public float3 Vector;
        public bool need;
    }

    public struct MovingPlatformController : IComponentData
    {
        public float3 Vector;
    }
}
