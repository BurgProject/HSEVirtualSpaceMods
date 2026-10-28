using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HSEVerse.TerrainTools.Editor
{
    internal sealed class TerrainVegetationMaterials
    {
        private readonly Dictionary<Material, Material> cache = new Dictionary<Material, Material>();
        private readonly Action<UnityEngine.Object> own;
        internal TerrainVegetationMaterials(Action<UnityEngine.Object> own) => this.own = own;

        internal Material Get(Material source)
        {
            if (!source) throw new InvalidOperationException("Terrain vegetation has a missing material.");
            if (cache.TryGetValue(source, out var result)) return result;
            if (source.shader && source.shader.keywordSpace.FindKeyword("DOTS_INSTANCING_ON").isValid)
            {
                cache.Add(source, source);
                return source;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is missing; cannot convert vegetation material " + source.name);
            result = new Material(shader) { name = source.name + "_HSEVerse_Lit", enableInstancing = true };
            string mapName = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            if (source.HasProperty(mapName))
            {
                result.SetTexture("_BaseMap", source.GetTexture(mapName));
                result.SetTextureScale("_BaseMap", source.GetTextureScale(mapName));
                result.SetTextureOffset("_BaseMap", source.GetTextureOffset(mapName));
            }
            string colorName = source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (source.HasProperty(colorName)) result.SetColor("_BaseColor", source.GetColor(colorName));
            if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap"))
            {
                result.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                result.SetFloat("_BumpScale", source.HasProperty("_BumpScale") ? source.GetFloat("_BumpScale") : 1);
                result.EnableKeyword("_NORMALMAP");
            }
            result.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0);
            result.SetFloat("_Smoothness", source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") :
                source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0);
            if (source.HasProperty("_MetallicGlossMap") && source.GetTexture("_MetallicGlossMap"))
            {
                result.SetTexture("_MetallicGlossMap", source.GetTexture("_MetallicGlossMap"));
                result.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            result.SetFloat("_AlphaClip", 1);
            result.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.5f);
            result.SetFloat("_Cull", (float)CullMode.Off);
            result.EnableKeyword("_ALPHATEST_ON");
            result.SetOverrideTag("RenderType", "TransparentCutout");
            result.renderQueue = (int)RenderQueue.AlphaTest;
            own(result);
            cache.Add(source, result);
            Debug.LogWarning($"HSEVerse: vegetation material '{source.name}' ({source.shader?.name}) has no DOTS variant. " +
                "Created an alpha-clipped URP Lit copy with its base texture/color, normal map and standard metallic/smoothness settings. " +
                "Custom wind, subsurface shading and nonstandard packed maps are not reproduced.", source);
            return result;
        }
    }
}
