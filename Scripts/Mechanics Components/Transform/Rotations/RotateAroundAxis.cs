using System;
using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Mechanics.Components
{
    [HSESerializableStruct]
    [Serializable]
    public struct RotateAroundAxis: IComponentData, IEnableableComponent, ITransformMotionComponent
    {
        public float3 Axis;
        public float Speed;
    }
}
