using HSE.Core.Components.Network;
using Unity.Collections;
using Unity.Entities;

namespace HSE.Core.Components.Network
{
    /// <summary>Настройки ghost-префаба из Initialization; удаляются после успешной конвертации.</summary>
    [HSESerializableStruct(World = HSEComponentWorld.Both)]
    public struct NetworkObjectIdentity: IComponentData
    {
        public FixedString32Bytes UniqueName;
        public GhostType GhostType;
    }
}
