using HSE.Core.Components;
using Unity.CharacterController;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Scenes;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HSE.Playtest
{
    /// <summary>
    /// Walk test for mod rooms: pressing Play in ContentScene spawns the metaverse client's first-person character at
    /// 0,0,0 inside the room, with the client's movement, camera, render pipeline and post-processing, so a creator can
    /// check how a player will move through the room. Nothing is added to the scene: the character, its systems and
    /// the camera are created at run time and are gone when Play stops; the only project setting touched (the quality
    /// level's render pipeline) is restored on stop, and again on the next editor load if the editor died mid-test.
    ///
    /// Only the values a room can set come from the room (skybox, fog and ambient lighting, applied the way the client
    /// applies them); everything else is the client's, from <see cref="PlaytestSettings"/>. Scene objects that never
    /// reach the client - cameras, audio listeners and post-processing volumes outside the room's baked content - are
    /// switched off for the test.
    ///
    /// Controls, as in the client on desktop: WASD / arrows move, the mouse looks, Space jumps; R puts the character back
    /// at 0,0,0; Esc frees the cursor and a click in the Game view captures it again. Shift does nothing - in the
    /// client's first-person character it only changes the run animation.
    /// </summary>
    [AddComponentMenu("")]
    public class PlaytestDriver : MonoBehaviour
    {
        public const string ContentSceneName = "ContentScene";
        static readonly float3 SpawnPosition = float3.zero;
        const float SceneWaitWarnSeconds = 15f;
        const float PhysicsCheckSeconds = 2f;

        static string PipelineMarkerKey => "HSE.Playtest.RestorePipeline." + Application.dataPath;

        PlaytestSettings _settings;
        World _world;
        Entity _character;
        BlobAssetReference<Unity.Physics.Collider> _collider;
        Camera _camera;
        EntityQuery _skyboxQuery, _fogQuery, _lightingQuery;
        bool _queriesCreated;
        bool _roomSettingsApplied;
        bool _physicsChecked;
        float _startTime, _spawnTime;
        string _status;

        bool _pipelineSwapped;
        RenderPipelineAsset _previousPipeline;
        VolumeProfile _previousGlobalDefaultProfile;
        bool _globalDefaultProfileCaptured;

        // ------------------------------------------------------------------------------------------------ start-up

        /// <summary>Called by <see cref="PlaytestPipelineGuard"/> when the editor enters Play.</summary>
        internal static void Boot()
        {
            // only when Play starts in ContentScene: the client's own flow starts elsewhere and loads it later
            if (SceneManager.GetActiveScene().name != ContentSceneName)
                return;
            if (FindAnyObjectByType<PlaytestDriver>() != null)
                return;
            var settings = AssetDatabase.LoadAssetAtPath<PlaytestSettings>(PlaytestSettings.AssetPath);
            if (settings == null)
            {
                Debug.LogWarning("[HSE Playtest] " + PlaytestSettings.AssetPath + " is missing - the walk test is off.");
                return;
            }
            var go = new GameObject("[HSE Playtest]");
            DontDestroyOnLoad(go);
            go.AddComponent<PlaytestDriver>()._settings = settings;
        }

        void Start()
        {
            _startTime = Time.realtimeSinceStartup;
            _status = "Waiting for the room's subscene to load...";
            SwitchOffSceneOnlyObjects();
            SetUpRendering();
            SetUpCamera();
            ApplyRoomDefaults();
            SetCursorLocked(true);
        }

        /// <summary>Cameras, audio listeners and volumes in the scene are not part of the room the client downloads.</summary>
        void SwitchOffSceneOnlyObjects()
        {
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                cam.enabled = false;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                listener.enabled = false;
            foreach (var volume in FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                volume.enabled = false;
        }

        void SetUpRendering()
        {
            if (_settings.Pipeline == null)
                return;
            _previousPipeline = QualitySettings.renderPipeline;
            // written before the swap: if the editor dies mid-test, PlaytestPipelineGuard restores from it on next load
            EditorPrefs.SetString(PipelineMarkerKey, QualitySettings.GetQualityLevel() + "|" + AssetGuid(_previousPipeline));
            QualitySettings.renderPipeline = _settings.Pipeline;
            _pipelineSwapped = true;
        }

        void SetUpCamera()
        {
            var go = new GameObject("Playtest Camera");
            go.transform.SetParent(transform, false);
            go.tag = "MainCamera";
            _camera = go.AddComponent<Camera>();
            _camera.clearFlags = _settings.ClearFlags;
            _camera.backgroundColor = _settings.BackgroundColor;
            _camera.fieldOfView = _settings.FieldOfView;
            _camera.nearClipPlane = _settings.NearClipPlane;
            _camera.farClipPlane = _settings.FarClipPlane;
            _camera.allowHDR = _settings.AllowHDR;
            _camera.allowMSAA = _settings.AllowMSAA;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = _settings.RenderPostProcessing;
            data.antialiasing = _settings.Antialiasing;
            data.antialiasingQuality = _settings.AntialiasingQuality;
            data.dithering = _settings.Dithering;
            data.stopNaN = _settings.StopNaN;
            go.AddComponent<AudioListener>();
            _camera.transform.SetPositionAndRotation((Vector3)(SpawnPosition + _settings.ViewLocalPosition), Quaternion.identity);
        }

        /// <summary>What the client's game room shows when a room sets no skybox / fog / lighting of its own.</summary>
        void ApplyRoomDefaults()
        {
            RenderSettings.skybox = _settings.Skybox;
            RenderSettings.fog = _settings.Fog;
            RenderSettings.fogMode = _settings.FogMode;
            RenderSettings.fogColor = _settings.FogColor;
            RenderSettings.fogDensity = _settings.FogDensity;
            RenderSettings.fogStartDistance = _settings.FogStartDistance;
            RenderSettings.fogEndDistance = _settings.FogEndDistance;
            RenderSettings.ambientMode = _settings.AmbientMode;
            RenderSettings.ambientSkyColor = _settings.AmbientSkyColor;
            RenderSettings.ambientEquatorColor = _settings.AmbientEquatorColor;
            RenderSettings.ambientGroundColor = _settings.AmbientGroundColor;
            RenderSettings.ambientIntensity = _settings.AmbientIntensity;
            RenderSettings.reflectionIntensity = _settings.ReflectionIntensity;
            if (_settings.AmbientMode == AmbientMode.Skybox)
                DynamicGI.UpdateEnvironment(); // the game room's ambient comes from its skybox, not from this scene's lighting data
        }

        // ------------------------------------------------------------------------------------------------ per frame

        void Update()
        {
            if (_world != null && !_world.IsCreated)
            {
                _world = null;
                _character = Entity.Null;
                _queriesCreated = false;
            }
            if (_world == null)
            {
                _world = PickWorld();
                if (_world == null)
                {
                    if (Time.realtimeSinceStartup - _startTime > SceneWaitWarnSeconds)
                        _status = "The room's subscene has not loaded into any world with physics yet.";
                    return;
                }
                Spawn();
            }

            HandleCursor();
            WriteInput();
            if (!_roomSettingsApplied)
                TryApplyRoomSettings();
            if (!_physicsChecked && Time.realtimeSinceStartup - _spawnTime > PhysicsCheckSeconds)
                CheckPhysicsRuns();
        }

        void LateUpdate()
        {
            KeepDefaultVolumeProfile();
            if (_world == null || !_world.IsCreated || _camera == null)
                return;
            var em = _world.EntityManager;
            if (!em.Exists(_character))
                return;
            var ltw = em.GetComponentData<LocalToWorld>(_character);
            var character = em.GetComponentData<PlaytestCharacter>(_character);
            quaternion rotation = ltw.Rotation;
            float3 position = ltw.Position + math.rotate(rotation, _settings.ViewLocalPosition);
            _camera.transform.SetPositionAndRotation(position, math.mul(rotation, character.ViewLocalRotation));
        }

        /// <summary>The client's post-processing base is its URP default volume profile; the pipeline swap re-initialises
        /// the volume system with this project's own, so it is put back whenever that happens.</summary>
        void KeepDefaultVolumeProfile()
        {
            var profile = _settings != null ? _settings.DefaultVolumeProfile : null;
            var vm = VolumeManager.instance;
            if (profile == null || !vm.isInitialized)
                return;
            if (!_globalDefaultProfileCaptured)
            {
                _previousGlobalDefaultProfile = vm.globalDefaultProfile;
                _globalDefaultProfileCaptured = true;
            }
            if (vm.globalDefaultProfile != profile)
                vm.SetGlobalDefaultProfile(profile);
        }

        // ------------------------------------------------------------------------------------------------ world + character

        /// <summary>The world the room's subscenes are loaded into: a client world when netcode made one, else the
        /// default one. Server and thin-client worlds never render, so they are skipped.</summary>
        static World PickWorld()
        {
            var subScenes = FindObjectsByType<SubScene>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            World best = null;
            foreach (var world in World.All)
            {
                if (!world.IsCreated)
                    continue;
                var flags = world.Flags;
                bool isGame = (flags & WorldFlags.Game) == WorldFlags.Game;
                bool isClient = (flags & WorldFlags.GameClient) == WorldFlags.GameClient;
                bool isThin = (flags & WorldFlags.GameThinClient) == WorldFlags.GameThinClient;
                if ((!isGame && !isClient) || isThin)
                    continue;
                if (world.GetExistingSystemManaged<AfterPhysicsSystemGroup>() == null)
                    continue;
                if (!SubScenesLoaded(world, subScenes))
                    continue;
                if (best == null || isClient)
                    best = world;
            }
            return best;
        }

        static bool SubScenesLoaded(World world, SubScene[] subScenes)
        {
            foreach (var subScene in subScenes)
            {
                if (!subScene.AutoLoadScene)
                    continue;
                var sceneEntity = SceneSystem.GetSceneEntity(world.Unmanaged, subScene.SceneGUID);
                if (sceneEntity == Entity.Null || !SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity))
                    return false;
            }
            return true;
        }

        void Spawn()
        {
            var em = _world.EntityManager;
            var s = _settings;

            _collider = Unity.Physics.CapsuleCollider.Create(
                new CapsuleGeometry { Vertex0 = s.CapsuleVertex0, Vertex1 = s.CapsuleVertex1, Radius = s.CapsuleRadius },
                new CollisionFilter { BelongsTo = s.BelongsTo, CollidesWith = s.CollidesWith, GroupIndex = s.GroupIndex },
                new Unity.Physics.Material
                {
                    Friction = s.Friction,
                    FrictionCombinePolicy = s.FrictionCombine,
                    Restitution = s.Restitution,
                    RestitutionCombinePolicy = s.RestitutionCombine,
                    CollisionResponse = s.CollisionResponse,
                    CustomTags = s.CustomMaterialTags,
                });

            _character = em.CreateEntity();
            em.SetName(_character, "Playtest Character");
            em.AddComponentData(_character, LocalTransform.FromPosition(SpawnPosition));
            em.AddComponentData(_character, new LocalToWorld { Value = float4x4.Translate(SpawnPosition) });
            em.AddComponentData(_character, new PhysicsCollider { Value = _collider });
            em.AddSharedComponent(_character, new PhysicsWorldIndex { Value = 0 });
            KinematicCharacterUtilities.CreateCharacter(em, _character, s.CharacterProperties);
            em.AddComponentData(_character, new PlaytestCharacter
            {
                GroundMaxSpeed = s.GroundMaxSpeed,
                GroundedMovementSharpness = s.GroundedMovementSharpness,
                AirAcceleration = s.AirAcceleration,
                AirMaxSpeed = s.AirMaxSpeed,
                AirDrag = s.AirDrag,
                JumpSpeed = s.JumpSpeed,
                Gravity = s.Gravity,
                PreventAirAccelerationAgainstUngroundedHits = s.PreventAirAccelerationAgainstUngroundedHits,
                StepAndSlopeHandling = s.StepAndSlopeHandling,
                MinViewAngle = s.MinViewAngle,
                MaxViewAngle = s.MaxViewAngle,
                ViewRollAmount = s.ViewRollAmount,
                ViewRollSharpness = s.ViewRollSharpness,
                ViewLocalRotation = quaternion.identity,
            });
            em.AddComponentData(_character, new PlaytestCharacterControl());

            var physicsGroup = _world.GetExistingSystemManaged<KinematicCharacterPhysicsUpdateGroup>();
            if (physicsGroup == null)
            {
                physicsGroup = _world.CreateSystemManaged<KinematicCharacterPhysicsUpdateGroup>();
                _world.GetExistingSystemManaged<AfterPhysicsSystemGroup>().AddSystemToUpdateList(physicsGroup);
            }
            physicsGroup.AddSystemToUpdateList(_world.CreateSystem<PlaytestCharacterPhysicsUpdateSystem>());
            _world.GetExistingSystemManaged<SimulationSystemGroup>()
                .AddSystemToUpdateList(_world.CreateSystem<PlaytestCharacterVariableUpdateSystem>());

            // The client steps the character at its netcode tick rate (30), not Unity's default 60. A netcode client
            // world already runs physics at that rate in its predicted group; a local world steps FixedStep.
            var fixedStep = _world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            bool isClient = (_world.Flags & WorldFlags.GameClient) == WorldFlags.GameClient;
            if (fixedStep != null && !isClient && s.SimulationTickRate > 0f)
                fixedStep.Timestep = 1f / s.SimulationTickRate;

            _spawnTime = Time.realtimeSinceStartup;
            _status = null;
            Debug.Log("[HSE Playtest] character spawned at 0,0,0 in world '" + _world.Name + "' (" + s.SyncedFrom + ")");
        }

        void Respawn()
        {
            var em = _world.EntityManager;
            var transformData = em.GetComponentData<LocalTransform>(_character);
            transformData.Position = SpawnPosition;
            em.SetComponentData(_character, transformData);
            var body = em.GetComponentData<KinematicCharacterBody>(_character);
            body.RelativeVelocity = float3.zero;
            body.ParentEntity = Entity.Null;
            body.IsGrounded = false;
            em.SetComponentData(_character, body);
            if (em.HasComponent<CharacterInterpolation>(_character))
            {
                var interpolation = em.GetComponentData<CharacterInterpolation>(_character);
                interpolation.SkipNextInterpolation();
                em.SetComponentData(_character, interpolation);
            }
        }

        /// <summary>A world where the physics step never runs (e.g. netcode physics waiting for a connection) would
        /// leave the character frozen; say so instead of failing silently.</summary>
        void CheckPhysicsRuns()
        {
            _physicsChecked = true;
            var em = _world.EntityManager;
            em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            if (query.IsEmpty || query.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.NumDynamicBodies == 0)
            {
                _status = "Physics is not stepping in world '" + _world.Name + "' - the character cannot move.";
                Debug.LogWarning("[HSE Playtest] " + _status);
            }
        }

        // ------------------------------------------------------------------------------------------------ input

        static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        static void HandleCursor()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                SetCursorLocked(false);
            else if (!CursorLocked && Input.GetMouseButtonDown(0))
                SetCursorLocked(true);
        }

        /// <summary>The client's desktop bindings: WASD and arrows (a digital 2D composite, normalised on diagonals),
        /// the mouse delta scaled by 0.1 and then by the look sensitivity, Space for a jump on the next fixed step.</summary>
        void WriteInput()
        {
            var em = _world.EntityManager;
            if (!em.Exists(_character))
                return;
            var control = em.GetComponentData<PlaytestCharacterControl>(_character);

            float2 move = float2.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
            if (math.lengthsq(move) > 1f)
                move = math.normalize(move);
            control.MoveInput = move;

            if (Input.GetKeyDown(KeyCode.Space))
                control.JumpRequested = true;

            if (CursorLocked)
                control.LookYawPitchDegreesDelta +=
                    new float2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * _settings.LookSensitivity;

            em.SetComponentData(_character, control);

            if (Input.GetKeyDown(KeyCode.R))
                Respawn();
        }

        // ------------------------------------------------------------------------------------------------ room settings

        /// <summary>The client's TransferSceneSettingsSystem: once a room carries all three of skybox, fog and lighting
        /// settings they are applied, once; with any of them missing the game room's defaults stay.</summary>
        void TryApplyRoomSettings()
        {
            var em = _world.EntityManager;
            if (!_queriesCreated)
            {
                _skyboxQuery = em.CreateEntityQuery(ComponentType.ReadOnly<SkyboxMaterialComponent>());
                _fogQuery = em.CreateEntityQuery(ComponentType.ReadOnly<FogSettingsComponent>());
                _lightingQuery = em.CreateEntityQuery(ComponentType.ReadOnly<SceneLightingSettingsComponent>());
                _queriesCreated = true;
            }
            if (_skyboxQuery.IsEmpty || _fogQuery.IsEmpty || _lightingQuery.IsEmpty)
                return;

            using (var skyboxes = _skyboxQuery.ToComponentDataArray<SkyboxMaterialComponent>(Allocator.Temp))
                RenderSettings.skybox = skyboxes[0].Material;

            // optional mode components (rooms baked before them: exponential fog, three-colour ambient)
            var fogMode = FogMode.Exponential;
            var ambientMode = AmbientMode.Trilight;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<FogModeComponent>()))
                if (!q.IsEmpty)
                {
                    int m = q.GetSingleton<FogModeComponent>().Mode;
                    if (m >= (int)FogMode.Linear && m <= (int)FogMode.ExponentialSquared) fogMode = (FogMode)m;
                }
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<AmbientModeComponent>()))
                if (!q.IsEmpty)
                {
                    var m = (AmbientMode)q.GetSingleton<AmbientModeComponent>().Mode;
                    if (m == AmbientMode.Skybox || m == AmbientMode.Flat) ambientMode = m;
                }

            using (var fogs = _fogQuery.ToComponentDataArray<FogSettingsComponent>(Allocator.Temp))
            {
                var fog = fogs[0];
                RenderSettings.fog = fog.EnableFog;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fog.FogColor;
                RenderSettings.fogDensity = fog.FogDensity;
                RenderSettings.fogStartDistance = fog.FogStartDistance;
                RenderSettings.fogEndDistance = fog.FogEndDistance;
            }

            using (var lightings = _lightingQuery.ToComponentDataArray<SceneLightingSettingsComponent>(Allocator.Temp))
            {
                var lighting = lightings[0];
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = lighting.AmbientSkyColor;
                RenderSettings.ambientEquatorColor = lighting.AmbientEquatorColor;
                RenderSettings.ambientGroundColor = lighting.AmbientGroundColor;
                RenderSettings.ambientIntensity = lighting.AmbientIntensity;
                if (ambientMode == AmbientMode.Skybox) DynamicGI.UpdateEnvironment();
            }
            _roomSettingsApplied = true;
        }

        // ------------------------------------------------------------------------------------------------ HUD

        GUIStyle _hudStyle;

        void OnGUI()
        {
            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 12,
                    wordWrap = true,
                    padding = new RectOffset(8, 8, 6, 6),
                };
                _hudStyle.normal.textColor = Color.white;
            }
            string text = "HSE playtest - client character\n" +
                          "WASD / arrows move   Mouse look   Space jump   R back to 0,0,0   " +
                          (CursorLocked ? "Esc frees the cursor" : "Click to capture the cursor");
            if (!string.IsNullOrEmpty(_status))
                text += "\n" + _status;
            float height = string.IsNullOrEmpty(_status) ? 44f : 62f;
            GUI.Box(new Rect(10f, Screen.height - height - 10f, 560f, height), text, _hudStyle);
        }

        // ------------------------------------------------------------------------------------------------ teardown

        void OnDestroy()
        {
            SetCursorLocked(false);
            if (_pipelineSwapped)
            {
                QualitySettings.renderPipeline = _previousPipeline;
                EditorPrefs.DeleteKey(PipelineMarkerKey);
                _pipelineSwapped = false;
            }
            if (_globalDefaultProfileCaptured && _previousGlobalDefaultProfile != null && VolumeManager.instance.isInitialized)
                VolumeManager.instance.SetGlobalDefaultProfile(_previousGlobalDefaultProfile);
            if (_world != null && _world.IsCreated && _world.EntityManager.Exists(_character))
                _world.EntityManager.DestroyEntity(_character);
            if (_collider.IsCreated)
                _collider.Dispose();
        }

        /// <summary>Puts the render pipeline back after a test the editor did not end cleanly (called on editor load).</summary>
        public static void RestorePipelineFromMarker()
        {
            if (!EditorPrefs.HasKey(PipelineMarkerKey))
                return;
            var parts = EditorPrefs.GetString(PipelineMarkerKey).Split('|');
            EditorPrefs.DeleteKey(PipelineMarkerKey);
            if (parts.Length < 2 || !int.TryParse(parts[0], out int level))
                return;
            var previous = parts[1].Length > 0
                ? AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(parts[1]))
                : null;
            if (QualitySettings.GetQualityLevel() != level)
                QualitySettings.SetQualityLevel(level, false);
            QualitySettings.renderPipeline = previous;
            Debug.Log("[HSE Playtest] restored the render pipeline left over from an interrupted walk test.");
        }

        static string AssetGuid(Object asset) =>
            asset == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    }

    /// <summary>Starts the walk test when the editor enters Play, and restores the render pipeline if the editor
    /// stopped (crash, domain reload) in the middle of one.</summary>
    [InitializeOnLoad]
    static class PlaytestPipelineGuard
    {
        static PlaytestPipelineGuard()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    PlaytestDriver.RestorePipelineFromMarker();
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                    PlaytestDriver.Boot();
                else if (state == PlayModeStateChange.EnteredEditMode)
                    PlaytestDriver.RestorePipelineFromMarker();
            };
        }
    }
}
