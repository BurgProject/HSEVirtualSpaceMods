using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;

namespace HSE.Playtest
{
    /// <summary>The client's FirstPersonCharacterComponent without the network fields and the name-tag/view entities.</summary>
    public struct PlaytestCharacter : IComponentData
    {
        public float GroundMaxSpeed;
        public float GroundedMovementSharpness;
        public float AirAcceleration;
        public float AirMaxSpeed;
        public float AirDrag;
        public float JumpSpeed;
        public float3 Gravity;
        public bool PreventAirAccelerationAgainstUngroundedHits;
        public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;
        public float MinViewAngle;
        public float MaxViewAngle;
        public float ViewRollAmount;
        public float ViewRollSharpness;

        public float CharacterYDegrees;
        public float ViewPitchDegrees;
        public float ViewRollDegrees;
        public quaternion ViewLocalRotation;
    }

    /// <summary>The client's FirstPersonCharacterControl plus the raw input the driver writes each frame.</summary>
    public struct PlaytestCharacterControl : IComponentData
    {
        public float2 MoveInput;                // -1..1 per axis, normalised like the client's 2D composite
        public float3 MoveVector;               // world-space, computed at each fixed step from MoveInput + rotation
        public float2 LookYawPitchDegreesDelta; // accumulated by the driver, consumed by the variable update
        public bool JumpRequested;              // set on the frame Space goes down, consumed by the next fixed step
        public bool Jump;
    }
}
