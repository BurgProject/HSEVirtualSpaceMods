// Editor-only (manual conversion): decodes a TerrainLayer normal map whatever its import encoding and writes the
// tangent-space X/Y into RG, so every slice of the normal array can be the same two-channel format.
Shader "Hidden/HSEVerse/Terrain Normal Repack"
{
    Properties
    {
        _MainTex("Normal map", 2D) = "bump" {}
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = UnpackNormalScale(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv), 1);
                return half4(n.xy * 0.5 + 0.5, 0, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
