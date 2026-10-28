Shader "HSEVerse/Terrain Mesh"
{
    Properties
    {
        [HideInInspector] _BaseMap("Base", 2D) = "white" {}
        [HideInInspector] _BaseColor("Base color", Color) = (1,1,1,1)
        [HideInInspector] _Cutoff("Cutoff", Float) = 0
        [HideInInspector] _Surface("Surface", Float) = 0
        _Control("Splat weights", 2D) = "red" {}
        _LayerCount("Total layers", Float) = 1
        _HeightBlend("Height blend", Float) = 0
        _HeightTransition("Height transition", Float) = 0.1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _ZWrite("Depth write", Float) = 1
        [HideInInspector] _AddPass("Additional group", Float) = 0
        _Splat0("Layer 0 albedo", 2D) = "white" {}
        _Normal0("Layer 0 normal", 2D) = "bump" {}
        _Mask0("Layer 0 mask", 2D) = "gray" {}
        _LayerST0("Layer 0 UV", Vector) = (1,1,0,0)
        _Diffuse0("Layer 0 tint/density", Vector) = (1,1,1,1)
        _MaskScale0("Layer 0 mask scale", Vector) = (1,1,1,1)
        _MaskOffset0("Layer 0 mask offset", Vector) = (0,0,0,0)
        _Params0("Layer 0 metallic/smoothness/normal/mask", Vector) = (0,0,0,0)
        _AlphaSmoothness0("Layer 0 alpha smoothness", Float) = 0
        _Splat1("Layer 1 albedo", 2D) = "white" {}
        _Normal1("Layer 1 normal", 2D) = "bump" {}
        _Mask1("Layer 1 mask", 2D) = "gray" {}
        _LayerST1("Layer 1 UV", Vector) = (1,1,0,0)
        _Diffuse1("Layer 1 tint/density", Vector) = (1,1,1,1)
        _MaskScale1("Layer 1 mask scale", Vector) = (1,1,1,1)
        _MaskOffset1("Layer 1 mask offset", Vector) = (0,0,0,0)
        _Params1("Layer 1 metallic/smoothness/normal/mask", Vector) = (0,0,0,0)
        _AlphaSmoothness1("Layer 1 alpha smoothness", Float) = 0
        _Splat2("Layer 2 albedo", 2D) = "white" {}
        _Normal2("Layer 2 normal", 2D) = "bump" {}
        _Mask2("Layer 2 mask", 2D) = "gray" {}
        _LayerST2("Layer 2 UV", Vector) = (1,1,0,0)
        _Diffuse2("Layer 2 tint/density", Vector) = (1,1,1,1)
        _MaskScale2("Layer 2 mask scale", Vector) = (1,1,1,1)
        _MaskOffset2("Layer 2 mask offset", Vector) = (0,0,0,0)
        _Params2("Layer 2 metallic/smoothness/normal/mask", Vector) = (0,0,0,0)
        _AlphaSmoothness2("Layer 2 alpha smoothness", Float) = 0
        _Splat3("Layer 3 albedo", 2D) = "white" {}
        _Normal3("Layer 3 normal", 2D) = "bump" {}
        _Mask3("Layer 3 mask", 2D) = "gray" {}
        _LayerST3("Layer 3 UV", Vector) = (1,1,0,0)
        _Diffuse3("Layer 3 tint/density", Vector) = (1,1,1,1)
        _MaskScale3("Layer 3 mask scale", Vector) = (1,1,1,1)
        _MaskOffset3("Layer 3 mask offset", Vector) = (0,0,0,0)
        _Params3("Layer 3 metallic/smoothness/normal/mask", Vector) = (0,0,0,0)
        _AlphaSmoothness3("Layer 3 alpha smoothness", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry-100" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma vertex LitPassVertex
            #pragma fragment HseFragment
            #define _NORMALMAP 1
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include "TerrainMeshInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            half4 HseFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;
                half weight;
                HseSurface(input.uv, surface, weight);
                if (_AddPass > 0.5) clip(weight - 0.005);
                InputData lighting;
                InitializeInputData(input, surface.normalTS, lighting);
                InitializeBakedGIData(input, lighting);
                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, lighting.fogCoord) * weight;
                color.a = 1;
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "TerrainMeshInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "TerrainMeshInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma vertex LitPassVertex
            #pragma fragment HseDepthNormals
            #define _NORMALMAP 1
            #include "TerrainMeshInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            half4 HseDepthNormals(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SurfaceData surface; half weight;
                HseSurface(input.uv, surface, weight);
                InputData lighting;
                InitializeInputData(input, surface.normalTS, lighting);
                return half4(NormalizeNormalPerPixel(lighting.normalWS), 0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #include "TerrainMeshInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}

