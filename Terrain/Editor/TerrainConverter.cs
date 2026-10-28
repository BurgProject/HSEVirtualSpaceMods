using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HSEVerse.TerrainTools.Editor
{
    /// <summary>
    /// Terrain -> ordinary meshes the SubScene bakers understand (Entities Graphics does not render Terrain and Unity
    /// Physics has no Terrain baker). Per 128-cell chunk: a 4-level LOD group with skirts; ground collision as one
    /// Unity Physics height field (MeshColliders at 1/4 density when the terrain has holes); trees as prefab instances
    /// with the terrain's billboard / cull distances; grass in 32 m cells culled at the detail distance.
    /// A manual conversion with a graphics device also builds the GPU cache (layer texture arrays, far-LOD base maps,
    /// tree impostors); conversions without one reuse it and fall back where it is missing.
    /// </summary>
    public static class TerrainConverter
    {
        [MenuItem("HSEverse/Terrain/Convert or Update Selected Terrains", false, 100)]
        public static void ConvertSelected()
        {
            var terrains = Selection.gameObjects.SelectMany(go => go.GetComponentsInChildren<Terrain>(true)).Distinct().ToArray();
            if (terrains.Length == 0)
                throw new InvalidOperationException("Select a Terrain or a parent containing Terrains in an open scene/SubScene.");
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Convert HSEVerse Terrains");
            try
            {
                for (int i = 0; i < terrains.Length; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("HSEVerse Terrain", terrains[i].name, (float)i / terrains.Length)) break;
                    Convert(terrains[i], true);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorUtility.ClearProgressBar(); Undo.CollapseUndoOperations(undoGroup); }
        }

        [MenuItem("HSEverse/Terrain/Restore Selected Source Terrains", false, 101)]
        public static void RestoreSelected()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Restore source Terrains");
            foreach (var marker in Selection.gameObjects.SelectMany(go => go.GetComponentsInChildren<HseTerrainConversion>(true)).Distinct())
            {
                var terrain = marker.GetComponent<Terrain>();
                var collider = marker.GetComponent<TerrainCollider>();
                if (terrain) { Undo.RecordObject(terrain, "Restore Terrain"); terrain.enabled = marker.terrainWasEnabled; }
                if (collider) { Undo.RecordObject(collider, "Restore Terrain collider"); collider.enabled = marker.colliderWasEnabled; }
                if (marker.generatedRoot) Undo.DestroyObjectImmediate(marker.generatedRoot);
                Undo.DestroyObjectImmediate(marker);
            }
            Undo.CollapseUndoOperations(undoGroup);
        }

        internal static HseTerrainConversion Convert(Terrain terrain, bool persistent)
        {
            if (!terrain || !terrain.terrainData) throw new InvalidOperationException("Terrain has no TerrainData.");
            if (EditorUtility.IsPersistent(terrain) || !terrain.gameObject.scene.IsValid())
                throw new InvalidOperationException("Convert a Terrain instance in a scene, not a prefab asset.");
            var data = terrain.terrainData;
            var previous = terrain.GetComponent<HseTerrainConversion>();
            bool enabled = previous ? previous.terrainWasEnabled : terrain.enabled;
            var collider = terrain.GetComponent<TerrainCollider>();
            bool colliderEnabled = previous ? previous.colliderWasEnabled : collider && collider.enabled;
            if (colliderEnabled && collider.terrainData != data)
                throw new InvalidOperationException($"{terrain.name}: TerrainCollider uses different TerrainData; align the data before conversion.");
            var owned = new List<Object>();
            var root = new GameObject("HSEVerse Converted Terrain");
            root.transform.SetParent(terrain.transform, false);
            root.layer = terrain.gameObject.layer;
            root.SetActive(false);
            string assetPath = null;
            try
            {
                // ---------------------------------------------------------------- materials
                float[,,] maps = data.alphamapLayers > 0 && data.terrainLayers.Length > 0
                    ? data.GetAlphamaps(0, 0, data.alphamapResolution, data.alphamapResolution) : null;
                var cache = TerrainGpuCacheBuilder.Load(data);
                if (persistent && TerrainGpuCacheBuilder.GpuAvailable)
                    cache = TerrainGpuCacheBuilder.BuildOrUpdate(terrain, cache, maps);
                string layersHash = TerrainGpuCacheBuilder.LayersHash(data);
                Material[] near, far;
                if (maps != null && cache && cache.albedo && cache.layerParams && cache.layersHash == layersHash)
                {
                    var control = TerrainMaterials.BuildControlArray(data, maps);
                    owned.Add(control);
                    near = new[] { TerrainMaterials.CreateArray(terrain, cache, control, owned.Add) };
                    far = cache.baseAlbedo && cache.baseNormal && cache.controlsHash == TerrainGpuCacheBuilder.ControlsHash(maps, layersHash)
                        ? new[] { TerrainMaterials.CreateBase(terrain, cache, owned.Add) }
                        : near;
                }
                else
                {
                    near = TerrainMaterials.Create(terrain, owned.Add);
                    far = near;
                }

                // ---------------------------------------------------------------- ground
                var staticFlags = GameObjectUtility.GetStaticEditorFlags(terrain.gameObject);
                bool lightmapUv = terrain.lightmapIndex >= 0 || (staticFlags & StaticEditorFlags.ContributeGI) != 0;
                bool holes = TerrainGeometry.HasHoles(data);
                foreach (var chunk in TerrainGeometry.CreateChunks(data, lightmapUv, colliderEnabled && holes))
                {
                    var chunkGo = new GameObject($"Terrain_{chunk.X}_{chunk.Z}");
                    chunkGo.transform.SetParent(root.transform, false);
                    chunkGo.layer = terrain.gameObject.layer;
                    GameObjectUtility.SetStaticEditorFlags(chunkGo, staticFlags);
                    var lods = new List<LOD>();
                    for (int l = 0; l < chunk.Lods.Length; l++)
                    {
                        var mesh = chunk.Lods[l];
                        owned.Add(mesh);
                        if (mesh.GetIndexCount(0) == 0) continue;
                        var materials = l >= 2 ? far : near;
                        var renderers = new List<Renderer>();
                        for (int group = 0; group < materials.Length; group++)
                        {
                            var go = new GameObject($"LOD{l}" + (group == 0 ? "" : $"_Layers{group}"));
                            go.transform.SetParent(chunkGo.transform, false);
                            go.layer = terrain.gameObject.layer;
                            GameObjectUtility.SetStaticEditorFlags(go, staticFlags);
                            go.AddComponent<MeshFilter>().sharedMesh = mesh;
                            var renderer = go.AddComponent<MeshRenderer>();
                            renderer.sharedMaterial = materials[group];
                            renderer.enabled = enabled && terrain.drawHeightmap;
                            renderer.shadowCastingMode = group == 0 ? terrain.shadowCastingMode : ShadowCastingMode.Off;
                            renderer.receiveShadows = true;
                            renderer.renderingLayerMask = terrain.renderingLayerMask;
                            renderer.lightmapIndex = terrain.lightmapIndex;
                            renderer.lightmapScaleOffset = terrain.lightmapScaleOffset;
                            renderers.Add(renderer);
                        }
                        lods.Add(new LOD(TerrainGeometry.LodHeights[l], renderers.ToArray()));
                    }
                    if (lods.Count > 0)
                    {
                        var bounds = chunk.Lods[0].bounds;
                        var lodGroup = chunkGo.AddComponent<LODGroup>();
                        lodGroup.localReferencePoint = bounds.center;
                        lodGroup.size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                        var last = lods[lods.Count - 1];
                        last.screenRelativeTransitionHeight = 0f; // the ground is never culled
                        lods[lods.Count - 1] = last;
                        lodGroup.SetLODs(lods.ToArray());
                    }
                    if (chunk.Collider)
                    {
                        owned.Add(chunk.Collider);
                        if (chunk.Collider.GetIndexCount(0) > 0)
                        {
                            var go = new GameObject("Collider");
                            go.transform.SetParent(chunkGo.transform, false);
                            go.layer = terrain.gameObject.layer;
                            GameObjectUtility.SetStaticEditorFlags(go, staticFlags);
                            var meshCollider = go.AddComponent<MeshCollider>();
                            meshCollider.sharedMesh = chunk.Collider;
                            meshCollider.sharedMaterial = collider.sharedMaterial;
                            meshCollider.includeLayers = collider.includeLayers;
                            meshCollider.excludeLayers = collider.excludeLayers;
                            meshCollider.layerOverridePriority = collider.layerOverridePriority;
                        }
                    }
                    if (chunkGo.transform.childCount == 0) Object.DestroyImmediate(chunkGo);
                }
                if (colliderEnabled && !holes)
                {
                    var heightfield = root.AddComponent<HseTerrainHeightfield>();
                    heightfield.terrainData = data;
                    heightfield.material = collider.sharedMaterial;
                    heightfield.includeLayers = collider.includeLayers.value;
                    heightfield.excludeLayers = collider.excludeLayers.value;
                }

                // ---------------------------------------------------------------- vegetation
                bool drawVegetation = enabled && terrain.drawTreesAndFoliage;
                if (drawVegetation || colliderEnabled)
                    TerrainVegetation.CreateTrees(terrain, root.transform, colliderEnabled, drawVegetation, cache, owned.Add);
                if (drawVegetation)
                    TerrainVegetation.CreateDetails(terrain, root.transform, owned.Add);

                if (persistent)
                {
                    TerrainPaths.EnsureGeneratedFolder();
                    assetPath = AssetDatabase.GenerateUniqueAssetPath(TerrainPaths.GeneratedFolder + "/Terrain.asset");
                    // The first generated object is the main asset; the rest are its subassets.
                    AssetDatabase.CreateAsset(owned[0], assetPath);
                    for (int i = 1; i < owned.Count; i++) AssetDatabase.AddObjectToAsset(owned[i], assetPath);
                    AssetDatabase.SaveAssets();
                    Undo.RegisterCreatedObjectUndo(root, "Convert Terrain");
                    if (previous) Undo.RecordObject(previous, "Update Terrain conversion");
                    Undo.RecordObject(terrain, "Disable source Terrain");
                    if (collider) Undo.RecordObject(collider, "Disable source TerrainCollider");
                }
                var marker = previous ? previous : persistent ? Undo.AddComponent<HseTerrainConversion>(terrain.gameObject) : terrain.gameObject.AddComponent<HseTerrainConversion>();
                if (marker.generatedRoot)
                {
                    if (persistent) Undo.DestroyObjectImmediate(marker.generatedRoot);
                    else Object.DestroyImmediate(marker.generatedRoot);
                }
                marker.generatedRoot = root;
                marker.terrainWasEnabled = enabled;
                marker.colliderWasEnabled = colliderEnabled;
                marker.assetPath = assetPath;
                terrain.enabled = false;
                if (collider) collider.enabled = false;
                root.SetActive(true);
                if (persistent)
                {
                    EditorUtility.SetDirty(marker);
                    EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
                    Debug.Log($"{terrain.name}: converted to {root.GetComponentsInChildren<MeshRenderer>(true).Length} mesh renderers. Source Terrain is retained. Generated assets: {assetPath}", terrain);
                }
                else TerrainAutoConversion.KeepAlive(terrain.gameObject.scene, owned);
                return marker;
            }
            catch
            {
                Object.DestroyImmediate(root);
                foreach (var item in owned) if (item && !EditorUtility.IsPersistent(item)) Object.DestroyImmediate(item);
                throw;
            }
        }

        internal static void ConvertScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                // Rebuild from the source even for a manual conversion: painted changes must reach the build.
                if (terrain.terrainData && (terrain.enabled || terrain.GetComponent<HseTerrainConversion>())) Convert(terrain, false);
            }
        }
    }
}
