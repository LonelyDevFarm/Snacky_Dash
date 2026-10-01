// Shared code for the "FakeUnlit" family, rebuilt from the original GLES3 shader programs
// (G:\Game_2D\APK\ShaderGLSL). The original Shader Graphs are URP Lit graphs with
// BaseColor = black, Metallic = 0 and the visible colour routed through Emission, so the
// only lighting contribution is the specular term driven by Smoothness.
#ifndef SNACK_FAKE_UNLIT_INCLUDED
#define SNACK_FAKE_UNLIT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct FakeUnlitAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct FakeUnlitVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float2 uv         : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

FakeUnlitVaryings FakeUnlitVert(FakeUnlitAttributes input)
{
    FakeUnlitVaryings output = (FakeUnlitVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = input.uv;
    return output;
}

// Original: 4x4 ordered dither, index = (x & 3) * 4 + (y & 3), clip(1 - (_AlphaDither - threshold)).
// _AlphaDither = 0 keeps every pixel; raising it towards 2 dissolves the object.
void FakeUnlitDitherClip(float4 positionCS, float alphaDither)
{
    static const float thresholds[16] =
    {
        0.0588235296, 0.529411793, 0.176470593, 0.647058845,
        0.764705896, 0.294117659, 0.882352948, 0.411764711,
        0.235294119, 0.70588237, 0.117647059, 0.588235319,
        0.941176474, 0.470588237, 0.823529422, 0.352941185
    };
    uint2 pixel = (uint2)positionCS.xy;
    uint index = ((pixel.x & 3u) << 2) | (pixel.y & 3u);
    clip(1.0 - (alphaDither - thresholds[index]));
}

// Screen-space radial distance used by the tile and background vignettes:
// uv in [-1, 1], x corrected by aspect ratio.
float FakeUnlitScreenRadius(float4 positionCS)
{
    float2 uv = GetNormalizedScreenSpaceUV(positionCS) * 2.0 - 1.0;
    uv.x *= _ScreenParams.x / _ScreenParams.y;
    return length(uv);
}

// Rotate uv around (0.5, 0.5) after tiling, matching the Shader Graph Rotate node (degrees).
float2 FakeUnlitRotateUv(float2 uv, float tiling, float degrees)
{
    float s, c;
    sincos(degrees * 0.0174532924, s, c);
    float2 centered = uv * tiling - 0.5;
    return float2(dot(centered, float2(c, s)), dot(centered.yx, float2(c, -s))) + 0.5;
}

half3 FakeUnlitShade(FakeUnlitVaryings input, half3 emission, half smoothness)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = half4(1, 1, 1, 1);

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = half3(0, 0, 0);
    surfaceData.metallic = 0;
    surfaceData.specular = half3(0, 0, 0);
    surfaceData.smoothness = saturate(smoothness);
    surfaceData.occlusion = 1;
    surfaceData.emission = emission;
    surfaceData.alpha = 1;

    half3 color = UniversalFragmentPBR(inputData, surfaceData).rgb;
    return min(color, 65504.0);
}

half4 FakeUnlitDepthFrag(FakeUnlitVaryings input) : SV_Target
{
    return input.positionCS.z;
}

half4 FakeUnlitDepthNormalsFrag(FakeUnlitVaryings input) : SV_Target
{
    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
}

#endif
