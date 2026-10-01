// Rebuilt from the original GLES3 program (G:\Game_2D\APK\ShaderGLSL\Shader Graphs_SH_VO_Background.glsl).
// Unlit graph: world-XY texture (tiled + rotated) masked by camera-facing normal, blended between
// Color_A and Color_B, multiplied by a screen-space vignette.
Shader "Shader Graphs/SH_VO_Background"
{
    Properties
    {
        [NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
        _Texture_Tiling ("Texture_Tiling", Float) = 1
        _Texture_Rotation ("Texture_Rotation", Range(0, 360)) = 0
        _Color_A ("Color_A", Color) = (1,1,1,0)
        _Color_B ("Color_B", Color) = (0,0,0,0)
        _Vignette_Edge_1 ("Vignette_Edge_1", Float) = 0
        _Vignette_Edge_2 ("Vignette_Edge_2", Float) = 1
        _Vignette_Color ("Vignette_Color", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Include/SnackFakeUnlit.hlsl"

        TEXTURE2D(_Texture); SAMPLER(sampler_Texture);
        CBUFFER_START(UnityPerMaterial)
            float _Texture_Tiling;
            float _Texture_Rotation;
            float4 _Color_A;
            float4 _Color_B;
            float _Vignette_Edge_1;
            float _Vignette_Edge_2;
            float4 _Vignette_Color;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back ZWrite On ZTest LEqual

            HLSLPROGRAM
            #pragma vertex FakeUnlitVert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(FakeUnlitVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float vignette = 1.0 - FakeUnlitScreenRadius(input.positionCS);
                vignette = smoothstep(0.0, 1.0, saturate((vignette - _Vignette_Edge_1) / (_Vignette_Edge_2 - _Vignette_Edge_1)));
                float3 vignetteColor = lerp(_Vignette_Color.rgb, 1.0, vignette);

                float2 uv = FakeUnlitRotateUv(input.positionWS.xy, _Texture_Tiling, _Texture_Rotation);
                float3 pattern = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, uv).rgb;
                float facing = saturate(-normalize(input.normalWS).z);
                float3 color = lerp(_Color_A.rgb, _Color_B.rgb, pattern * facing);

                return half4(color * vignetteColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex FakeUnlitVert
            #pragma fragment FakeUnlitDepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
