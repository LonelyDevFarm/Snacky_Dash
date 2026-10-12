// Rebuilt from the original GLES3 program (G:\Game_2D\APK\ShaderGLSL\Shader Graphs_SH_VO_Tile.glsl).
// Unlit graph: colour gradient along object-space Z (plus an optional "peak" gradient) and a
// highlight driven by the world-space normal Y.
Shader "Shader Graphs/SH_VO_Tile"
{
    Properties
    {
        _ColorNear ("ColorNear", Color) = (1,0,0,0)
        _ColorFar ("ColorFar", Color) = (0.7169812,0,0,0)
        _GradientStart ("GradientStart", Float) = -1
        _GradientEnd ("GradientEnd", Float) = 1
        _GradientSmoothness ("GradientSmoothness", Float) = 0.5
        _HighlightColor ("HighlightColor", Color) = (1,1,1,0)
        _HighlightThreshold ("HighlightThreshold", Float) = 0.5
        _HighlightSoftness ("HighlightSoftness", Float) = 0.25
        _HighlightIntensity ("HighlightIntensity", Float) = 1
        _GradientEndPeak ("GradientEndPeak", Float) = 1
        _GradientStartPeak ("GradientStartPeak", Float) = -1
        _GradientSmoothnessPeak ("GradientSmoothnessPeak", Float) = 0.5
        _PeakStrength ("PeakStrength", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Include/SnackFakeUnlit.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _ColorNear;
            float4 _ColorFar;
            float _GradientStart;
            float _GradientEnd;
            float _GradientSmoothness;
            float4 _HighlightColor;
            float _HighlightThreshold;
            float _HighlightSoftness;
            float _HighlightIntensity;
            float _GradientEndPeak;
            float _GradientStartPeak;
            float _GradientSmoothnessPeak;
            float _PeakStrength;
        CBUFFER_END

        // Normalised position between start and end (reversed when start >= end), then a
        // smoothstep window of width `smoothness` centred on 0.5.
        float VoTileGradient(float value, float start, float end, float smoothness)
        {
            float t = saturate((value - start) / max(abs(end - start), 9.99999975e-05));
            if (start >= end)
                t = 1.0 - t;
            float halfWidth = max(smoothness * 0.5, 9.99999975e-05);
            return smoothstep(0.0, 1.0, saturate((t - (0.5 - halfWidth)) / (2.0 * halfWidth)));
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

            half4 Frag(FakeUnlitVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float objectZ = TransformWorldToObject(input.positionWS).z;
                float gradient = VoTileGradient(objectZ, _GradientStart, _GradientEnd, _GradientSmoothness)
                               + _PeakStrength * VoTileGradient(objectZ, _GradientStartPeak, _GradientEndPeak, _GradientSmoothnessPeak);
                float3 color = lerp(_ColorNear.rgb, _ColorFar.rgb, gradient);

                float normalY = normalize(input.normalWS).y;
                float lower = _HighlightThreshold - _HighlightSoftness;
                float highlight = smoothstep(0.0, 1.0, saturate((normalY - lower) / (2.0 * _HighlightSoftness)));
                highlight = saturate(highlight * _HighlightIntensity);
                color = lerp(color, _HighlightColor.rgb, highlight);

                return half4(color, 1.0);
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
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
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
