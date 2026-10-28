using Unity.Entities;

namespace HSE.Core.Components
{
    [HSESerializableStruct(World = HSEComponentWorld.Both)]
    public struct MovingPlatformTag : IComponentData { }
}
