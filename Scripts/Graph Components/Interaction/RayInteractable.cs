using Unity.Entities;

namespace HSE.Core.Components
{
    [HSESerializableStruct]
    public struct RayInteractable: IComponentData {
        public uint CountClick;
        public uint CurrentClick;
    }
}
