using Unity.CharacterController;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HSE.Playtest
{
    /// <summary>
    /// Everything the play-mode walk test takes from the metaverse client: the values only the client's developers can
    /// set (character, camera, render pipeline, post-processing, the game room's defaults). It is read-only for mod
    /// creators - the package ships it - and is written in the client project only, by HSE/Playtest/Sync from client,
    /// from CharacterFPS.prefab, GameRoom.unity, the "High Fidelity" URP asset and the URP default volume profile.
    /// </summary>
    public class PlaytestSettings : ScriptableObject
    {
        public const string Folder = "Packages/com.hseverse.mods/Settings/Playtest";
        public const string AssetPath = Folder + "/PlaytestSettings.asset";

        [Header("Source")]
        [Tooltip("When and from what the values were taken.")]
        public string SyncedFrom = "";

        [Header("Character body (CharacterFPS.prefab, FirstPersonCharacterAuthoring)")]
        public AuthoringKinematicCharacterProperties CharacterProperties = AuthoringKinematicCharacterProperties.GetDefault();

        [Header("Character movement")]
        public float GroundMaxSpeed = 5f;
        public float GroundedMovementSharpness = 10f;
        public float AirAcceleration = 50f;
        public float AirMaxSpeed = 10f;
        public float AirDrag = 0f;
        public float JumpSpeed = 10f;
        public float3 Gravity = new float3(0f, -30f, 0f);
        public bool PreventAirAccelerationAgainstUngroundedHits = true;
        public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling = BasicStepAndSlopeHandlingParameters.GetDefault();

        [Header("View")]
        public float MinViewAngle = -90f;
        public float MaxViewAngle = 90f;
        public float ViewRollAmount = 0f;
        public float ViewRollSharpness = 0f;
        [Tooltip("The camera point relative to the character root (CameraTarget).")]
        public float3 ViewLocalPosition = new float3(0f, 1.44f, 0f);

        [Header("Collider (CharacterFPS.prefab, PhysicsShapeAuthoring)")]
        public float3 CapsuleVertex0 = new float3(0f, 1.72f, 0f);
        public float3 CapsuleVertex1 = new float3(0f, 0.28f, 0f);
        public float CapsuleRadius = 0.28f;
        public uint BelongsTo = 1u << 1;
        public uint CollidesWith = 0b111;
        public int GroupIndex = 0;
        public float Friction = 0.5f;
        public Unity.Physics.Material.CombinePolicy FrictionCombine = Unity.Physics.Material.CombinePolicy.GeometricMean;
        public float Restitution = 0f;
        public Unity.Physics.Material.CombinePolicy RestitutionCombine = Unity.Physics.Material.CombinePolicy.Maximum;
        public Unity.Physics.CollisionResponsePolicy CollisionResponse = Unity.Physics.CollisionResponsePolicy.Collide;
        public byte CustomMaterialTags = 0;

        [Header("Input (PlayersAction.inputactions + FirstPersonPlayerInputsSystem)")]
        [Tooltip("Degrees per unit of the legacy \"Mouse X/Y\" axis. The legacy axis already carries the Input Manager's 0.1, which " +
                 "is the client's ScaleVector2(0.1) on <Mouse>/delta; this is the client's PcLookSensitivity on top.")]
        public float LookSensitivity = 0.15f;

        [Header("Simulation (NetcodeConfig.asset)")]
        [Tooltip("Character physics steps per second: the client's SimulationTickRate / PredictedFixedStepSimulationTickRatio. " +
                 "The test's local world steps its fixed-step group at this rate instead of Unity's default 60.")]
        public float SimulationTickRate = 30f;

        [Header("Camera (GameRoom.unity)")]
        public CameraClearFlags ClearFlags = CameraClearFlags.Skybox;
        public Color BackgroundColor = Color.black;
        public float FieldOfView = 60f;
        public float NearClipPlane = 0.3f;
        public float FarClipPlane = 1000f;
        public bool AllowHDR = true;
        public bool AllowMSAA = true;
        public bool RenderPostProcessing = true;
        public AntialiasingMode Antialiasing = AntialiasingMode.None;
        public AntialiasingQuality AntialiasingQuality = AntialiasingQuality.High;
        public bool Dithering = false;
        public bool StopNaN = false;

        [Header("Rendering (the client's \"High Fidelity\" quality level)")]
        public RenderPipelineAsset Pipeline;
        [Tooltip("The client's URP default volume profile: rooms cannot set post-processing, so this is all a room shows.")]
        public VolumeProfile DefaultVolumeProfile;

        [Header("Room defaults (GameRoom.unity render settings, used when a room sets no skybox/fog/lighting)")]
        public Material Skybox;
        public bool Fog = false;
        public FogMode FogMode = FogMode.ExponentialSquared;
        public Color FogColor = Color.gray;
        public float FogDensity = 0.001f;
        public float FogStartDistance = 0f;
        public float FogEndDistance = 300f;
        public AmbientMode AmbientMode = AmbientMode.Skybox;
        public Color AmbientSkyColor = new Color(0.212f, 0.227f, 0.259f, 1f);
        public Color AmbientEquatorColor = new Color(0.114f, 0.125f, 0.133f, 1f);
        public Color AmbientGroundColor = new Color(0.047f, 0.043f, 0.035f, 1f);
        public float AmbientIntensity = 1f;
        public float ReflectionIntensity = 1f;
    }
}
