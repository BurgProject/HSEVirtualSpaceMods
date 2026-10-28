using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Serialization;

namespace HSE.Mechanics.Components
{
    [HSESerializableStruct(World = HSEComponentWorld.Client)]
    public struct UiPanelState : IComponentData, IEnableableComponent
    {
        public FixedString128Bytes Text;
        public float3 WorldOffset;
        [FormerlySerializedAs("Offset")] public float2 ScreenOffset;
    }

    [HSESerializableStruct(World = HSEComponentWorld.Client)]
    public struct PointerHintState : IComponentData, IEnableableComponent
    {
        public FixedString128Bytes Text;
        public float2 Offset;
    }
}
