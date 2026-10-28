using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace HSE.Playtest
{
    /// <summary>
    /// Fixed-step character update: the client's FirstPersonPlayerFixedStepControlSystem (move vector from input and the
    /// character's rotation, jump) followed by its FirstPersonCharacterPhysicsUpdateSystem. Never auto-created: the
    /// play-mode driver adds it to the world the room loaded into.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup))]
    [BurstCompile]
    public partial struct PlaytestCharacterPhysicsUpdateSystem : ISystem
    {
        EntityQuery m_CharacterQuery;
        PlaytestCharacterUpdateContext m_Context;
        KinematicCharacterUpdateContext m_BaseContext;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_CharacterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
                .WithAll<PlaytestCharacter, PlaytestCharacterControl>()
                .Build(ref state);

            m_Context = new PlaytestCharacterUpdateContext();
            m_Context.OnSystemCreate(ref state);
            m_BaseContext = new KinematicCharacterUpdateContext();
            m_BaseContext.OnSystemCreate(ref state);

            state.RequireForUpdate(m_CharacterQuery);
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_Context.OnSystemUpdate(ref state);
            m_BaseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());

            state.Dependency = new PlaytestCharacterPhysicsUpdateJob
            {
                Context = m_Context,
                BaseContext = m_BaseContext,
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct PlaytestCharacterPhysicsUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public PlaytestCharacterUpdateContext Context;
            public KinematicCharacterUpdateContext BaseContext;

            void Execute(
                Entity entity,
                RefRW<LocalTransform> localTransform,
                RefRW<KinematicCharacterProperties> characterProperties,
                RefRW<KinematicCharacterBody> characterBody,
                RefRW<PhysicsCollider> physicsCollider,
                RefRW<PlaytestCharacter> characterComponent,
                RefRW<PlaytestCharacterControl> characterControl,
                DynamicBuffer<KinematicCharacterHit> characterHitsBuffer,
                DynamicBuffer<StatefulKinematicCharacterHit> statefulHitsBuffer,
                DynamicBuffer<KinematicCharacterDeferredImpulse> deferredImpulsesBuffer,
                DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits)
            {
                // the client's fixed-step control: move relative to the character's facing, clamped to 1; jump this step
                ref PlaytestCharacterControl control = ref characterControl.ValueRW;
                quaternion characterRotation = localTransform.ValueRO.Rotation;
                float3 characterForward = math.mul(characterRotation, math.forward());
                float3 characterRight = math.mul(characterRotation, math.right());
                control.MoveVector = MathUtilities.ClampToMaxLength(
                    control.MoveInput.y * characterForward + control.MoveInput.x * characterRight, 1f);
                control.Jump = control.JumpRequested;
                control.JumpRequested = false;

                var processor = new PlaytestCharacterProcessor
                {
                    CharacterDataAccess = new KinematicCharacterDataAccess(
                        entity, localTransform, characterProperties, characterBody, physicsCollider,
                        characterHitsBuffer, statefulHitsBuffer, deferredImpulsesBuffer, velocityProjectionHits),
                    CharacterComponent = characterComponent,
                    CharacterControl = characterControl,
                };
                processor.PhysicsUpdate(ref Context, ref BaseContext);
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                BaseContext.EnsureCreationOfTmpCollections();
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
                bool chunkWasExecuted)
            { }
        }
    }

    /// <summary>
    /// Per-frame part: the client's FirstPersonCharacterVariableUpdateSystem (yaw on the body, pitch on the view, from
    /// the look input) and its presentation-only view roll. Never auto-created.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct PlaytestCharacterVariableUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlaytestCharacter>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            float deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (localTransform, character, control, body) in SystemAPI
                         .Query<RefRW<LocalTransform>, RefRW<PlaytestCharacter>, RefRW<PlaytestCharacterControl>, RefRO<KinematicCharacterBody>>())
            {
                ref PlaytestCharacter c = ref character.ValueRW;

                // rotation from look input (roll is not part of the simulation, as in the client)
                PlaytestCharacterUtilities.ComputeFinalRotationsFromRotationDelta(
                    ref c.ViewPitchDegrees, ref c.CharacterYDegrees, math.up(), control.ValueRO.LookYawPitchDegreesDelta,
                    0f, c.MinViewAngle, c.MaxViewAngle, out quaternion characterRotation, out c.ViewLocalRotation);
                localTransform.ValueRW.Rotation = characterRotation;
                control.ValueRW.LookYawPitchDegreesDelta = float2.zero;

                // view roll from lateral speed (the client's FirstPersonCharacterPresentationOnlySystem)
                float3 characterRight = MathUtilities.GetRightFromRotation(characterRotation);
                float characterMaxSpeed = body.ValueRO.IsGrounded ? c.GroundMaxSpeed : c.AirMaxSpeed;
                float3 lateralVelocity = math.projectsafe(body.ValueRO.RelativeVelocity, characterRight);
                float lateralRatio = characterMaxSpeed > 0f ? math.clamp(math.length(lateralVelocity) / characterMaxSpeed, 0f, 1f) : 0f;
                bool velocityIsRight = math.dot(body.ValueRO.RelativeVelocity, characterRight) > 0f;
                float targetTilt = math.lerp(0f, c.ViewRollAmount, lateralRatio);
                targetTilt = velocityIsRight ? -targetTilt : targetTilt;
                c.ViewRollDegrees = math.lerp(c.ViewRollDegrees, targetTilt, MathUtilities.GetSharpnessInterpolant(c.ViewRollSharpness, deltaTime));
                c.ViewLocalRotation = PlaytestCharacterUtilities.CalculateLocalViewRotation(c.ViewPitchDegrees, c.ViewRollDegrees);
            }
        }
    }
}
