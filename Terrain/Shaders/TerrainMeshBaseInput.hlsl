// Far LODs: the blended terrain surface baked once (TerrainBaseBake.shader) into a colour map (smoothness in alpha)
// and a two-channel tangent-space normal map - two samples per pixel instead of the per-layer work.
#ifndef HSE_TERRAIN_BASE_INPUT_INCLUDED
#define HSE_TERRAIN_BASE_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST, _BaseMap_TexelSize;
half4 _BaseColor;
half _Cutoff, _Surface;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END
TEXTURE2D(_BaseAlbedo); SAMPLER(sampler_BaseAlbedo);
TEXTURE2D(_BaseNormal); SAMPLER(sampler_BaseNormal);

void HseSurface(float2 uv, float2 dx, float2 dy, out SurfaceData surface, out half groupWeight)
{
    surface = (SurfaceData)0;
    groupWeight = 1;
    half4 c = SAMPLE_TEXTURE2D(_BaseAlbedo, sampler_BaseAlbedo, uv);
    half2 n = SAMPLE_TEXTURE2D(_BaseNormal, sampler_BaseNormal, uv).rg * 2 - 1;
    surface.albedo = c.rgb;
    surface.smoothness = c.a;
    surface.occlusion = 1;
    surface.normalTS = half3(n, sqrt(saturate(1 - dot(n, n))));
    surface.alpha = 1;
}

void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    half weight;
    HseSurface(uv, 0, 0, surface, weight);
}
#endif
