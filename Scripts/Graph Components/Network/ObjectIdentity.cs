using Unity.Entities;

namespace HSE.Core.Components
{
    [HSESerializableStruct]
    public struct ObjectIdentity : IComponentData
    {
        public int InstanceID;
        public ObjectIdentity(int instanceID) => InstanceID = instanceID;
    }
}
