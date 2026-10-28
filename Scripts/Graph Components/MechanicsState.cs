using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Mechanics.Components
{

    [HSESerializableStruct(StateOnly = true)]
    public struct WaypointIndex : IComponentData
    {
        public int CurrentWaypointIndex;
        public float3 CurrentPos;
    }

    [HSESerializableStruct(StateOnly = true)]
    public struct CurrentEulerData : IComponentData
    {
        public float3 CurrentEuler;
    }
}
