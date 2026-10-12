// Rebuilt from the original GLES3 program (G:\Game_2D\APK\ShaderGLSL\Shader Graphs_SH_FX_Particle_Dissolve_Additive.glsl).
// Additive particle (SrcAlpha One); the dissolve mask fades alpha smoothly (no step).
Shader "Shader Graphs/SH_FX_Particle_Dissolve_Additive"
{
    Properties
    {
        [NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
        [NoScaleOffset] _Dissolve_Texture ("Dissolve_Texture", 2D) = "white" {}
        _Dissolve_Tiling ("Dissolve_Tiling", Vector) = (1,1,0,0)
        _Dissolve_Speed ("Dissolve_Speed", Vector) = (0,0,0,0)
        _Dissolve_Smoothness ("Dissolve_Smoothness", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off ZTest LEqual Cull Off

            HLSLPROGRAM
            #pragma vertex DissolveVert
            #pragma fragment Frag
            #include "Include/SnackParticleDissolve.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float2 _Dissolve_Tiling;
                float2 _Dissolve_Speed;
                float _Dissolve_Smoothness;
            CBUFFER_END

            half4 Frag(DissolveVaryings input) : SV_Target
            {
                float2 uv = input.uv0.xy * _Dissolve_Tiling + _Time.y * _Dissolve_Speed;
                float noise = SAMPLE_TEXTURE2D(_Dissolve_Texture, sampler_Dissolve_Texture, uv).r;
                half4 color = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, input.uv0.xy) * input.color;
                color.a *= DissolveMask(noise, input.uv1.x, _Dissolve_Smoothness);
                return color;
            }
            ENDHLSL
        }
    }
}
