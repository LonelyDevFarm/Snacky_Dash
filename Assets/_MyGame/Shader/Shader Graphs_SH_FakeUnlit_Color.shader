// Rebuilt from the original GLES3 program (G:\Game_2D\APK\ShaderGLSL\Shader Graphs_SH_FakeUnlit_Color.glsl).
// Emission = _Color + flash colour; specular from Smoothness. Colour properties are m_Type 0 (Color)
// in the original serialized shader, so they are sRGB-converted like any Color property.
Shader "Shader Graphs/SH_FakeUnlit_Color"
{
    Properties
    {
        _Color ("Color", Color) = (0,0.8915043,1,0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        _FlashStrength ("FlashStrength", Range(0, 1)) = 0
        _FlashColor ("FlashColor", Color) = (1,0.9333333,0.6941177,0)
        _AlphaDither ("AlphaDither", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Include/SnackFakeUnlit.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Smoothness;
            float _FlashStrength;
            float4 _FlashColor;
            float _AlphaDither;
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            half4 Frag(FakeUnlitVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                FakeUnlitDitherClip(input.positionCS, _AlphaDither);
                half3 emission = _Color.rgb + _FlashStrength * _FlashColor.rgb;
                return half4(FakeUnlitShade(input, emission, _Smoothness), 1.0);
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
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(FakeUnlitVaryings input) : SV_Target
            {
                FakeUnlitDitherClip(input.positionCS, _AlphaDither);
                return FakeUnlitDepthFrag(input);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Back ZWrite On

            HLSLPROGRAM
            #pragma vertex FakeUnlitVert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(FakeUnlitVaryings input) : SV_Target
            {
                FakeUnlitDitherClip(input.positionCS, _AlphaDither);
                return FakeUnlitDepthNormalsFrag(input);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
