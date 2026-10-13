// Rebuilt from the original GLES3 program (G:\Game_2D\APK\ShaderGLSL\Shader Graphs_SH_FakeUnlit_Curtain_Shutter.glsl).
// Wooden planks of a curtain (rèm), sampled in world XY (the mesh has no UV), lit by how much the face looks up the
// board (-Z). The shutter rolls up from _MinY: rows below it are cut away, a soft shadow runs along its edge and the
// planks slide with it. _MinY / _MaxY are heights relative to _ParentWorldPos (the curtain group), _Shutter 0..1.
Shader "Shader Graphs/SH_FakeUnlit_Curtain_Shutter"
{
    Properties
    {
        [NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
        _Texture_Tiling ("Texture_Tiling", Float) = 1
        _FlashStrength ("FlashStrength", Range(0, 1)) = 0
        _FlashColor ("FlashColor", Color) = (1,0.9333333,0.6941177,0)
        _AlphaDither ("AlphaDither", Float) = 0
        _Shutter ("Shutter", Float) = 0
        _ShadowColor ("ShadowColor", Color) = (0.3207547,0.108533,0.0257209,0)
        _ShadowSize ("ShadowSize", Float) = 0.05
        _ShadowEdge_1 ("ShadowEdge_1", Float) = 0
        _ShadowEdge_2 ("ShadowEdge_2", Float) = 1
        _MinY ("MinY", Float) = -10
        _MaxY ("MaxY", Float) = 10
        _ParentWorldPos ("ParentWorldPos", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Include/SnackFakeUnlit.hlsl"

        TEXTURE2D(_Texture); SAMPLER(sampler_Texture);
        CBUFFER_START(UnityPerMaterial)
            float _Texture_Tiling;
            float4 _FlashColor;
            float _ShadowEdge_2;
            float _FlashStrength;
            float _AlphaDither;
            float _Shutter;
            float _MinY;
            float _MaxY;
            float4 _ParentWorldPos;
            float4 _ShadowColor;
            float _ShadowSize;
            float _ShadowEdge_1;
        CBUFFER_END

        // Height inside the group (0 at _MinY, 1 at _MaxY) above the shutter line; <= 0 is rolled away.
        float ShutterHeight(float3 positionWS)
        {
            return (positionWS.y - _ParentWorldPos.y - _MinY) / (_MaxY - _MinY) - _Shutter;
        }

        half PlankR(float3 positionWS)
        {
            float2 uv = positionWS.xy * _Texture_Tiling - float2(0.0, (_MaxY - _MinY) * _Shutter);
            return SAMPLE_TEXTURE2D(_Texture, sampler_Texture, uv).r;
        }
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

                float t = ShutterHeight(input.positionWS);
                float2 uv = input.positionWS.xy * _Texture_Tiling - float2(0.0, (_MaxY - _MinY) * _Shutter);
                half3 tex = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, uv).rgb;
                clip(tex.r * saturate(t) - 0.01);

                half facing = saturate(-normalize(input.normalWS).z);
                half3 colour = facing * tex + _FlashStrength * _FlashColor.rgb;
                half edge = smoothstep(_ShadowEdge_1, _ShadowEdge_2, saturate(t / _ShadowSize));
                colour *= lerp(_ShadowColor.rgb, 1.0, edge);

                return half4(FakeUnlitShade(input, colour, 0.0), 1.0);
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
                clip(PlankR(input.positionWS) * saturate(ShutterHeight(input.positionWS)) - 0.01);
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
                clip(PlankR(input.positionWS) * saturate(ShutterHeight(input.positionWS)) - 0.01);
                return FakeUnlitDepthNormalsFrag(input);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
