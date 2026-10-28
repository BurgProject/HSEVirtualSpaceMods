using Unity.Entities;

namespace HSE.Mechanics.Components
{
    [HSESerializableStruct]
    public struct RotateWaypointTag : IComponentData, IEnableableComponent, ITransformMotionComponent { }
}
