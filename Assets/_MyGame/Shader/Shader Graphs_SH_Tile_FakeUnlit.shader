// Rebuilt from the original GLES3 program (G:\Game_2D\APK\ShaderGLSL\Shader Graphs_SH_Tile_FakeUnlit.glsl).
// Board terrain: stepped height shading along world Z, world-XY overlay on camera-facing faces,
// screen vignette and a side colour band, all routed through Emission with Smoothness = 0.
Shader "Shader Graphs/SH_Tile_FakeUnlit"
{
    Properties
    {
        _Main_Color_1 ("Main_Color_1", Color) = (1,0,0,0)
        _Main_Color_2 ("Main_Color_2", Color) = (0,0.4149446,1,0)
        _Side_Color ("Side_Color", Color) = (0.8261385,0,1,0)
        _Side_Height_Top ("Side_Height_Top", Float) = 0
        _Side_Height_Bottom ("Side_Height_Bottom", Float) = 1.4
        _Shadow_Color ("Shadow_Color", Color) = (0,0,0,0)
        _Shadow_Edge_1 ("Shadow_Edge_1", Float) = 0.2
        _Shadow_Edge_2 ("Shadow_Edge_2", Float) = 1
        _Shadow_Steps ("Shadow_Steps", Float) = 3
        _Shadow_Strength ("Shadow_Strength", Float) = 1
        [NoScaleOffset] _Overlay_Texture ("Overlay_Texture", 2D) = "white" {}
        _Overlay_Tiling ("Overlay_Tiling", Float) = 1
        _Overlay_Rotation ("Overlay_Rotation", Range(0, 360)) = 0
        _Overlay_Strength ("Overlay_Strength", Float) = 0
        _Overlay_Color ("Overlay_Color", Color) = (1,1,1,0)
        _Vignette_Color ("Vignette_Color", Color) = (1,1,1,0)
        _Vignette_Strength_1 ("Vignette_Strength_1", Float) = 0
        _Vignette_Strength_2 ("Vignette_Strength_2", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Include/SnackFakeUnlit.hlsl"

        TEXTURE2D(_Overlay_Texture); SAMPLER(sampler_Overlay_Texture);
        CBUFFER_START(UnityPerMaterial)
            float4 _Main_Color_1;
            float4 _Main_Color_2;
            float4 _Side_Color;
            float _Side_Height_Top;
            float _Side_Height_Bottom;
            float4 _Shadow_Color;
            float _Shadow_Edge_1;
            float _Shadow_Edge_2;
            float _Shadow_Steps;
            float _Shadow_Strength;
            float _Overlay_Tiling;
            float _Overlay_Rotation;
            float _Overlay_Strength;
            float4 _Overlay_Color;
            float4 _Vignette_Color;
            float _Vignette_Strength_1;
            float _Vignette_Strength_2;
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
                float3 n = normalize(input.normalWS);
                float3 p = input.positionWS;

                // Side band: fades in once the normal is no longer pointing down (y > -0.8).
                float sideFade = smoothstep(0.0, 1.0, saturate((n.y + 0.8) * 1.25));
                float sideBottom = (1.0 - n.z) >= _Side_Height_Bottom ? 1.0 : 0.0;
                float sideTop = saturate(n.x * (1.0 - n.y)) >= _Side_Height_Top ? 1.0 : 0.0;
                float3 side = sideTop * sideBottom * _Side_Color.rgb;

                // Faces looking at the camera (board normal is -Z).
                float facing = saturate(-n.z);

                float vignette = 1.0 - facing * FakeUnlitScreenRadius(input.positionCS);
                vignette = smoothstep(0.0, 1.0, saturate((vignette - _Vignette_Strength_1) / (_Vignette_Strength_2 - _Vignette_Strength_1)));
                float3 vignetteColor = lerp(_Vignette_Color.rgb, 1.0, vignette);

                float2 overlayUv = FakeUnlitRotateUv(p.xy, _Overlay_Tiling, _Overlay_Rotation);
                float3 overlay = SAMPLE_TEXTURE2D(_Overlay_Texture, sampler_Overlay_Texture, overlayUv).rgb
                               * _Overlay_Strength * _Overlay_Color.rgb;

                float height = (p.z + 1.0) * 0.5;
                float shade = smoothstep(0.0, 1.0, saturate((height - _Shadow_Edge_1) / (_Shadow_Edge_2 - _Shadow_Edge_1)));
                float stepSize = 1.0 / _Shadow_Steps;
                float stepped = floor(shade / stepSize) * stepSize;
                shade = _Shadow_Strength * (height - stepped) + stepped;
                float3 mainColor = lerp(_Main_Color_2.rgb, _Main_Color_1.rgb, shade);

                float3 emission = (facing * overlay + mainColor) * vignetteColor;
                emission = sideFade * side + emission;

                return half4(FakeUnlitShade(input, emission, 0.0), 1.0);
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

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Back ZWrite On

            HLSLPROGRAM
            #pragma vertex FakeUnlitVert
            #pragma fragment FakeUnlitDepthNormalsFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
