// Rebuilt from the original GLES3 program (G:\Game_2D\APK\ShaderGLSL\Custom_URP_GradientMapUnlit.glsl).
// Rec.601 luminance of _MainTex (optionally inverted) is remapped through up to 8 colour stops.
Shader "Custom/URP/GradientMapUnlit"
{
    Properties
    {
        _MainTex ("Grayscale Texture", 2D) = "white" {}
        [Toggle] _Invert ("Invert Input", Float) = 0
        _StopCount ("Stop Count", Range(2, 8)) = 2
        [HDR] _Color0 ("Color 0", Color) = (0,0,0,1)
        _Position0 ("Position 0", Range(0, 1)) = 0
        [HDR] _Color1 ("Color 1", Color) = (1,1,1,1)
        _Position1 ("Position 1", Range(0, 1)) = 1
        [HDR] _Color2 ("Color 2", Color) = (1,1,1,1)
        _Position2 ("Position 2", Range(0, 1)) = 0.5
        [HDR] _Color3 ("Color 3", Color) = (1,1,1,1)
        _Position3 ("Position 3", Range(0, 1)) = 0.5
        [HDR] _Color4 ("Color 4", Color) = (1,1,1,1)
        _Position4 ("Position 4", Range(0, 1)) = 0.5
        [HDR] _Color5 ("Color 5", Color) = (1,1,1,1)
        _Position5 ("Position 5", Range(0, 1)) = 0.5
        [HDR] _Color6 ("Color 6", Color) = (1,1,1,1)
        _Position6 ("Position 6", Range(0, 1)) = 0.5
        [HDR] _Color7 ("Color 7", Color) = (1,1,1,1)
        _Position7 ("Position 7", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Cull Back ZWrite On ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Invert;
                float _StopCount;
                float4 _Color0, _Color1, _Color2, _Color3, _Color4, _Color5, _Color6, _Color7;
                float _Position0, _Position1, _Position2, _Position3, _Position4, _Position5, _Position6, _Position7;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float value = saturate(dot(tex.rgb, float3(0.298999995, 0.587000012, 0.114)));
                value = lerp(value, 1.0 - value, _Invert);

                float4 colors[8] = { _Color0, _Color1, _Color2, _Color3, _Color4, _Color5, _Color6, _Color7 };
                float positions[8] = { _Position0, _Position1, _Position2, _Position3, _Position4, _Position5, _Position6, _Position7 };
                int count = (int)clamp(_StopCount, 2.0, 8.0);

                float4 color = _Color0;
                if (value > _Position0)
                {
                    if (value >= positions[count - 1])
                    {
                        color = colors[count - 1];
                    }
                    else
                    {
                        [unroll]
                        for (int i = 0; i < 7; i++)
                        {
                            if (i + 1 < count && value >= positions[i] && value <= positions[i + 1])
                            {
                                float t = saturate((value - positions[i]) / max(positions[i + 1] - positions[i], 9.99999997e-07));
                                color = lerp(colors[i], colors[i + 1], t);
                                break;
                            }
                        }
                    }
                }
                return half4(color.rgb, tex.a * color.a);
            }
            ENDHLSL
        }
    }
}
