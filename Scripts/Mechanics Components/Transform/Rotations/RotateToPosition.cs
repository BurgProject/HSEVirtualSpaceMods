using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Mechanics.Components
{
    [HSESerializableStruct]
    public struct RotateToPosition : IComponentData, IEnableableComponent, ITransformMotionComponent
    {
        public float3 TargetPos;
        public float RotationSpeed;
        public bool SmoothRotation;
        public bool ShoodStopOnRotationComplete;
    }
}
