using Unity.Entities;
using Unity.NetCode;

namespace HSE.Mechanics.Components.Network
{
    [GhostComponent, HSESerializableStruct]
    public struct MaterialSwitcherCounter : IComponentData
    {
        [GhostField] public uint count;
        public uint currentCount;
        public uint maxCount;
    }
}
