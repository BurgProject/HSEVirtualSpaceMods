using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace HSE.Playtest
{
    public struct PlaytestCharacterUpdateContext
    {
        public void OnSystemCreate(ref SystemState state) { }
        public void OnSystemUpdate(ref SystemState state) { }
    }

    /// <summary>
    /// The client's FirstPersonCharacterProcessor (Assets/Scripts/CoreECS/Systems/FirstPersonContext), step for step,
    /// minus the network fields. One deliberate difference, for parity: <see cref="CanCollideWithHit"/> ignores dynamic
    /// and kinematic bodies. This standalone playtest does not create ghosts, while the runtime character accepts
    /// moving bodies only when they have GhostInstance. Networked platform interaction requires a multiplayer test.
    /// </summary>
    public struct PlaytestCharacterProcessor : IKinematicCharacterProcessor<PlaytestCharacterUpdateContext>
    {
        public KinematicCharacterDataAccess CharacterDataAccess;
        public RefRW<PlaytestCharacter> CharacterComponent;
        public RefRW<PlaytestCharacterControl> CharacterControl;

        public void PhysicsUpdate(ref PlaytestCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref PlaytestCharacter characterComponent = ref CharacterComponent.ValueRW;
            ref KinematicCharacterBody characterBody = ref CharacterDataAccess.CharacterBody.ValueRW;
            ref float3 characterPosition = ref CharacterDataAccess.LocalTransform.ValueRW.Position;

            KinematicCharacterUtilities.Update_Initialize(
                in this, ref context, ref baseContext, ref characterBody,
                CharacterDataAccess.CharacterHitsBuffer, CharacterDataAccess.DeferredImpulsesBuffer,
                CharacterDataAccess.VelocityProjectionHits, baseContext.Time.DeltaTime);

            KinematicCharacterUtilities.Update_ParentMovement(
                in this, ref context, ref baseContext, CharacterDataAccess.CharacterEntity, ref characterBody,
                CharacterDataAccess.CharacterProperties.ValueRO, CharacterDataAccess.PhysicsCollider.ValueRO,
                CharacterDataAccess.LocalTransform.ValueRO, ref characterPosition, characterBody.WasGroundedBeforeCharacterUpdate);

            KinematicCharacterUtilities.Update_Grounding(
                in this, ref context, ref baseContext, ref characterBody, CharacterDataAccess.CharacterEntity,
                CharacterDataAccess.CharacterProperties.ValueRO, CharacterDataAccess.PhysicsCollider.ValueRO,
                CharacterDataAccess.LocalTransform.ValueRO, CharacterDataAccess.VelocityProjectionHits,
                CharacterDataAccess.CharacterHitsBuffer, ref characterPosition);

            HandleVelocityControl(ref context, ref baseContext);

            KinematicCharacterUtilities.Update_PreventGroundingFromFutureSlopeChange(
                in this, ref context, ref baseContext, CharacterDataAccess.CharacterEntity, ref characterBody,
                CharacterDataAccess.CharacterProperties.ValueRO, CharacterDataAccess.PhysicsCollider.ValueRO,
                in characterComponent.StepAndSlopeHandling);

            KinematicCharacterUtilities.Update_GroundPushing(
                in this, ref context, ref baseContext, ref characterBody, CharacterDataAccess.CharacterProperties.ValueRO,
                CharacterDataAccess.LocalTransform.ValueRO, CharacterDataAccess.DeferredImpulsesBuffer, characterComponent.Gravity);

            KinematicCharacterUtilities.Update_MovementAndDecollisions(
                in this, ref context, ref baseContext, CharacterDataAccess.CharacterEntity, ref characterBody,
                CharacterDataAccess.CharacterProperties.ValueRO, CharacterDataAccess.PhysicsCollider.ValueRO,
                CharacterDataAccess.LocalTransform.ValueRO, CharacterDataAccess.VelocityProjectionHits,
                CharacterDataAccess.CharacterHitsBuffer, CharacterDataAccess.DeferredImpulsesBuffer, ref characterPosition);

            KinematicCharacterUtilities.Update_MovingPlatformDetection(ref baseContext, ref characterBody);

            KinematicCharacterUtilities.Update_ParentMomentum(ref baseContext, ref characterBody,
                CharacterDataAccess.LocalTransform.ValueRO.Position);

            KinematicCharacterUtilities.Update_ProcessStatefulCharacterHits(
                CharacterDataAccess.CharacterHitsBuffer, CharacterDataAccess.StatefulHitsBuffer);
        }

        void HandleVelocityControl(ref PlaytestCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            float deltaTime = baseContext.Time.DeltaTime;
            ref KinematicCharacterBody characterBody = ref CharacterDataAccess.CharacterBody.ValueRW;
            ref PlaytestCharacter characterComponent = ref CharacterComponent.ValueRW;
            ref PlaytestCharacterControl characterControl = ref CharacterControl.ValueRW;

            // Rotate move input and velocity to take into account parent rotation
            if (characterBody.ParentEntity != Entity.Null)
            {
                characterControl.MoveVector = math.rotate(characterBody.RotationFromParent, characterControl.MoveVector);
                characterBody.RelativeVelocity = math.rotate(characterBody.RotationFromParent, characterBody.RelativeVelocity);
            }

            if (characterBody.IsGrounded)
            {
                // Move on ground
                float3 targetVelocity = characterControl.MoveVector * characterComponent.GroundMaxSpeed;
                CharacterControlUtilities.StandardGroundMove_Interpolated(ref characterBody.RelativeVelocity,
                    targetVelocity, characterComponent.GroundedMovementSharpness, deltaTime, characterBody.GroundingUp,
                    characterBody.GroundHit.Normal);
                // Jump
                if (characterControl.Jump)
                {
                    CharacterControlUtilities.StandardJump(ref characterBody,
                        characterBody.GroundingUp * characterComponent.JumpSpeed, true, characterBody.GroundingUp);
                }
            }
            else
            {
                // Move in air
                float3 airAcceleration = characterControl.MoveVector * characterComponent.AirAcceleration;
                if (math.lengthsq(airAcceleration) > 0f)
                {
                    float3 tmpVelocity = characterBody.RelativeVelocity;
                    CharacterControlUtilities.StandardAirMove(ref characterBody.RelativeVelocity, airAcceleration,
                        characterComponent.AirMaxSpeed, characterBody.GroundingUp, deltaTime, false);

                    // Cancel air acceleration from input if we would hit a non-grounded surface
                    if (characterComponent.PreventAirAccelerationAgainstUngroundedHits &&
                        KinematicCharacterUtilities.MovementWouldHitNonGroundedObstruction(
                            in this, ref context, ref baseContext, CharacterDataAccess.CharacterProperties.ValueRO,
                            CharacterDataAccess.LocalTransform.ValueRO, CharacterDataAccess.CharacterEntity,
                            CharacterDataAccess.PhysicsCollider.ValueRO, characterBody.RelativeVelocity * deltaTime,
                            out ColliderCastHit hit))
                    {
                        characterBody.RelativeVelocity = tmpVelocity;
                    }
                }

                // Gravity
                CharacterControlUtilities.AccelerateVelocity(ref characterBody.RelativeVelocity, characterComponent.Gravity, deltaTime);

                // Drag
                CharacterControlUtilities.ApplyDragToVelocity(ref characterBody.RelativeVelocity, deltaTime, characterComponent.AirDrag);
            }
        }

        #region Character Processor Callbacks

        public void UpdateGroundingUp(ref PlaytestCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref KinematicCharacterBody characterBody = ref CharacterDataAccess.CharacterBody.ValueRW;
            KinematicCharacterUtilities.Default_UpdateGroundingUp(ref characterBody, CharacterDataAccess.LocalTransform.ValueRO.Rotation);
        }

        public bool CanCollideWithHit(ref PlaytestCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            in BasicHit hit)
        {
            // dynamic and kinematic bodies come first in the collision world; the client keeps a room's ones out of the
            // character's physics world (see the type summary)
            if (hit.RigidBodyIndex >= 0 && hit.RigidBodyIndex < baseContext.PhysicsWorld.NumDynamicBodies)
                return false;
            return PhysicsUtilities.IsCollidable(hit.Material);
        }

        public bool IsGroundedOnHit(ref PlaytestCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            in BasicHit hit, int groundingEvaluationType)
        {
            PlaytestCharacter characterComponent = CharacterComponent.ValueRO;
            return KinematicCharacterUtilities.Default_IsGroundedOnHit(
                in this, ref context, ref baseContext, CharacterDataAccess.CharacterEntity,
                CharacterDataAccess.PhysicsCollider.ValueRO, CharacterDataAccess.CharacterBody.ValueRO,
                CharacterDataAccess.CharacterProperties.ValueRO, in hit, in characterComponent.StepAndSlopeHandling,
                groundingEvaluationType);
        }

        public void OnMovementHit(ref PlaytestCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterHit hit, ref float3 remainingMovementDirection, ref float remainingMovementLength,
            float3 originalVelocityDirection, float hitDistance)
        {
            ref KinematicCharacterBody characterBody = ref CharacterDataAccess.CharacterBody.ValueRW;
            ref float3 characterPosition = ref CharacterDataAccess.LocalTransform.ValueRW.Position;
            PlaytestCharacter characterComponent = CharacterComponent.ValueRO;

            KinematicCharacterUtilities.Default_OnMovementHit(
                in this, ref context, ref baseContext, ref characterBody, CharacterDataAccess.CharacterEntity,
                CharacterDataAccess.CharacterProperties.ValueRO, CharacterDataAccess.PhysicsCollider.ValueRO,
                CharacterDataAccess.LocalTransform.ValueRO, ref characterPosition, CharacterDataAccess.VelocityProjectionHits,
                ref hit, ref remainingMovementDirection, ref remainingMovementLength, originalVelocityDirection, hitDistance,
                characterComponent.StepAndSlopeHandling.StepHandling,
                characterComponent.StepAndSlopeHandling.MaxStepHeight,
                characterComponent.StepAndSlopeHandling.CharacterWidthForStepGroundingCheck);
        }

        public void OverrideDynamicHitMasses(ref PlaytestCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            ref PhysicsMass characterMass, ref PhysicsMass otherMass, BasicHit hit)
        {
        }

        public void ProjectVelocityOnHits(ref PlaytestCharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            ref float3 velocity, ref bool characterIsGrounded, ref BasicHit characterGroundHit,
            in Unity.Entities.DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits, float3 originalVelocityDirection)
        {
            PlaytestCharacter characterComponent = CharacterComponent.ValueRO;
            KinematicCharacterUtilities.Default_ProjectVelocityOnHits(
                ref velocity, ref characterIsGrounded, ref characterGroundHit, in velocityProjectionHits,
                originalVelocityDirection, characterComponent.StepAndSlopeHandling.ConstrainVelocityToGroundPlane,
                in CharacterDataAccess.CharacterBody.ValueRO);
        }

        #endregion
    }

    /// <summary>The rotation helpers of the client's FirstPersonCharacterUtilities that the character uses.</summary>
    public static class PlaytestCharacterUtilities
    {
        public static void ComputeFinalRotationsFromRotationDelta(
            ref float viewPitchDegrees, ref float characterRotationYDegrees, float3 characterTransformUp,
            float2 yawPitchDeltaDegrees, float viewRollDegrees, float minPitchDegrees, float maxPitchDegrees,
            out quaternion characterRotation, out quaternion viewLocalRotation)
        {
            // Yaw
            characterRotationYDegrees += yawPitchDeltaDegrees.x;
            ComputeRotationFromYAngleAndUp(characterRotationYDegrees, characterTransformUp, out characterRotation);

            // Pitch
            viewPitchDegrees += yawPitchDeltaDegrees.y;
            viewPitchDegrees = math.clamp(viewPitchDegrees, minPitchDegrees, maxPitchDegrees);

            viewLocalRotation = CalculateLocalViewRotation(viewPitchDegrees, viewRollDegrees);
        }

        public static void ComputeRotationFromYAngleAndUp(float characterRotationYDegrees, float3 characterTransformUp,
            out quaternion characterRotation)
        {
            characterRotation = math.mul(MathUtilities.CreateRotationWithUpPriority(characterTransformUp, math.forward()),
                quaternion.Euler(0f, math.radians(characterRotationYDegrees), 0f));
        }

        public static quaternion CalculateLocalViewRotation(float viewPitchDegrees, float viewRollDegrees)
        {
            quaternion viewLocalRotation = quaternion.AxisAngle(-math.right(), math.radians(viewPitchDegrees));
            viewLocalRotation = math.mul(viewLocalRotation, quaternion.AxisAngle(math.forward(), math.radians(viewRollDegrees)));
            return viewLocalRotation;
        }
    }
}
