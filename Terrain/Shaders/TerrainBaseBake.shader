// Editor-only (manual conversion): renders the blended terrain surface in terrain-UV space. Pass 0 writes colour and
// smoothness, pass 1 the tangent-space normal packed into RG. Driven by Graphics.Blit with the array material's
// properties; the explicit derivative is one texel of the target, so layers are sampled at the mip that texel needs.
Shader "Hidden/HSEVerse/Terrain Base Bake"
{
    Properties
    {
        _Control("Splat weights", 2DArray) = "" {}
        _ControlSize("Splat map size", Float) = 512
        _Albedo("Layer albedo", 2DArray) = "" {}
        _Normals("Layer normals", 2DArray) = "" {}
        _Masks("Layer masks", 2DArray) = "" {}
        _LayerParams("Layer parameters", 2D) = "black" {}
        _LayerCount("Total layers", Float) = 1
        _HeightBlend("Height blend", Float) = 0
        _HeightTransition("Height transition", Float) = 0.1
        _BakeTexel("Target texel", Float) = 0.0009765625
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        HLSLINCLUDE
        #pragma target 3.5
        #pragma shader_feature_local_fragment _ _HSE_NORMALS
        #pragma shader_feature_local_fragment _ _HSE_MASKS
        #include "TerrainMeshArrayInput.hlsl"
        float _BakeTexel;
        struct BakeAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct BakeVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
        BakeVaryings BakeVertex(BakeAttributes input)
        {
            BakeVaryings o;
            o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            o.uv = input.uv;
            return o;
        }
        SurfaceData BakeSurface(float2 uv)
        {
            SurfaceData surface; half weight;
            HseSurface(uv, float2(_BakeTexel, 0), float2(0, _BakeTexel), surface, weight);
            return surface;
        }
        ENDHLSL
        Pass
        {
            Name "BakeAlbedo"
            HLSLPROGRAM
            #pragma vertex BakeVertex
            #pragma fragment Frag
            half4 Frag(BakeVaryings input) : SV_Target
            {
                SurfaceData s = BakeSurface(input.uv);
                return half4(s.albedo, s.smoothness);
            }
            ENDHLSL
        }
        Pass
        {
            Name "BakeNormal"
            HLSLPROGRAM
            #pragma vertex BakeVertex
            #pragma fragment Frag
            half4 Frag(BakeVaryings input) : SV_Target
            {
                SurfaceData s = BakeSurface(input.uv);
                return half4(s.normalTS.xy * 0.5 + 0.5, 0, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
