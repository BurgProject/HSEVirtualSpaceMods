using Unity.Entities;
using Unity.Mathematics;

[HSESerializableStruct(StateOnly = true)]
public struct GrabbableTag : IComponentData
{
    public Entity Holder;
    public float3 OriginalPosition;
    public quaternion OriginalRotation;
    public float OriginalScale;
}
