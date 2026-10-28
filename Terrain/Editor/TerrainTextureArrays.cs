using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HSEVerse.TerrainTools.Editor
{
    /// <summary>GPU builders for <see cref="HseTerrainGpuCache"/>: every layer's maps resized to one size and packed
    /// into compressed texture arrays (albedo BC7 sRGB, normals BC5 as X/Y, masks BC7 linear), the per-layer numbers
    /// into a float texture, and the base maps baked from the blended surface.</summary>
    internal static class TerrainTextureArrays
    {
        private const int ParamRows = 6;

        internal static void Build(HseTerrainGpuCache cache, TerrainData data)
        {
            var layers = data.terrainLayers;
            int w = 4, h = 4;
            bool anyNormal = false, anyMask = false;
            foreach (var layer in layers)
            {
                if (!layer) continue;
                foreach (var t in new Texture[] { layer.diffuseTexture, layer.normalMapTexture, layer.maskMapTexture })
                    if (t) { w = Mathf.Max(w, t.width); h = Mathf.Max(h, t.height); }
                anyNormal |= layer.normalMapTexture;
                anyMask |= layer.maskMapTexture;
            }
            w = RoundTo4(Mathf.Min(w, TerrainGpuCacheBuilder.MaxArraySize));
            h = RoundTo4(Mathf.Min(h, TerrainGpuCacheBuilder.MaxArraySize));
            cache.albedo = BuildArray(data.name + "_Albedo", layers, l => l.diffuseTexture, w, h, TextureFormat.BC7, true, false, new Color32(255, 255, 255, 255));
            cache.normals = anyNormal ? BuildArray(data.name + "_Normals", layers, l => l.normalMapTexture, w, h, TextureFormat.BC5, false, true, new Color32(128, 128, 0, 255)) : null;
            cache.masks = anyMask ? BuildArray(data.name + "_Masks", layers, l => l.maskMapTexture, w, h, TextureFormat.BC7, false, false, new Color32(128, 128, 128, 128)) : null;
            cache.hasNormals = anyNormal;
            cache.hasMasks = anyMask;
            cache.layerParams = BuildParams(data);
        }

        private static int RoundTo4(int v) => Mathf.Max(4, (v + 3) / 4 * 4);

        private static Texture2DArray BuildArray(string name, TerrainLayer[] layers, Func<TerrainLayer, Texture2D> pick, int w, int h,
            TextureFormat format, bool srgb, bool repackNormal, Color32 fill)
        {
            var array = new Texture2DArray(w, h, layers.Length, format, true, !srgb)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4
            };
            Material repack = repackNormal ? new Material(TerrainPaths.Load(TerrainPaths.RepackShader)) { hideFlags = HideFlags.HideAndDontSave } : null;
            try
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    var source = layers[i] ? pick(layers[i]) : null;
                    var slice = Capture(source, w, h, srgb, repack, fill);
                    EditorUtility.CompressTexture(slice, format, TextureCompressionQuality.Normal);
                    for (int m = 0; m < array.mipmapCount; m++)
                        array.SetPixelData(slice.GetPixelData<byte>(m), m, i);
                    Object.DestroyImmediate(slice);
                }
            }
            finally { if (repack) Object.DestroyImmediate(repack); }
            array.Apply(false, false);
            return array;
        }

        /// <summary>The source at the target size, read back to the CPU (with a full mip chain). A missing source is
        /// the fill colour.</summary>
        private static Texture2D Capture(Texture source, int w, int h, bool srgb, Material material, Color32 fill)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, !srgb);
            if (!source)
            {
                var px = new Color32[w * h];
                for (int i = 0; i < px.Length; i++) px[i] = fill;
                tex.SetPixels32(px);
                tex.Apply(true);
                return tex;
            }
            ReadBack(tex, w, h, srgb, rt =>
            {
                if (material) Graphics.Blit(source, rt, material);
                else Graphics.Blit(source, rt);
            });
            return tex;
        }

        private static void ReadBack(Texture2D into, int w, int h, bool srgb, Action<RenderTexture> draw)
        {
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0) { sRGB = srgb, msaaSamples = 1 });
            var previous = RenderTexture.active;
            try
            {
                draw(rt);
                RenderTexture.active = rt;
                into.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                into.Apply(true);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Texture2D BuildParams(TerrainData data)
        {
            var tex = new Texture2D(TerrainGpuCacheBuilder.MaxLayers, ParamRows, TextureFormat.RGBAFloat, false, true)
            {
                name = data.name + "_LayerParams", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color[TerrainGpuCacheBuilder.MaxLayers * ParamRows];
            var layers = data.terrainLayers;
            for (int i = 0; i < layers.Length && i < TerrainGpuCacheBuilder.MaxLayers; i++)
            {
                var layer = layers[i];
                if (!layer) continue;
                bool alphaSmoothness = TerrainMaterials.AlphaSmoothness(layer);
                void Row(int row, Vector4 v) => px[row * TerrainGpuCacheBuilder.MaxLayers + i] = v;
                Row(0, TerrainMaterials.LayerST(data, layer));
                Row(1, layer.diffuseRemapMax - layer.diffuseRemapMin);
                Row(2, layer.maskMapRemapMax - layer.maskMapRemapMin);
                Row(3, layer.maskMapRemapMin);
                Row(4, TerrainMaterials.LayerParams(layer, alphaSmoothness));
                Row(5, new Vector4(alphaSmoothness ? 1 : 0, 0, 0, 0));
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Render the blended surface into <see cref="TerrainGpuCacheBuilder.BaseSize"/>-square colour and
        /// normal maps (the far LODs' only textures).</summary>
        internal static void BakeBase(HseTerrainGpuCache cache, Terrain terrain, float[,,] maps)
        {
            int size = TerrainGpuCacheBuilder.BaseSize;
            var control = TerrainMaterials.BuildControlArray(terrain.terrainData, maps);
            var bake = new Material(TerrainPaths.Load(TerrainPaths.BakeShader)) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                TerrainMaterials.ApplyArrayProperties(bake, terrain, cache, control);
                bake.SetFloat("_BakeTexel", 1f / size);
                var albedo = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = terrain.terrainData.name + "_BaseAlbedo" };
                ReadBack(albedo, size, size, true, rt => Graphics.Blit(null, rt, bake, 0));
                EditorUtility.CompressTexture(albedo, TextureFormat.BC7, TextureCompressionQuality.Normal);
                var normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = terrain.terrainData.name + "_BaseNormal" };
                ReadBack(normal, size, size, false, rt => Graphics.Blit(null, rt, bake, 1));
                EditorUtility.CompressTexture(normal, TextureFormat.BC5, TextureCompressionQuality.Normal);
                albedo.wrapMode = normal.wrapMode = TextureWrapMode.Clamp;
                albedo.filterMode = normal.filterMode = FilterMode.Trilinear;
                albedo.Apply(false, false); normal.Apply(false, false);
                cache.baseAlbedo = albedo;
                cache.baseNormal = normal;
            }
            finally
            {
                Object.DestroyImmediate(bake);
                Object.DestroyImmediate(control);
            }
        }
    }
}
