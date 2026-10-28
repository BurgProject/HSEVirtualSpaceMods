using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Unity.CharacterController;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HSE.Playtest.Editor
{
    /// <summary>
    /// Client project only: copies what the walk test needs from the client into the package, so mod creators get the
    /// client's values without the client. Run it whenever the character, the game room's camera / render settings, the
    /// "High Fidelity" URP asset or the default volume profile change. Writes Settings/Playtest in the package:
    /// PlaytestSettings.asset plus copies of the URP asset, its renderer(s), the default volume profile and the game
    /// room's skybox, and a report in %TEMP%/playtest_sync.txt.
    /// </summary>
    public static class PlaytestSync
    {
        const string MenuPath = "HSE/Playtest/Sync from client";
        const string CharacterPrefab = "Assets/Prefabs/Ghosts/CharacterFPS.prefab";
        const string GameRoomScene = "Assets/Scenes/Main/MainGameScene/GameRoom.unity";
        const string InputActions = "Assets/Settings/Inputs/PlayersAction.inputactions";
        const string NetcodeConfig = "Assets/Settings/Netcode/NetcodeConfig.asset";
        const string ClientQualityLevel = "High Fidelity";
        const float ClientPcLookSensitivity = 0.15f;   // FirstPersonPlayerInputsSystem.PcLookSensitivity (a code constant)
        const float LegacyMouseAxisScale = 0.1f;       // the Input Manager's default "Mouse X/Y" sensitivity

        static readonly StringBuilder Report = new StringBuilder();

        [MenuItem(MenuPath, true)]
        static bool CanSync() => File.Exists(CharacterPrefab) && File.Exists(GameRoomScene);

        [MenuItem(MenuPath)]
        public static void Sync()
        {
            Report.Clear();
            try
            {
                EnsureFolder();
                var settings = AssetDatabase.LoadAssetAtPath<PlaytestSettings>(PlaytestSettings.AssetPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<PlaytestSettings>();
                    AssetDatabase.CreateAsset(settings, PlaytestSettings.AssetPath);
                }

                ReadCharacter(settings);
                ReadInput(settings);
                ReadTickRate(settings);
                string roomText = File.ReadAllText(GameRoomScene).Replace("\r\n", "\n");
                ReadCamera(settings, roomText);
                ReadRoomDefaults(settings, roomText);
                var pipelineName = CopyPipeline(settings);
                CopyDefaultVolumeProfile(settings);

                settings.SyncedFrom = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                                      " from CharacterFPS.prefab, GameRoom.unity, " + pipelineName;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                foreach (var path in AssetDatabase.FindAssets("", new[] { PlaytestSettings.Folder }).Select(AssetDatabase.GUIDToAssetPath))
                    WarnAboutClientDependencies(path);
                Log("synced: " + settings.SyncedFrom);
                Debug.Log("[HSE Playtest] " + Report);
            }
            catch (Exception e)
            {
                Log("FAILED: " + e);
                Debug.LogError("[HSE Playtest] sync failed: " + e);
            }
            finally
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "playtest_sync.txt"), Report.ToString());
            }
        }

        static void Log(string line) => Report.AppendLine(line);

        static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(PlaytestSettings.Folder))
                return;
            var parent = Path.GetDirectoryName(PlaytestSettings.Folder)?.Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(PlaytestSettings.Folder));
        }

        // ------------------------------------------------------------------------------------------------ character

        static void ReadCharacter(PlaytestSettings settings)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab);
            var authoring = prefab.GetComponents<MonoBehaviour>()
                .FirstOrDefault(c => c != null && c.GetType().Name == "FirstPersonCharacterAuthoring");
            if (authoring == null)
                throw new Exception("FirstPersonCharacterAuthoring not found on " + CharacterPrefab);
            var so = new SerializedObject(authoring);

            settings.CharacterProperties = (AuthoringKinematicCharacterProperties)ReadStruct(
                so.FindProperty("CharacterProperties"), typeof(AuthoringKinematicCharacterProperties));

            var c = so.FindProperty("Character");
            float F(string name) => c.FindPropertyRelative(name).floatValue;
            settings.GroundMaxSpeed = F("GroundMaxSpeed");
            settings.GroundedMovementSharpness = F("GroundedMovementSharpness");
            settings.AirAcceleration = F("AirAcceleration");
            settings.AirMaxSpeed = F("AirMaxSpeed");
            settings.AirDrag = F("AirDrag");
            settings.JumpSpeed = F("JumpSpeed");
            settings.Gravity = (float3)ReadStruct(c.FindPropertyRelative("Gravity"), typeof(float3));
            settings.PreventAirAccelerationAgainstUngroundedHits = c.FindPropertyRelative("PreventAirAccelerationAgainstUngroundedHits").boolValue;
            settings.StepAndSlopeHandling = (BasicStepAndSlopeHandlingParameters)ReadStruct(
                c.FindPropertyRelative("StepAndSlopeHandling"), typeof(BasicStepAndSlopeHandlingParameters));
            settings.MinViewAngle = F("MinViewAngle");
            settings.MaxViewAngle = F("MaxViewAngle");
            settings.ViewRollAmount = F("ViewRollAmount");
            settings.ViewRollSharpness = F("ViewRollSharpness");

            var view = so.FindProperty("ViewObject").objectReferenceValue as GameObject;
            if (view == null)
                throw new Exception("FirstPersonCharacterAuthoring.ViewObject is not set");
            settings.ViewLocalPosition = prefab.transform.InverseTransformPoint(view.transform.position);

            var shape = prefab.GetComponentsInChildren<PhysicsShapeAuthoring>(true).FirstOrDefault();
            if (shape == null || shape.ShapeType != ShapeType.Capsule)
                throw new Exception("the character's PhysicsShapeAuthoring is missing or not a capsule");
            var capsule = shape.GetCapsuleProperties().ToRuntime();
            var toRoot = prefab.transform.worldToLocalMatrix * shape.transform.localToWorldMatrix;
            settings.CapsuleVertex0 = toRoot.MultiplyPoint3x4(capsule.Vertex0);
            settings.CapsuleVertex1 = toRoot.MultiplyPoint3x4(capsule.Vertex1);
            settings.CapsuleRadius = capsule.Radius;
            settings.BelongsTo = shape.BelongsTo.Value;
            settings.CollidesWith = shape.CollidesWith.Value;
            settings.GroupIndex = 0;
            settings.Friction = shape.Friction.Value;
            settings.FrictionCombine = shape.Friction.CombineMode;
            settings.Restitution = shape.Restitution.Value;
            settings.RestitutionCombine = shape.Restitution.CombineMode;
            settings.CollisionResponse = shape.CollisionResponse;
            settings.CustomMaterialTags = shape.CustomTags.Value;

            Log(string.Format(CultureInfo.InvariantCulture,
                "character: ground {0} m/s sharp {1}, air accel {2} max {3} drag {4}, jump {5}, gravity {6}, step {7} (handling {8}), slope {9}, snap {10}, interpolate pos {11} rot {12}",
                settings.GroundMaxSpeed, settings.GroundedMovementSharpness, settings.AirAcceleration, settings.AirMaxSpeed,
                settings.AirDrag, settings.JumpSpeed, settings.Gravity, settings.StepAndSlopeHandling.MaxStepHeight,
                settings.StepAndSlopeHandling.StepHandling, settings.CharacterProperties.MaxGroundedSlopeAngle,
                settings.CharacterProperties.GroundSnappingDistance, settings.CharacterProperties.InterpolatePosition,
                settings.CharacterProperties.InterpolateRotation));
            Log(string.Format(CultureInfo.InvariantCulture,
                "collider: capsule {0} .. {1} r {2}, belongs 0x{3:X} collides 0x{4:X}, friction {5} ({6}), restitution {7} ({8}), response {9}; view at {10}",
                settings.CapsuleVertex0, settings.CapsuleVertex1, settings.CapsuleRadius, settings.BelongsTo, settings.CollidesWith,
                settings.Friction, settings.FrictionCombine, settings.Restitution, settings.RestitutionCombine,
                settings.CollisionResponse, settings.ViewLocalPosition));
        }

        /// <summary>Copies a serialized struct field by field (floats, ints, bools, bytes, enums, nested structs), so a
        /// struct the client serializes can be read without referencing the client's code.</summary>
        static object ReadStruct(SerializedProperty property, Type type)
        {
            if (property == null)
                throw new Exception("missing serialized field of type " + type.Name);
            object boxed = Activator.CreateInstance(type);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.IsNotSerialized)
                    continue;
                var child = property.FindPropertyRelative(field.Name);
                if (child == null)
                    continue;
                var t = field.FieldType;
                object value;
                if (t == typeof(float)) value = child.floatValue;
                else if (t == typeof(bool)) value = child.boolValue;
                else if (t == typeof(int)) value = child.intValue;
                else if (t == typeof(uint)) value = (uint)child.longValue;
                else if (t == typeof(byte)) value = (byte)child.intValue;
                else if (t.IsEnum) value = Enum.ToObject(t, child.intValue);
                else if (t.IsValueType && !t.IsPrimitive) value = ReadStruct(child, t);
                else continue;
                field.SetValue(boxed, value);
            }
            return boxed;
        }

        static void ReadInput(PlaytestSettings settings)
        {
            // MouseLook: <Mouse>/delta with ScaleVector2(x=0.1,y=0.1); the legacy axis the test reads already carries 0.1
            float scale = LegacyMouseAxisScale;
            if (File.Exists(InputActions))
            {
                var m = Regex.Match(File.ReadAllText(InputActions),
                    "\"name\":\\s*\"MouseLook\"[^}]*?\"processors\":\\s*\"ScaleVector2\\(x=([0-9.]+)", RegexOptions.Singleline);
                if (m.Success)
                    scale = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            settings.LookSensitivity = ClientPcLookSensitivity * scale / LegacyMouseAxisScale;
            Log(string.Format(CultureInfo.InvariantCulture, "look: delta x {0} x {1} -> {2} per legacy axis unit",
                scale, ClientPcLookSensitivity, settings.LookSensitivity));
        }

        /// <summary>The client steps the character in its predicted fixed-step group: SimulationTickRate divided by
        /// PredictedFixedStepSimulationTickRatio steps per second.</summary>
        static void ReadTickRate(PlaytestSettings settings)
        {
            if (!File.Exists(NetcodeConfig))
            {
                Log("tick rate: " + NetcodeConfig + " missing, kept " + settings.SimulationTickRate);
                return;
            }
            string text = File.ReadAllText(NetcodeConfig);
            var rate = Regex.Match(text, @"\n\s*SimulationTickRate:\s*([0-9]+)");
            var ratio = Regex.Match(text, @"\n\s*PredictedFixedStepSimulationTickRatio:\s*([0-9]+)");
            if (!rate.Success)
            {
                Log("tick rate: not found in " + NetcodeConfig + ", kept " + settings.SimulationTickRate);
                return;
            }
            float ticks = float.Parse(rate.Groups[1].Value, CultureInfo.InvariantCulture);
            float divisor = ratio.Success ? Mathf.Max(1f, float.Parse(ratio.Groups[1].Value, CultureInfo.InvariantCulture)) : 1f;
            settings.SimulationTickRate = ticks / divisor;
            Log(string.Format(CultureInfo.InvariantCulture, "tick rate: {0} / {1} -> {2} steps per second", ticks, divisor,
                settings.SimulationTickRate));
        }

        // ------------------------------------------------------------------------------------------------ game room

        static string Block(string sceneText, string header)
        {
            int start = sceneText.IndexOf(header, StringComparison.Ordinal);
            if (start < 0) return null;
            int end = sceneText.IndexOf("\n--- ", start + header.Length, StringComparison.Ordinal);
            return end < 0 ? sceneText.Substring(start) : sceneText.Substring(start, end - start);
        }

        static string Field(string block, string name)
        {
            var m = Regex.Match(block, "\\n\\s*" + Regex.Escape(name) + ": ([^\\n]*)");
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        static float FieldFloat(string block, string name, float fallback)
        {
            var v = Field(block, name);
            return v != null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;
        }

        static Color FieldColor(string block, string name, Color fallback)
        {
            var v = Field(block, name);
            if (v == null) return fallback;
            var m = Regex.Match(v, "r: ([-0-9.eE]+), g: ([-0-9.eE]+), b: ([-0-9.eE]+), a: ([-0-9.eE]+)");
            if (!m.Success) return fallback;
            float P(int i) => float.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
            return new Color(P(1), P(2), P(3), P(4));
        }

        static void ReadCamera(PlaytestSettings settings, string roomText)
        {
            var camMatch = Regex.Match(roomText, "--- !u!20 &\\d+\\nCamera:\\n.*?(?=\\n--- )", RegexOptions.Singleline);
            if (!camMatch.Success)
                throw new Exception("no Camera in " + GameRoomScene);
            string cam = "\n" + camMatch.Value;
            settings.ClearFlags = (CameraClearFlags)(int)FieldFloat(cam, "m_ClearFlags", 1);
            settings.BackgroundColor = FieldColor(cam, "m_BackGroundColor", Color.black);
            settings.NearClipPlane = FieldFloat(cam, "near clip plane", 0.3f);
            settings.FarClipPlane = FieldFloat(cam, "far clip plane", 1000f);
            settings.FieldOfView = FieldFloat(cam, "field of view", 60f);
            settings.AllowHDR = FieldFloat(cam, "m_HDR", 1) != 0;
            settings.AllowMSAA = FieldFloat(cam, "m_AllowMSAA", 1) != 0;

            // the URP camera data on the same GameObject
            var go = Regex.Match(cam, "m_GameObject: \\{fileID: (\\d+)\\}").Groups[1].Value;
            var urp = Regex.Matches(roomText, "--- !u!114 &\\d+\\nMonoBehaviour:\\n.*?(?=\\n--- )", RegexOptions.Singleline)
                .Cast<Match>().Select(x => "\n" + x.Value)
                .FirstOrDefault(b => b.Contains("m_GameObject: {fileID: " + go + "}") && b.Contains("m_RenderPostProcessing"));
            if (urp != null)
            {
                settings.RenderPostProcessing = FieldFloat(urp, "m_RenderPostProcessing", 1) != 0;
                settings.Antialiasing = (AntialiasingMode)(int)FieldFloat(urp, "m_Antialiasing", 0);
                settings.AntialiasingQuality = (AntialiasingQuality)(int)FieldFloat(urp, "m_AntialiasingQuality", 2);
                settings.Dithering = FieldFloat(urp, "m_Dithering", 0) != 0;
                settings.StopNaN = FieldFloat(urp, "m_StopNaN", 0) != 0;
            }
            else Log("WARNING: no URP camera data on the game room camera - URP defaults kept");

            Log(string.Format(CultureInfo.InvariantCulture,
                "camera: fov {0}, clip {1}..{2}, clear {3}, hdr {4}, msaa {5}, post-processing {6}, aa {7} ({8}), dithering {9}",
                settings.FieldOfView, settings.NearClipPlane, settings.FarClipPlane, settings.ClearFlags, settings.AllowHDR,
                settings.AllowMSAA, settings.RenderPostProcessing, settings.Antialiasing, settings.AntialiasingQuality, settings.Dithering));
        }

        static void ReadRoomDefaults(PlaytestSettings settings, string roomText)
        {
            string rs = Block(roomText, "\nRenderSettings:");
            if (rs == null)
                throw new Exception("no RenderSettings in " + GameRoomScene);
            settings.Fog = FieldFloat(rs, "m_Fog", 0) != 0;
            settings.FogColor = FieldColor(rs, "m_FogColor", Color.gray);
            settings.FogMode = (FogMode)(int)FieldFloat(rs, "m_FogMode", 3);
            settings.FogDensity = FieldFloat(rs, "m_FogDensity", 0.01f);
            settings.FogStartDistance = FieldFloat(rs, "m_LinearFogStart", 0f);
            settings.FogEndDistance = FieldFloat(rs, "m_LinearFogEnd", 300f);
            settings.AmbientSkyColor = FieldColor(rs, "m_AmbientSkyColor", settings.AmbientSkyColor);
            settings.AmbientEquatorColor = FieldColor(rs, "m_AmbientEquatorColor", settings.AmbientEquatorColor);
            settings.AmbientGroundColor = FieldColor(rs, "m_AmbientGroundColor", settings.AmbientGroundColor);
            settings.AmbientIntensity = FieldFloat(rs, "m_AmbientIntensity", 1f);
            settings.AmbientMode = (AmbientMode)(int)FieldFloat(rs, "m_AmbientMode", 0);
            settings.ReflectionIntensity = FieldFloat(rs, "m_ReflectionIntensity", 1f);

            var sky = Regex.Match(rs, "m_SkyboxMaterial: \\{fileID: (-?\\d+), guid: ([0-9a-f]{32}),\\s+type: \\d+\\}");
            settings.Skybox = sky.Success ? CopySkybox(long.Parse(sky.Groups[1].Value, CultureInfo.InvariantCulture), sky.Groups[2].Value) : null;

            Log(string.Format(CultureInfo.InvariantCulture,
                "room defaults: skybox {0}, fog {1} ({2}, density {3}), ambient {4} x{5}, reflections x{6}",
                settings.Skybox != null ? AssetDatabase.GetAssetPath(settings.Skybox) : "none", settings.Fog, settings.FogMode,
                settings.FogDensity, settings.AmbientMode, settings.AmbientIntensity, settings.ReflectionIntensity));
        }

        /// <summary>The game room's skybox material, copied (with the asset that holds it) into the package.</summary>
        static UnityEngine.Material CopySkybox(long localId, string guid)
        {
            string source = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(source))
            {
                Log("WARNING: the game room's skybox asset " + guid + " does not exist");
                return null;
            }
            string target = PlaytestSettings.Folder + "/PlaytestSkybox" + Path.GetExtension(source);
            CopyAsset(source, target);
            UnityEngine.Material material = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(target))
            {
                if (!(o is UnityEngine.Material m)) continue;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out _, out long id) && id == localId) { material = m; break; }
                if (material == null) material = m;
            }
            return material;
        }

        // ------------------------------------------------------------------------------------------------ rendering

        static string CopyPipeline(PlaytestSettings settings)
        {
            int level = Array.IndexOf(QualitySettings.names, ClientQualityLevel);
            if (level < 0)
            {
                Log("WARNING: no \"" + ClientQualityLevel + "\" quality level - using level 0");
                level = 0;
            }
            var source = QualitySettings.GetRenderPipelineAssetAt(level) ?? GraphicsSettings.defaultRenderPipeline;
            if (!(source is UniversalRenderPipelineAsset))
                throw new Exception("the \"" + ClientQualityLevel + "\" quality level has no URP asset");

            string pipelinePath = PlaytestSettings.Folder + "/PlaytestURP.asset";
            CopyAsset(AssetDatabase.GetAssetPath(source), pipelinePath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            var so = new SerializedObject(pipeline);

            var renderers = so.FindProperty("m_RendererDataList");
            for (int i = 0; i < renderers.arraySize; i++)
            {
                var element = renderers.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == null) continue;
                string rendererPath = PlaytestSettings.Folder + "/PlaytestURP_Renderer" + (i == 0 ? "" : i.ToString()) + ".asset";
                CopyAsset(AssetDatabase.GetAssetPath(element.objectReferenceValue), rendererPath);
                StripUnusedFeatures(rendererPath);
                element.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            }

            var qualityProfile = so.FindProperty("m_VolumeProfile");
            if (qualityProfile != null && qualityProfile.objectReferenceValue != null)
            {
                string profilePath = PlaytestSettings.Folder + "/PlaytestQualityVolumeProfile.asset";
                CopyAsset(AssetDatabase.GetAssetPath(qualityProfile.objectReferenceValue), profilePath);
                qualityProfile.objectReferenceValue = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            settings.Pipeline = pipeline;

            Log("pipeline: " + AssetDatabase.GetAssetPath(source) + " (quality \"" + QualitySettings.names[level] + "\") -> " + pipelinePath);
            return Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(source));
        }

        /// <summary>Renderer features left in the file but not in the renderer's list are dropped. The client's renderer
        /// carries unused outline features, two of them with no script at all, which the asset API cannot even load -
        /// so the copy is cleaned as text: every sub-object that is neither the renderer itself nor listed in
        /// m_RendererFeatures goes. Active non-URP features are reported.</summary>
        static void StripUnusedFeatures(string rendererPath)
        {
            string file = FileUtil.GetPhysicalPath(rendererPath);
            string text = File.ReadAllText(file).Replace("\r\n", "\n");
            var listed = new HashSet<string>();
            var list = Regex.Match(text, @"\n  m_RendererFeatures:\n((?:  - \{fileID: -?\d+\}\n)*)");
            foreach (Match id in Regex.Matches(list.Groups[1].Value, @"fileID: (-?\d+)"))
                listed.Add(id.Groups[1].Value);
            var documents = Regex.Split(text, "(?=^--- !u!)", RegexOptions.Multiline);
            var kept = new StringBuilder();
            foreach (var document in documents)
            {
                var header = Regex.Match(document, @"^--- !u!\d+ &(-?\d+)");
                if (header.Success && header.Groups[1].Value != "11400000" && !listed.Contains(header.Groups[1].Value))
                {
                    var name = Regex.Match(document, @"\n  m_Name: ([^\n]*)");
                    Log("  renderer: dropped unused feature " + (name.Success ? name.Groups[1].Value : header.Groups[1].Value));
                    continue;
                }
                kept.Append(document);
            }
            File.WriteAllText(file, kept.ToString());
            AssetDatabase.ImportAsset(rendererPath, ImportAssetOptions.ForceUpdate);

            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            foreach (var feature in data.rendererFeatures)
            {
                if (feature == null)
                {
                    Log("  WARNING: the renderer lists a feature whose script is missing");
                    continue;
                }
                bool builtIn = feature.GetType().Assembly == typeof(UniversalRenderPipelineAsset).Assembly;
                Log("  renderer feature " + feature.name + (feature.isActive ? " (active)" : " (off)") +
                    (builtIn ? "" : " - WARNING: client code, a mod project will show a missing script"));
            }
        }

        static void CopyDefaultVolumeProfile(PlaytestSettings settings)
        {
            VolumeProfile source = null;
            if (GraphicsSettings.TryGetRenderPipelineSettings<URPDefaultVolumeProfileSettings>(out var profileSettings))
                source = profileSettings.volumeProfile;
            if (source == null)
            {
                Log("WARNING: the client has no URP default volume profile");
                settings.DefaultVolumeProfile = null;
                return;
            }
            string target = PlaytestSettings.Folder + "/PlaytestDefaultVolumeProfile.asset";
            CopyAsset(AssetDatabase.GetAssetPath(source), target);
            var copy = AssetDatabase.LoadAssetAtPath<VolumeProfile>(target);
            settings.DefaultVolumeProfile = copy;
            foreach (var component in copy.components.Where(c => c != null))
            {
                var assembly = component.GetType().Assembly;
                bool builtIn = assembly == typeof(UniversalRenderPipelineAsset).Assembly || assembly == typeof(VolumeProfile).Assembly;
                if (!builtIn)
                    Log("  WARNING: volume override " + component.GetType().Name + " is client code");
            }
            Log("default volume profile: " + AssetDatabase.GetAssetPath(source) + " (" + copy.components.Count + " overrides, " +
                copy.components.Count(c => c != null && c.active) + " active) -> " + target);
        }

        // ------------------------------------------------------------------------------------------------ helpers

        static void CopyAsset(string source, string target)
        {
            if (AssetDatabase.LoadMainAssetAtPath(target) != null)
                AssetDatabase.DeleteAsset(target);
            if (!AssetDatabase.CopyAsset(source, target))
                throw new Exception("could not copy " + source + " to " + target);
        }

        /// <summary>Everything a copied asset needs must exist in a mod project too: anything still under Assets/ is reported.</summary>
        static void WarnAboutClientDependencies(string path)
        {
            foreach (var dependency in AssetDatabase.GetDependencies(path, true))
            {
                if (dependency == path || !dependency.StartsWith("Assets/", StringComparison.Ordinal))
                    continue;
                Log("  WARNING: " + path + " still uses client asset " + dependency);
            }
        }
    }
}
