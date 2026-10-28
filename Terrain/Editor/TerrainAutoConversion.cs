using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HSEVerse.TerrainTools.Editor
{
    // SubSceneImporter opens an authoring scene in an Asset Import Worker before BakeScene.
    // Convert that temporary scene, so the existing mesh, LOD and physics bakers process it.
    // No renderer settings or on-disk authoring scenes are changed.
    [InitializeOnLoad]
    internal static class TerrainAutoConversion
    {
        private static readonly Dictionary<int, List<Object>> Owned = new Dictionary<int, List<Object>>();
        static TerrainAutoConversion()
        {
            if (!AssetDatabase.IsAssetImportWorkerProcess()) return;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) TerrainConverter.ConvertScene(scene);
        }

        internal static void KeepAlive(Scene scene, List<Object> objects)
        {
            if (!Owned.TryGetValue(scene.handle, out var existing)) Owned.Add(scene.handle, existing = new List<Object>());
            existing.AddRange(objects);
        }

        private static void OnSceneClosed(Scene scene) => Release(scene);

        internal static void Release(Scene scene)
        {
            if (!Owned.TryGetValue(scene.handle, out var objects)) return;
            foreach (var obj in objects) if (obj && !EditorUtility.IsPersistent(obj)) Object.DestroyImmediate(obj);
            Owned.Remove(scene.handle);
        }
    }

    // Record dependencies on the original authoring assets, including disabled source Terrain.
    // A failure of the worker hook must be visible rather than silently shipping an empty terrain.
    internal sealed class TerrainConversionDependenciesBaker : Baker<Terrain>
    {
        public override void Bake(Terrain authoring)
        {
            var data = authoring.terrainData;
            if (!data) return;
            DependsOn(data);
            DependsOn(authoring.materialTemplate);
            foreach (var layer in data.terrainLayers)
            {
                DependsOn(layer);
                if (!layer) continue;
                DependsOn(layer.diffuseTexture); DependsOn(layer.normalMapTexture); DependsOn(layer.maskMapTexture);
            }
            foreach (var prototype in data.treePrototypes) DependsOn(prototype.prefab);
            foreach (var prototype in data.detailPrototypes) { DependsOn(prototype.prototype); DependsOn(prototype.prototypeTexture); }
            // the GPU-made parts a manual conversion stores (texture arrays, base maps, impostors)
            var cache = TerrainGpuCacheBuilder.Load(data);
            if (cache) DependsOn(cache);
            var marker = GetComponent<HseTerrainConversion>();
            if (AssetDatabase.IsAssetImportWorkerProcess() && authoring.enabled && (!marker || !marker.generatedRoot))
                throw new InvalidOperationException($"HSEVerse: Terrain '{authoring.name}' was not converted before SubScene baking. Use HSEVerse/Terrain/Convert or Update Selected Terrains and rebuild.");
        }
    }
}
