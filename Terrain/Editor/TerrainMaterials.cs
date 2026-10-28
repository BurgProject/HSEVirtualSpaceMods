using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HSEVerse.TerrainTools.Editor
{
    internal static class TerrainMaterials
    {
        /// <summary>Fallback when there is no texture-array cache: one material per 4 layers, each extra one an
        /// additive pass over the same chunks.</summary>
        internal static Material[] Create(Terrain terrain, Action<UnityEngine.Object> own)
        {
            var shader = TerrainPaths.Load(TerrainPaths.MeshShader);
            var data = terrain.terrainData;
            var layers = data.terrainLayers;
            WarnCustomShader(terrain);
            var result = new List<Material>();
            var controls = data.alphamapTextures;
            for (int group = 0; group < Mathf.Max(1, (layers.Length + 3) / 4); group++)
            {
                var material = new Material(shader) { name = $"{terrain.name}_Layers_{group * 4}", enableInstancing = true };
                own(material);
                material.SetTexture("_Control", group < controls.Length ? controls[group] : Texture2D.redTexture);
                material.SetFloat("_LayerCount", layers.Length);
                material.SetFloat("_DstBlend", group == 0 ? (float)BlendMode.Zero : (float)BlendMode.One);
                material.SetFloat("_ZWrite", group == 0 ? 1 : 0);
                material.SetFloat("_AddPass", group == 0 ? 0 : 1);
                material.renderQueue = (int)RenderQueue.Geometry - 100 + group;
                material.SetFloat("_HeightBlend", HeightBlend(terrain) ? 1 : 0);
                material.SetFloat("_HeightTransition", HeightTransition(terrain));
                if (group > 0)
                {
                    material.SetShaderPassEnabled("ShadowCaster", false);
                    material.SetShaderPassEnabled("DepthOnly", false);
                    material.SetShaderPassEnabled("DepthNormals", false);
                }
                for (int i = 0; i < 4; i++)
                {
                    var layer = group * 4 + i < layers.Length ? layers[group * 4 + i] : null;
                    if (!layer) continue;
                    material.SetTexture($"_Splat{i}", layer.diffuseTexture ? layer.diffuseTexture : Texture2D.whiteTexture);
                    material.SetTexture($"_Normal{i}", layer.normalMapTexture ? layer.normalMapTexture : Texture2D.normalTexture);
                    material.SetTexture($"_Mask{i}", layer.maskMapTexture ? layer.maskMapTexture : Texture2D.grayTexture);
                    material.SetVector($"_LayerST{i}", LayerST(data, layer));
                    material.SetVector($"_Diffuse{i}", layer.diffuseRemapMax - layer.diffuseRemapMin);
                    material.SetVector($"_MaskScale{i}", layer.maskMapRemapMax - layer.maskMapRemapMin);
                    material.SetVector($"_MaskOffset{i}", layer.maskMapRemapMin);
                    bool alphaSmoothness = AlphaSmoothness(layer);
                    material.SetVector($"_Params{i}", LayerParams(layer, alphaSmoothness));
                    material.SetFloat($"_AlphaSmoothness{i}", alphaSmoothness ? 1 : 0);
                }
                result.Add(material);
            }
            return result.ToArray();
        }

        /// <summary>All layers in one pass (texture arrays from the GPU cache, splat weights from this conversion).</summary>
        internal static Material CreateArray(Terrain terrain, HseTerrainGpuCache cache, Texture2DArray control, Action<UnityEngine.Object> own)
        {
            WarnCustomShader(terrain);
            var material = new Material(TerrainPaths.Load(TerrainPaths.ArrayShader)) { name = $"{terrain.name}_Layers", enableInstancing = true };
            own(material);
            ApplyArrayProperties(material, terrain, cache, control);
            material.renderQueue = (int)RenderQueue.Geometry - 100;
            return material;
        }

        /// <summary>Shared by the terrain material and the base-map bake.</summary>
        internal static void ApplyArrayProperties(Material material, Terrain terrain, HseTerrainGpuCache cache, Texture2DArray control)
        {
            material.SetTexture("_Control", control);
            material.SetFloat("_ControlSize", control.width);
            material.SetTexture("_Albedo", cache.albedo);
            if (cache.normals) material.SetTexture("_Normals", cache.normals);
            if (cache.masks) material.SetTexture("_Masks", cache.masks);
            material.SetTexture("_LayerParams", cache.layerParams);
            material.SetFloat("_LayerCount", terrain.terrainData.terrainLayers.Length);
            material.SetFloat("_HeightBlend", HeightBlend(terrain) ? 1 : 0);
            material.SetFloat("_HeightTransition", HeightTransition(terrain));
            if (cache.hasNormals) material.EnableKeyword("_HSE_NORMALS"); else material.DisableKeyword("_HSE_NORMALS");
            if (cache.hasMasks) material.EnableKeyword("_HSE_MASKS"); else material.DisableKeyword("_HSE_MASKS");
        }

        /// <summary>Far LODs: the baked colour + normal.</summary>
        internal static Material CreateBase(Terrain terrain, HseTerrainGpuCache cache, Action<UnityEngine.Object> own)
        {
            var material = new Material(TerrainPaths.Load(TerrainPaths.BaseShader)) { name = $"{terrain.name}_Base", enableInstancing = true };
            own(material);
            material.SetTexture("_BaseAlbedo", cache.baseAlbedo);
            material.SetTexture("_BaseNormal", cache.baseNormal);
            material.renderQueue = (int)RenderQueue.Geometry - 100;
            return material;
        }

        /// <summary>The splat weights, four layers per slice (the same 8-bit precision as Terrain's own control maps).
        /// Built on the CPU in every conversion, so painting reaches the build without a GPU.</summary>
        internal static Texture2DArray BuildControlArray(TerrainData data, float[,,] maps)
        {
            int res = maps.GetLength(0), layers = maps.GetLength(2);
            int slices = Mathf.Max(1, (layers + 3) / 4);
            var array = new Texture2DArray(res, res, slices, TextureFormat.RGBA32, false, true)
            {
                name = data.name + "_SplatWeights", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[res * res];
            for (int s = 0; s < slices; s++)
            {
                for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    byte C(int c) { int l = s * 4 + c; return l < layers ? (byte)Mathf.RoundToInt(Mathf.Clamp01(maps[y, x, l]) * 255f) : (byte)0; }
                    pixels[y * res + x] = new Color32(C(0), C(1), C(2), C(3));
                }
                array.SetPixels32(pixels, s);
            }
            array.Apply(false, false);
            return array;
        }

        internal static Vector4 LayerST(TerrainData data, TerrainLayer layer) => new Vector4(
            data.size.x / Mathf.Max(0.0001f, layer.tileSize.x), data.size.z / Mathf.Max(0.0001f, layer.tileSize.y),
            layer.tileOffset.x / Mathf.Max(0.0001f, layer.tileSize.x), layer.tileOffset.y / Mathf.Max(0.0001f, layer.tileSize.y));

        internal static bool AlphaSmoothness(TerrainLayer layer) =>
            layer.diffuseTexture && GraphicsFormatUtility.HasAlphaChannel(layer.diffuseTexture.graphicsFormat)
            && layer.smoothnessSource == TerrainLayerSmoothnessSource.DiffuseAlphaChannel;

        internal static Vector4 LayerParams(TerrainLayer layer, bool alphaSmoothness) => new Vector4(layer.metallic,
            alphaSmoothness ? 1 : layer.smoothness, layer.normalMapTexture ? layer.normalScale : 0, layer.maskMapTexture ? 1 : 0);

        private static bool HeightBlend(Terrain terrain) =>
            terrain.materialTemplate && terrain.materialTemplate.IsKeywordEnabled("_TERRAIN_BLEND_HEIGHT") && terrain.terrainData.terrainLayers.Length <= 4;

        private static float HeightTransition(Terrain terrain) =>
            terrain.materialTemplate && terrain.materialTemplate.HasProperty("_HeightTransition") ? terrain.materialTemplate.GetFloat("_HeightTransition") : 0.1f;

        private static void WarnCustomShader(Terrain terrain)
        {
            if (terrain.materialTemplate && terrain.materialTemplate.shader.name != "Universal Render Pipeline/Terrain/Lit")
                Debug.LogWarning($"{terrain.name}: custom Terrain shader '{terrain.materialTemplate.shader.name}' is replaced by URP Terrain layer shading. Custom shader effects cannot be inferred.", terrain);
        }
    }
}
