#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HSE.Mechanics.Authorings;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HSE_MAIN.Editor.Mods
{
    internal static class LocationContentBuild
    {
        /// <summary>Создаёт служебную связь перед экспортом. Сохраняет только свои изменения в предварительно сохранённых сценах.</summary>
        public static LocationContentBootstrap Prepare(string scenePath)
        {
            using var main = new SceneEditScope(scenePath);
            var subScenes = new SortedDictionary<string, SubScene>(StringComparer.Ordinal);
            foreach (var root in main.Scene.GetRootGameObjects())
            {
                foreach (var sub in root.GetComponentsInChildren<SubScene>(true))
                {
                    string path = AssetDatabase.GetAssetPath(sub.SceneAsset);
                    if (string.IsNullOrEmpty(path) || path == scenePath)
                        throw new InvalidOperationException("ContentScene contains an invalid SubScene reference.");
                    subScenes[path] = sub;
                }
            }

            if (subScenes.Count == 0)
                throw new InvalidOperationException("ContentScene contains no subscene to build.");

            var scopes = new List<SceneEditScope>();
            try
            {
                LocationSceneAuthoring reference = null;
                SubScene firstSubScene = null;
                foreach (var pair in subScenes)
                {
                    firstSubScene ??= pair.Value;
                    var scope = new SceneEditScope(pair.Key);
                    scopes.Add(scope);
                    foreach (var root in scope.Scene.GetRootGameObjects())
                    {
                        foreach (var candidate in root.GetComponentsInChildren<LocationSceneAuthoring>(true))
                        {
                            if (reference != null)
                                throw new InvalidOperationException("Multiple LocationSceneAuthoring components found. Keep only one service reference.");
                            if (!candidate.enabled || !candidate.gameObject.activeInHierarchy)
                                throw new InvalidOperationException("The location service reference must be active.");
                            reference = candidate;
                        }
                    }
                }

                var id = UntypedWeakReferenceId.CreateFromObjectInstance(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath));
                var referenceScene = reference != null ? reference.gameObject.scene : scopes[0].Scene;
                bool referenceChanged = reference == null;
                if (reference == null)
                {
                    var service = new GameObject("[CodeCraft] Location Content");
                    SceneManager.MoveGameObjectToScene(service, referenceScene);
                    Undo.RegisterCreatedObjectUndo(service, "Prepare location content");
                    reference = Undo.AddComponent<LocationSceneAuthoring>(service);
                }

                if (!reference.Scene.Id.Equals(id))
                {
                    Undo.RecordObject(reference, "Update location scene reference");
                    reference.Scene = new WeakObjectSceneReference { Id = id };
                    referenceChanged = true;
                }

                bool hasMarker = false;
                foreach (var root in main.Scene.GetRootGameObjects())
                    hasMarker |= root.GetComponentInChildren<DeliveredLocationScene>(true) != null;
                if (!hasMarker)
                    Undo.AddComponent<DeliveredLocationScene>(firstSubScene.gameObject);

                if (referenceChanged)
                    SaveGeneratedChanges(referenceScene);
                if (!hasMarker)
                    SaveGeneratedChanges(main.Scene);

                return Inspect(scenePath) ?? throw new InvalidOperationException("Location service preparation did not produce a valid bootstrap.");
            }
            finally
            {
                for (int i = scopes.Count - 1; i >= 0; i--)
                    scopes[i].Dispose();
            }
        }

        private static void SaveGeneratedChanges(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Cannot save location service data in {scene.path}.");
        }

        private sealed class SceneEditScope : IDisposable
        {
            public Scene Scene { get; }
            private readonly bool _opened;

            public SceneEditScope(string path)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    throw new InvalidOperationException($"Scene not found: {path}.");
                var scene = SceneManager.GetSceneByPath(path);
                _opened = !scene.IsValid() || !scene.isLoaded;
                if (!_opened && scene.isDirty)
                    throw new InvalidOperationException($"Save scene before exporting: {path}.");
                Scene = _opened ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene;
            }

            public void Dispose()
            {
                if (_opened && Scene.IsValid())
                    EditorSceneManager.CloseScene(Scene, true);
            }
        }

        public static LocationContentBootstrap Inspect(string scenePath)
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            bool preview = !scene.IsValid() || !scene.isLoaded;
            if (preview)
                scene = EditorSceneManager.OpenPreviewScene(scenePath);

            try
            {
                bool delivered = false;
                var subScenes = new List<SubScene>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    delivered |= root.GetComponentInChildren<DeliveredLocationScene>(true) != null;
                    subScenes.AddRange(root.GetComponentsInChildren<SubScene>(true));
                }

                if (!delivered)
                    return null;

                var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int referenceCount = 0;
                string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                foreach (var subScene in subScenes)
                {
                    var path = AssetDatabase.GetAssetPath(subScene.SceneAsset);
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid))
                        throw new InvalidOperationException("Delivered location contains an unassigned SubScene.");
                    if (guids.Add(guid))
                        referenceCount += CountReferences(path, sceneGuid);
                }

                if (referenceCount != 1)
                    throw new InvalidOperationException("Delivered location requires exactly one active LocationSceneAuthoring referencing its ContentScene.");

                var ids = new List<string>(guids);
                ids.Sort(StringComparer.Ordinal);
                var bootstrap = new LocationContentBootstrap { SubScenes = ids.ToArray() };
                bootstrap.Validate();
                return bootstrap;
            }
            finally
            {
                if (preview)
                    EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static int CountReferences(string path, string sceneGuid)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool preview = !scene.IsValid() || !scene.isLoaded;
            if (preview)
                scene = EditorSceneManager.OpenPreviewScene(path);

            try
            {
                int count = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var reference in root.GetComponentsInChildren<LocationSceneAuthoring>(true))
                    {
                        if (!reference.gameObject.activeInHierarchy || !reference.enabled ||
                            !reference.Scene.IsReferenceValid ||
                            !string.Equals(reference.Scene.Id.GlobalId.AssetGUID.ToString(), sceneGuid, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("LocationSceneAuthoring must be active and reference the delivered ContentScene.");
                        count++;
                    }
                }
                return count;
            }
            finally
            {
                if (preview)
                    EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
