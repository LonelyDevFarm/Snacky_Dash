// Bản trong suốt của SH_FakeUnlit_Texture cho booster Rắn bóng ma (phần sáng tạo, bản gốc không có) [user 20/10].
// Tô màu y như bản gốc (độ đậm màu, ánh nháy, ánh sáng) rồi nhân độ mờ _Alpha. Hai lượt cho mỗi vật: lượt đầu (SRPDefaultUnlit,
// URP vẽ trước UniversalForward) chỉ ghi chiều sâu, thêm ánh sáng và mép sáng (rim) để không bị đường tối kéo sẫm; lượt sau tô màu trộn trong suốt và chỉ qua đúng mặt trước nhất, nên khối hộp
// mờ đều như kính, không lộ mặt trong; đốt vẽ trước che đốt sau ở chỗ thân chồng nhau.
Shader "Snack/SH_FakeUnlit_Texture_Ghost"
{
    Properties
    {
        [NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Saturation ("Saturation", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        _FlashStrength ("FlashStrength", Range(0, 1)) = 0
        _FlashColor ("FlashColor", Color) = (1,0.9333333,0.6941177,0)
        _AlphaDither ("AlphaDither", Float) = 0
        _Alpha ("Alpha", Range(0, 1)) = 0.5
        _GlowColor ("GlowColor", Color) = (0.6, 0.9, 1, 1)
        _Glow ("Glow", Range(0, 1)) = 0.3
        _RimColor ("RimColor", Color) = (1, 1, 1, 1)
        _Rim ("Rim", Range(0, 2)) = 0.9
        _RimPower ("RimPower", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        HLSLINCLUDE
        #include "Include/SnackFakeUnlit.hlsl"

        TEXTURE2D(_Texture); SAMPLER(sampler_Texture);
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Saturation;
            float _Smoothness;
            float _FlashStrength;
            float4 _FlashColor;
            float _AlphaDither;
            float _Alpha;
            float4 _GlowColor;
            float _Glow;
            float4 _RimColor;
            float _Rim;
            float _RimPower;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "GhostDepth"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex FakeUnlitVert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(FakeUnlitVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex FakeUnlitVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            half4 Frag(FakeUnlitVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 tex = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, input.uv).rgb;
                half luminance = dot(tex, half3(0.212672904, 0.715152204, 0.0721750036));
                half3 emission = luminance + _Saturation * (tex - luminance);
                emission += _FlashStrength * _FlashColor.rgb;
                // Bóng sáng để đường tối nhìn xuyên qua không làm rắn sẫm theo: ánh tự phát + mép sáng như kính (rim).
                emission += _Glow * _GlowColor.rgb;
                half3 normal = NormalizeNormalPerPixel(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = pow(1.0 - saturate(dot(normal, view)), _RimPower) * _Rim;
                emission += rim * _RimColor.rgb;
                return half4(FakeUnlitShade(input, emission, _Smoothness), saturate(_Alpha + rim * 0.5));
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
