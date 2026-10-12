// Shared code for SH_FX_Particle_Dissolve_*, rebuilt from the original GLES3 programs
// (G:\Game_2D\APK\ShaderGLSL). Colour = _Texture * particle colour; alpha is cut by a noise texture
// against the per-particle dissolve value the particle system writes to TEXCOORD1.x (custom data).
#ifndef SNACK_PARTICLE_DISSOLVE_INCLUDED
#define SNACK_PARTICLE_DISSOLVE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_Texture); SAMPLER(sampler_Texture);
TEXTURE2D(_Dissolve_Texture); SAMPLER(sampler_Dissolve_Texture);

struct DissolveAttributes
{
    float4 positionOS : POSITION;
    float4 uv0 : TEXCOORD0;
    float4 uv1 : TEXCOORD1;
    half4 color : COLOR;
};

struct DissolveVaryings
{
    float4 positionCS : SV_POSITION;
    float4 uv0 : TEXCOORD0;
    float4 uv1 : TEXCOORD1;
    half4 color : COLOR;
};

DissolveVaryings DissolveVert(DissolveAttributes input)
{
    DissolveVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv0 = input.uv0;
    output.uv1 = input.uv1;
    output.color = input.color;
    return output;
}

// Original: smoothstep(0, 1, saturate((noise - d) / (saturate(d * smoothness) - d))), d = TEXCOORD1.x.
float DissolveMask(float noise, float dissolve, float smoothness)
{
    float edge = saturate(dissolve * smoothness) - dissolve;
    return smoothstep(0.0, 1.0, saturate((noise - dissolve) / edge));
}

#endif
