// All TerrainLayers in ONE pass: albedo / normal / mask maps are slices of three texture arrays, the per-4-layer
// splat maps are slices of a fourth, and the per-layer numbers live in a small float texture (row per parameter).
// Blending follows URP 17 TerrainLit like TerrainMeshInput.hlsl: mask remap, density blending and (up to four
// layers) height blending; above four layers the weights are used as painted. Layers with no weight at a pixel are
// not sampled at all, and the normal / mask arrays only exist in the variants that need them.
#ifndef HSE_TERRAIN_ARRAY_INPUT_INCLUDED
#define HSE_TERRAIN_ARRAY_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST, _BaseMap_TexelSize;
half4 _BaseColor;
half _Cutoff, _Surface, _LayerCount, _HeightBlend, _HeightTransition;
float _ControlSize;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END
TEXTURE2D_ARRAY(_Control); SAMPLER(sampler_Control);
TEXTURE2D_ARRAY(_Albedo); SAMPLER(sampler_Albedo);
TEXTURE2D_ARRAY(_Normals); SAMPLER(sampler_Normals);
TEXTURE2D_ARRAY(_Masks); SAMPLER(sampler_Masks);
TEXTURE2D(_LayerParams);

// rows of _LayerParams (one column per layer)
#define HSE_ROW_UV 0          // tiling xy, offset zw
#define HSE_ROW_DIFFUSE 1     // diffuse remap rgb, density-blend factor w
#define HSE_ROW_MASK_SCALE 2
#define HSE_ROW_MASK_OFFSET 3
#define HSE_ROW_PARAMS 4      // metallic, smoothness, normal scale, has mask
#define HSE_ROW_EXTRA 5       // smoothness from albedo alpha

float4 HseParam(int layer, int row) { return LOAD_TEXTURE2D(_LayerParams, int2(layer, row)); }

struct HseLayer
{
    half4 diffuse;
    half4 mask;
    half3 normal;
};

HseLayer HseEmptyLayer()
{
    HseLayer l;
    l.diffuse = 0; l.mask = 0; l.normal = half3(0, 0, 1);
    return l;
}

HseLayer HseSampleLayer(int layer, float2 uv, float2 dx, float2 dy)
{
    float4 st = HseParam(layer, HSE_ROW_UV);
    float2 luv = uv * st.xy + st.zw;
    float2 ldx = dx * st.xy, ldy = dy * st.xy;
    float4 p = HseParam(layer, HSE_ROW_PARAMS);
    HseLayer l;
    l.diffuse = SAMPLE_TEXTURE2D_ARRAY_GRAD(_Albedo, sampler_Albedo, luv, layer, ldx, ldy);
#if defined(_HSE_MASKS)
    half4 m = SAMPLE_TEXTURE2D_ARRAY_GRAD(_Masks, sampler_Masks, luv, layer, ldx, ldy);
#else
    half4 m = half4(0.5, 0.5, 0.5, 0.5);
#endif
    l.mask = lerp(half4(0.5, 0.5, 0.5, 0.5), m, p.w) * HseParam(layer, HSE_ROW_MASK_SCALE) + HseParam(layer, HSE_ROW_MASK_OFFSET);
#if defined(_HSE_NORMALS)
    half2 n = (SAMPLE_TEXTURE2D_ARRAY_GRAD(_Normals, sampler_Normals, luv, layer, ldx, ldy).rg * 2 - 1) * p.z;
    l.normal = half3(n, sqrt(saturate(1 - dot(n, n))));
#else
    l.normal = half3(0, 0, 1);
#endif
    return l;
}

void HseAccumulate(int layer, half w, HseLayer l, inout SurfaceData surface, inout half3 normal)
{
    float4 p = HseParam(layer, HSE_ROW_PARAMS);
    float4 diffuse = HseParam(layer, HSE_ROW_DIFFUSE);
    half occlusionDefault = HseParam(layer, HSE_ROW_MASK_SCALE).g + HseParam(layer, HSE_ROW_MASK_OFFSET).g;
    half alphaSmoothness = HseParam(layer, HSE_ROW_EXTRA).x;
    surface.albedo += w * l.diffuse.rgb * diffuse.rgb;
    surface.metallic += w * lerp(p.x, l.mask.r, p.w);
    surface.smoothness += w * lerp(lerp(p.y, l.diffuse.a, alphaSmoothness), l.mask.a, p.w);
    surface.occlusion += w * lerp(occlusionDefault, l.mask.g, p.w);
    normal += w * l.normal;
}

// dx / dy: the screen-space derivatives of uv (explicit, so layers can be skipped inside branches)
void HseSurface(float2 uv, float2 dx, float2 dy, out SurfaceData surface, out half groupWeight)
{
    surface = (SurfaceData)0;
    groupWeight = 1;
    half3 normal = half3(0, 0, 1e-5);
    int count = (int)_LayerCount;
    float2 cuv = (uv * (_ControlSize - 1) + 0.5) / _ControlSize;
    if (count <= 4)
    {
        half4 w = SAMPLE_TEXTURE2D_ARRAY_LOD(_Control, sampler_Control, cuv, 0, 0);
        HseLayer l[4];
        [unroll] for (int i = 0; i < 4; i++)
        {
            if (i < count && w[i] > 0.0001) l[i] = HseSampleLayer(i, uv, dx, dy);
            else { l[i] = HseEmptyLayer(); w[i] = 0; }
        }
        if (_HeightBlend > 0.5)
        {
            half4 heights = half4(l[0].mask.b, l[1].mask.b, l[2].mask.b, l[3].mask.b) * w;
            half highest = max(max(heights.x, heights.y), max(heights.z, heights.w));
            w *= max(0, heights + max(_HeightTransition, 1e-5) - highest) + 1e-6;
            w /= max(dot(w, half4(1, 1, 1, 1)), 1e-6);
        }
        else
        {
            half4 density = saturate((half4(l[0].diffuse.a, l[1].diffuse.a, l[2].diffuse.a, l[3].diffuse.a) - (1 - w)) * 20);
            density += 0.001 * w;
            half4 factor = half4(HseParam(0, HSE_ROW_DIFFUSE).w, HseParam(1, HSE_ROW_DIFFUSE).w, HseParam(2, HSE_ROW_DIFFUSE).w, HseParam(3, HSE_ROW_DIFFUSE).w);
            w = lerp(density, w, factor);
        }
        w /= max(dot(w, half4(1, 1, 1, 1)), 1e-5);
        [unroll] for (int j = 0; j < 4; j++)
            if (w[j] > 0) HseAccumulate(j, w[j], l[j], surface, normal);
    }
    else
    {
        half total = 0;
        int groups = (count + 3) / 4;
        [loop] for (int g = 0; g < groups; g++)
        {
            half4 w = SAMPLE_TEXTURE2D_ARRAY_LOD(_Control, sampler_Control, cuv, g, 0);
            [unroll] for (int i = 0; i < 4; i++)
            {
                int layer = g * 4 + i;
                if (layer < count && w[i] > 0.0001)
                {
                    HseAccumulate(layer, w[i], HseSampleLayer(layer, uv, dx, dy), surface, normal);
                    total += w[i];
                }
            }
        }
        half inv = 1 / max(total, 1e-5);
        surface.albedo *= inv; surface.metallic *= inv; surface.smoothness *= inv; surface.occlusion *= inv;
    }
    surface.normalTS = normalize(normal);
    surface.alpha = 1;
}

void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    half weight;
    HseSurface(uv, ddx(uv), ddy(uv), surface, weight);
}
#endif
