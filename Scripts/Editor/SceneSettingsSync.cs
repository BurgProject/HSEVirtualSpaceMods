#if UNITY_EDITOR && !HSE_BUILD
using System.Collections.Generic;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HSE_MAIN.Editor.Mods
{
    /// <summary>
    /// Keeps the room's scene-settings authorings (<see cref="SkyboxAuthoring"/>, <see cref="FogSettingsAuthoring"/>,
    /// <see cref="SceneLightingSettingsAuthoring"/>) equal to the Lighting window of the scene that contains the room's
    /// SubScene (ContentScene): skybox material, fog (on/off, mode, colour, density, start/end) and environment lighting
    /// (mode, the three colours, intensity). The creator edits lighting the normal Unity way; the values are copied into
    /// the SubScene, which is what gets exported and baked (the parent scene is not exported).
    ///
    /// Runs twice a second and before every save for SubScenes open for editing, and before export for all of them
    /// (opening closed ones). A SubScene with none of the three gets one "Scene Settings" object carrying all three - the
    /// client applies them only together. Never runs in batch mode: a headless build opens its own template scene, whose
    /// lighting must not overwrite what the creator exported.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneSettingsSync
    {
        private const string ObjectName = "Scene Settings";
        private const double Interval = 0.5;
        private static double _next;

        private struct Lighting
        {
            public Material Skybox;
            public bool Fog;
            public FogMode FogMode;
            public Color FogColor;
            public float FogDensity, FogStart, FogEnd;
            public AmbientMode AmbientMode;
            public Color Sky, Equator, Ground;
            public float AmbientIntensity;
        }

        static SceneSettingsSync()
        {
            if (Application.isBatchMode || AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorSceneManager.sceneSaving += (scene, path) => SyncOpen();
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + Interval;
            SyncOpen();
        }

        /// <summary>Every SubScene of the active scene that is open for editing.</summary>
        public static void SyncOpen()
        {
            if(Application.isBatchMode) return;
            var parent = SceneManager.GetActiveScene();
            if (!parent.IsValid() || !parent.isLoaded) return;
            var subs = SubScenesOf(parent);
            if (subs.Count == 0) return;
            var lighting = Read();
            foreach (var sub in subs)
                if (sub.IsLoaded) Apply(sub.EditingScene, lighting);
        }

        /// <summary>Before an export: every SubScene of the content scene, closed ones opened, written and saved.</summary>
        public static void SyncForExport(string contentScenePath)
        {
            if(Application.isBatchMode || string.IsNullOrEmpty(contentScenePath)) return;
            var parent = SceneManager.GetSceneByPath(contentScenePath);
            bool openedParent = false;
            if (!parent.IsValid() || !parent.isLoaded)
            {
                if (!System.IO.File.Exists(contentScenePath)) return;
                parent = EditorSceneManager.OpenScene(contentScenePath, OpenSceneMode.Additive);
                openedParent = true;
            }
            var previousActive = SceneManager.GetActiveScene();
            try
            {
                if (previousActive != parent) SceneManager.SetActiveScene(parent); // RenderSettings are the active scene's
                var lighting = Read();
                foreach (var sub in SubScenesOf(parent))
                {
                    if (sub.IsLoaded)
                    {
                        if (Apply(sub.EditingScene, lighting)) EditorSceneManager.SaveScene(sub.EditingScene);
                        continue;
                    }
                    var path = sub.EditableScenePath;
                    if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) continue;
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    if (Apply(scene, lighting)) EditorSceneManager.SaveScene(scene);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded && SceneManager.GetActiveScene() != previousActive)
                    SceneManager.SetActiveScene(previousActive);
                if (openedParent) EditorSceneManager.CloseScene(parent, true);
            }
        }

        private static List<SubScene> SubScenesOf(Scene scene)
        {
            var list = new List<SubScene>();
            foreach (var root in scene.GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<SubScene>(true));
            return list;
        }

        private static Lighting Read() => new Lighting
        {
            Skybox = RenderSettings.skybox,
            Fog = RenderSettings.fog,
            FogMode = RenderSettings.fogMode,
            FogColor = RenderSettings.fogColor,
            FogDensity = RenderSettings.fogDensity,
            FogStart = RenderSettings.fogStartDistance,
            FogEnd = RenderSettings.fogEndDistance,
            AmbientMode = RenderSettings.ambientMode,
            Sky = RenderSettings.ambientSkyColor,
            Equator = RenderSettings.ambientEquatorColor,
            Ground = RenderSettings.ambientGroundColor,
            AmbientIntensity = RenderSettings.ambientIntensity,
        };

        /// <summary>Write the lighting into the scene's authorings (creating them when there are none); true when
        /// anything changed.</summary>
        private static bool Apply(Scene scene, Lighting l)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;
            var skyboxes = new List<SkyboxAuthoring>();
            var fogs = new List<FogSettingsAuthoring>();
            var lightings = new List<SceneLightingSettingsAuthoring>();
            foreach (var root in scene.GetRootGameObjects())
            {
                skyboxes.AddRange(root.GetComponentsInChildren<SkyboxAuthoring>(true));
                fogs.AddRange(root.GetComponentsInChildren<FogSettingsAuthoring>(true));
                lightings.AddRange(root.GetComponentsInChildren<SceneLightingSettingsAuthoring>(true));
            }
            bool changed = RemoveDuplicates(skyboxes, fogs, lightings);
            if (skyboxes.Count == 0 || fogs.Count == 0 || lightings.Count == 0)
            {
                GameObject host = skyboxes.Count > 0 ? skyboxes[0].gameObject : fogs.Count > 0 ? fogs[0].gameObject
                    : lightings.Count > 0 ? lightings[0].gameObject : null;
                if (!host)
                {
                    host = new GameObject(ObjectName);
                    SceneManager.MoveGameObjectToScene(host, scene);
                    // On the undo stack, so undoing the user's delete of the old object can't leave a second one alive
                    Undo.RegisterCreatedObjectUndo(host, "Create " + ObjectName);
                }
                if (skyboxes.Count == 0) skyboxes.Add(Undo.AddComponent<SkyboxAuthoring>(host));
                if (fogs.Count == 0) fogs.Add(Undo.AddComponent<FogSettingsAuthoring>(host));
                if (lightings.Count == 0) lightings.Add(Undo.AddComponent<SceneLightingSettingsAuthoring>(host));
                changed = true;
            }
            foreach (var a in skyboxes)
            {
                if (a.SkyboxMaterial == l.Skybox) continue;
                a.SkyboxMaterial = l.Skybox;
                Dirty(a, ref changed);
            }
            foreach (var a in fogs)
            {
                if (a.EnableFog == l.Fog && a.FogMode == l.FogMode && a.FogColor == l.FogColor && a.FogDensity == l.FogDensity
                    && a.FogStartDistance == l.FogStart && a.FogEndDistance == l.FogEnd) continue;
                a.EnableFog = l.Fog; a.FogMode = l.FogMode; a.FogColor = l.FogColor; a.FogDensity = l.FogDensity;
                a.FogStartDistance = l.FogStart; a.FogEndDistance = l.FogEnd;
                Dirty(a, ref changed);
            }
            foreach (var a in lightings)
            {
                if (a.AmbientMode == l.AmbientMode && a.AmbientSkyColor == l.Sky && a.AmbientEquatorColor == l.Equator
                    && a.AmbientGroundColor == l.Ground && a.AmbientIntensity == l.AmbientIntensity) continue;
                a.AmbientMode = l.AmbientMode; a.AmbientSkyColor = l.Sky; a.AmbientEquatorColor = l.Equator;
                a.AmbientGroundColor = l.Ground; a.AmbientIntensity = l.AmbientIntensity;
                Dirty(a, ref changed);
            }
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
            return changed;
        }

        /// <summary>
        /// One of each authoring per SubScene: a second set appears when an undo/revert restores a deleted
        /// "Scene Settings" after the sync already re-created it, or when the object is duplicated. Keeps the object that
        /// carries the most of the three (the original "Scene Settings" on a tie) and removes the other copies; an extra
        /// "Scene Settings" object left with nothing on it is deleted. True when anything was removed.
        /// </summary>
        private static bool RemoveDuplicates(List<SkyboxAuthoring> skyboxes, List<FogSettingsAuthoring> fogs,
            List<SceneLightingSettingsAuthoring> lightings)
        {
            if (skyboxes.Count <= 1 && fogs.Count <= 1 && lightings.Count <= 1) return false;

            GameObject keep = null;
            int keepScore = -1;
            foreach (var go in HostsOf(skyboxes, fogs, lightings))
            {
                int score = (go.GetComponent<SkyboxAuthoring>() ? 1 : 0) + (go.GetComponent<FogSettingsAuthoring>() ? 1 : 0)
                            + (go.GetComponent<SceneLightingSettingsAuthoring>() ? 1 : 0);
                if (score > keepScore || score == keepScore && go.name == ObjectName && keep.name != ObjectName)
                {
                    keep = go;
                    keepScore = score;
                }
            }

            var touched = new HashSet<GameObject>();
            Trim(skyboxes, keep, touched);
            Trim(fogs, keep, touched);
            Trim(lightings, keep, touched);
            foreach (var go in touched)
            {
                if (!go || go.name != ObjectName || go.transform.childCount > 0 || go.GetComponents<Component>().Length > 1) continue;
                Undo.DestroyObjectImmediate(go);
            }
            Debug.LogWarning("[SceneSettingsSync] Removed duplicate scene-settings authorings; kept '" + keep.name + "'.", keep);
            return true;
        }

        private static IEnumerable<GameObject> HostsOf(List<SkyboxAuthoring> a, List<FogSettingsAuthoring> b,
            List<SceneLightingSettingsAuthoring> c)
        {
            foreach (var x in a) yield return x.gameObject;
            foreach (var x in b) yield return x.gameObject;
            foreach (var x in c) yield return x.gameObject;
        }

        /// <summary>Leaves one component in the list (the one on <paramref name="keep"/> when it has one).</summary>
        private static void Trim<T>(List<T> list, GameObject keep, HashSet<GameObject> touched) where T : Component
        {
            if (list.Count <= 1) return;
            T kept = list.Find(c => c.gameObject == keep) ?? list[0];
            foreach (var c in list)
            {
                if (c == kept) continue;
                touched.Add(c.gameObject);
                Undo.DestroyObjectImmediate(c);
            }
            list.Clear();
            list.Add(kept);
        }

        private static void Dirty(Object o, ref bool changed)
        {
            EditorUtility.SetDirty(o);
            changed = true;
        }
    }

    /// <summary>The synced authorings are read-only: their values come from the Lighting window.</summary>
    [CustomEditor(typeof(SkyboxAuthoring)), CanEditMultipleObjects]
    internal sealed class SkyboxAuthoringEditor : SyncedSettingsEditor { }

    [CustomEditor(typeof(FogSettingsAuthoring)), CanEditMultipleObjects]
    internal sealed class FogSettingsAuthoringEditor : SyncedSettingsEditor { }

    [CustomEditor(typeof(SceneLightingSettingsAuthoring)), CanEditMultipleObjects]
    internal sealed class SceneLightingSettingsAuthoringEditor : SyncedSettingsEditor { }

    internal abstract class SyncedSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Set automatically from the Lighting window (Window > Rendering > Lighting > Environment) " +
                                    "of the scene that contains this SubScene. Change the skybox, fog and environment lighting there.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(true))
                DrawDefaultInspector();
        }
    }
}
#endif
