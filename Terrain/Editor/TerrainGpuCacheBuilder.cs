using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HSEVerse.TerrainTools.Editor
{
    /// <summary>
    /// The GPU half of a conversion (<see cref="HseTerrainGpuCache"/>): layer texture arrays, the far-LOD base maps and
    /// tree impostors. Built only by a manual conversion in an editor with a graphics device; every other conversion
    /// (the SubScene importer's worker, a -nographics build) reuses what is there and falls back where a part is
    /// missing or stale. One cache asset per TerrainData, next to the other generated assets.
    /// </summary>
    internal static class TerrainGpuCacheBuilder
    {
        internal const int MaxLayers = 16;          // the parameter texture's width
        internal const int MaxArraySize = 2048;     // layer textures larger than this are scaled down
        internal const int BaseSize = 1024;         // far-LOD colour / normal maps
        private const string Version = "hse-terrain-gpu-1";

        internal static bool GpuAvailable => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        internal static string CachePath(TerrainData data)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data));
            return string.IsNullOrEmpty(guid) ? null : $"{TerrainPaths.GeneratedFolder}/{guid}.gpu.asset";
        }

        internal static HseTerrainGpuCache Load(TerrainData data)
        {
            var path = CachePath(data);
            return path == null ? null : AssetDatabase.LoadAssetAtPath<HseTerrainGpuCache>(path);
        }

        /// <summary>Everything the arrays and the parameter texture are made from.</summary>
        internal static string LayersHash(TerrainData data)
        {
            var sb = new StringBuilder(Version).Append('|').Append(MaxArraySize).Append('|').Append(data.size).Append('|');
            void Asset(Object o)
            {
                if (!o) { sb.Append("-|"); return; }
                var path = AssetDatabase.GetAssetPath(o);
                sb.Append(path).Append(':').Append(string.IsNullOrEmpty(path) ? o.GetInstanceID().ToString() : AssetDatabase.GetAssetDependencyHash(path).ToString()).Append('|');
            }
            foreach (var layer in data.terrainLayers)
            {
                Asset(layer);
                if (!layer) continue;
                Asset(layer.diffuseTexture); Asset(layer.normalMapTexture); Asset(layer.maskMapTexture);
                sb.Append(layer.tileSize).Append(layer.tileOffset).Append(layer.diffuseRemapMin).Append(layer.diffuseRemapMax)
                  .Append(layer.maskMapRemapMin).Append(layer.maskMapRemapMax).Append(layer.metallic).Append(layer.smoothness)
                  .Append(layer.normalScale).Append((int)layer.smoothnessSource).Append('|');
            }
            return Hash128.Compute(sb.ToString()).ToString();
        }

        /// <summary>The painted weights plus the layers: what the base maps are baked from.</summary>
        internal static string ControlsHash(float[,,] maps, string layersHash)
        {
            var flat = new float[maps.Length];
            System.Buffer.BlockCopy(maps, 0, flat, 0, flat.Length * sizeof(float));
            var hash = Hash128.Compute(layersHash);
            hash.Append(flat);
            return hash.ToString();
        }

        private static string PrefabHash(GameObject prefab)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            return string.IsNullOrEmpty(path) ? null : Version + path + AssetDatabase.GetAssetDependencyHash(path);
        }

        /// <summary>Rebuild the stale parts (arrays, base maps, impostors) and save the cache.</summary>
        internal static HseTerrainGpuCache BuildOrUpdate(Terrain terrain, HseTerrainGpuCache cache, float[,,] maps)
        {
            var data = terrain.terrainData;
            var path = CachePath(data);
            if (path == null) return cache;
            TerrainPaths.EnsureGeneratedFolder();
            if (!cache)
            {
                cache = ScriptableObject.CreateInstance<HseTerrainGpuCache>();
                cache.name = data.name + "_Gpu";
                AssetDatabase.CreateAsset(cache, path);
            }
            var layers = data.terrainLayers;
            string layersHash = LayersHash(data);
            if (layers.Length > MaxLayers)
                Debug.LogWarning($"{terrain.name}: {layers.Length} layers; the single-pass shader takes {MaxLayers}. The terrain keeps the per-4-layer passes.", terrain);
            else if (layers.Length > 0 && (cache.layersHash != layersHash || !cache.albedo || !cache.layerParams))
            {
                Remove(cache.albedo); Remove(cache.normals); Remove(cache.masks); Remove(cache.layerParams);
                Remove(cache.baseAlbedo); Remove(cache.baseNormal);
                TerrainTextureArrays.Build(cache, data);
                Add(cache, cache.albedo); Add(cache, cache.normals); Add(cache, cache.masks); Add(cache, cache.layerParams);
                cache.layersHash = layersHash;
                cache.controlsHash = null;
            }
            if (cache.albedo && cache.layersHash == layersHash && maps != null)
            {
                string controlsHash = ControlsHash(maps, layersHash);
                if (cache.controlsHash != controlsHash || !cache.baseAlbedo || !cache.baseNormal)
                {
                    Remove(cache.baseAlbedo); Remove(cache.baseNormal);
                    TerrainTextureArrays.BakeBase(cache, terrain, maps);
                    Add(cache, cache.baseAlbedo); Add(cache, cache.baseNormal);
                    cache.controlsHash = controlsHash;
                }
            }
            var prefabs = new HashSet<GameObject>();
            foreach (var p in data.treePrototypes) if (p.prefab) prefabs.Add(p.prefab);
            foreach (var prefab in prefabs)
            {
                string hash = PrefabHash(prefab);
                var entry = cache.impostors.Find(i => i.prefab == prefab);
                if (entry != null && entry.hash == hash && entry.mesh && entry.material && entry.atlas) continue;
                if (entry != null) { Remove(entry.mesh); Remove(entry.material); Remove(entry.atlas); cache.impostors.Remove(entry); }
                var built = TerrainImpostors.Build(prefab);
                if (built == null) continue;
                built.hash = hash;
                Add(cache, built.atlas); Add(cache, built.material); Add(cache, built.mesh);
                cache.impostors.Add(built);
            }
            cache.impostors.RemoveAll(i => i == null || !i.prefab);
            EditorUtility.SetDirty(cache);
            AssetDatabase.SaveAssets();
            return cache;
        }

        internal static HseTerrainGpuCache.Impostor FindImpostor(HseTerrainGpuCache cache, GameObject prefab)
        {
            if (!cache || !prefab) return null;
            var entry = cache.impostors.Find(i => i.prefab == prefab);
            return entry != null && entry.hash == PrefabHash(prefab) && entry.mesh && entry.material ? entry : null;
        }

        private static void Remove(Object o)
        {
            if (!o) return;
            if (AssetDatabase.IsSubAsset(o)) AssetDatabase.RemoveObjectFromAsset(o);
            Object.DestroyImmediate(o, true);
        }

        private static void Add(HseTerrainGpuCache cache, Object o)
        {
            if (o) AssetDatabase.AddObjectToAsset(o, cache);
        }
    }
}
