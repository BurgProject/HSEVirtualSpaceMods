// Layer blending follows URP 17 TerrainLit: RGBA control maps, mask remap,
// density blending and (up to four layers) height blending. Geometry is an ordinary mesh.
#ifndef HSE_TERRAIN_INPUT_INCLUDED
#define HSE_TERRAIN_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST, _BaseMap_TexelSize, _Control_TexelSize;
half4 _BaseColor;
half _Cutoff, _Surface, _LayerCount, _HeightBlend, _HeightTransition, _AddPass;
float4 _LayerST0;
half4 _Diffuse0, _MaskScale0, _MaskOffset0, _Params0;
half _AlphaSmoothness0;
float4 _LayerST1;
half4 _Diffuse1, _MaskScale1, _MaskOffset1, _Params1;
half _AlphaSmoothness1;
float4 _LayerST2;
half4 _Diffuse2, _MaskScale2, _MaskOffset2, _Params2;
half _AlphaSmoothness2;
float4 _LayerST3;
half4 _Diffuse3, _MaskScale3, _MaskOffset3, _Params3;
half _AlphaSmoothness3;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END
TEXTURE2D(_Control); SAMPLER(sampler_Control);
TEXTURE2D(_Splat0); TEXTURE2D(_Normal0); TEXTURE2D(_Mask0);
TEXTURE2D(_Splat1); TEXTURE2D(_Normal1); TEXTURE2D(_Mask1);
TEXTURE2D(_Splat2); TEXTURE2D(_Normal2); TEXTURE2D(_Mask2);
TEXTURE2D(_Splat3); TEXTURE2D(_Normal3); TEXTURE2D(_Mask3);
SAMPLER(sampler_Splat0); SAMPLER(sampler_Normal0); SAMPLER(sampler_Mask0);

void HseSurface(float2 uv, out SurfaceData surface, out half groupWeight)
{
    surface = (SurfaceData)0;
    half4 weights = SAMPLE_TEXTURE2D(_Control, sampler_Control,
        (uv * (_Control_TexelSize.zw - 1) + 0.5) * _Control_TexelSize.xy);
    half4 diffuse[4], masks[4];
    half3 normals[4];
    {
        float2 layerUV = uv * _LayerST0.xy + _LayerST0.zw;
        diffuse[0] = SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, layerUV);
        masks[0] = lerp(half4(0.5,0.5,0.5,0.5), SAMPLE_TEXTURE2D(_Mask0, sampler_Mask0, layerUV), _Params0.w) * _MaskScale0 + _MaskOffset0;
        normals[0] = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal0, sampler_Normal0, layerUV), _Params0.z);
    }
    {
        float2 layerUV = uv * _LayerST1.xy + _LayerST1.zw;
        diffuse[1] = SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, layerUV);
        masks[1] = lerp(half4(0.5,0.5,0.5,0.5), SAMPLE_TEXTURE2D(_Mask1, sampler_Mask0, layerUV), _Params1.w) * _MaskScale1 + _MaskOffset1;
        normals[1] = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal1, sampler_Normal0, layerUV), _Params1.z);
    }
    {
        float2 layerUV = uv * _LayerST2.xy + _LayerST2.zw;
        diffuse[2] = SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, layerUV);
        masks[2] = lerp(half4(0.5,0.5,0.5,0.5), SAMPLE_TEXTURE2D(_Mask2, sampler_Mask0, layerUV), _Params2.w) * _MaskScale2 + _MaskOffset2;
        normals[2] = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal2, sampler_Normal0, layerUV), _Params2.z);
    }
    {
        float2 layerUV = uv * _LayerST3.xy + _LayerST3.zw;
        diffuse[3] = SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, layerUV);
        masks[3] = lerp(half4(0.5,0.5,0.5,0.5), SAMPLE_TEXTURE2D(_Mask3, sampler_Mask0, layerUV), _Params3.w) * _MaskScale3 + _MaskOffset3;
        normals[3] = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal3, sampler_Normal0, layerUV), _Params3.z);
    }

    if (_LayerCount <= 4)
    {
        if (_HeightBlend > 0.5)
        {
            half4 heights = half4(masks[0].b, masks[1].b, masks[2].b, masks[3].b) * weights;
            half highest = max(max(heights.x, heights.y), max(heights.z, heights.w));
            weights *= max(0, heights + max(_HeightTransition, 1e-5) - highest) + 1e-6;
            weights /= max(dot(weights, half4(1,1,1,1)), 1e-6);
        }
        else
        {
            half4 density = saturate((half4(diffuse[0].a, diffuse[1].a, diffuse[2].a, diffuse[3].a) - (1 - weights)) * 20);
            density += 0.001 * weights;
            weights = lerp(density, weights, half4(_Diffuse0.w, _Diffuse1.w, _Diffuse2.w, _Diffuse3.w));
        }
    }
    groupWeight = dot(weights, half4(1,1,1,1));
    // With density blending, Unity normalizes the single group's contribution.
    if (_LayerCount <= 4) groupWeight = 1;
    weights /= max(dot(weights, half4(1,1,1,1)), 1e-5);
    half3 normal = half3(0,0,1e-5);
    surface.albedo += weights[0] * diffuse[0].rgb * _Diffuse0.rgb;
    surface.metallic += weights[0] * lerp(_Params0.x, masks[0].r, _Params0.w);
    surface.smoothness += weights[0] * lerp(lerp(_Params0.y, diffuse[0].a, _AlphaSmoothness0), masks[0].a, _Params0.w);
    surface.occlusion += weights[0] * lerp(_MaskScale0.g + _MaskOffset0.g, masks[0].g, _Params0.w);
    normal += weights[0] * normals[0];
    surface.albedo += weights[1] * diffuse[1].rgb * _Diffuse1.rgb;
    surface.metallic += weights[1] * lerp(_Params1.x, masks[1].r, _Params1.w);
    surface.smoothness += weights[1] * lerp(lerp(_Params1.y, diffuse[1].a, _AlphaSmoothness1), masks[1].a, _Params1.w);
    surface.occlusion += weights[1] * lerp(_MaskScale1.g + _MaskOffset1.g, masks[1].g, _Params1.w);
    normal += weights[1] * normals[1];
    surface.albedo += weights[2] * diffuse[2].rgb * _Diffuse2.rgb;
    surface.metallic += weights[2] * lerp(_Params2.x, masks[2].r, _Params2.w);
    surface.smoothness += weights[2] * lerp(lerp(_Params2.y, diffuse[2].a, _AlphaSmoothness2), masks[2].a, _Params2.w);
    surface.occlusion += weights[2] * lerp(_MaskScale2.g + _MaskOffset2.g, masks[2].g, _Params2.w);
    normal += weights[2] * normals[2];
    surface.albedo += weights[3] * diffuse[3].rgb * _Diffuse3.rgb;
    surface.metallic += weights[3] * lerp(_Params3.x, masks[3].r, _Params3.w);
    surface.smoothness += weights[3] * lerp(lerp(_Params3.y, diffuse[3].a, _AlphaSmoothness3), masks[3].a, _Params3.w);
    surface.occlusion += weights[3] * lerp(_MaskScale3.g + _MaskOffset3.g, masks[3].g, _Params3.w);
    normal += weights[3] * normals[3];
    surface.normalTS = normalize(normal);
    surface.alpha = 1;
}
void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    half weight;
    HseSurface(uv, surface, weight);
}
#endif

