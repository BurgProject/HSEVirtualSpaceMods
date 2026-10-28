using HSE.Core.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Core.Components
{
    /// <summary>Настройки события зоны. Участники определяются только сервером после физического шага.</summary>
    [HSESerializableStruct]
    public struct TriggerGraphRule : IBufferElementData
    {
        public ActionType Event;
        public int SourceInstanceId;
        public uint RequiredPlayers;
        public bool Teleport;
        public float3 Destination;
    }
}
