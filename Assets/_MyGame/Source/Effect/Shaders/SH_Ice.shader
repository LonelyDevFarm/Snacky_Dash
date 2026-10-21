// Mặt băng (cơ chế sáng tạo, bản gốc không có) [user 21/10, theo ảnh mẫu Gemini]: một tấm băng liền chạy dọc đoạn, lùi vào trong
// mặt đường (còn thấy viền đường tối), hai đầu và khúc cua bo tròn, viền sương trắng dày lởm chởm, trong xanh nhạt có chấm sương và
// vết nứt mờ, có thành dày và bóng đổ cho thấy độ dày. Hình tấm tính từng điểm ảnh: tấm = các điểm cách đường đi của đoạn (_Points:
// đầu, khúc cua, cuối — IceView đặt) dưới _HalfWidth; mặt đường rộng ~1.5 ô nên tấm rộng hơn một ô. Mọi thứ khác chỉnh trên material.
Shader "Snack/SH_Ice"
{
    Properties
    {
        _HalfWidth ("Nửa bề ngang tấm băng (ô)", Range(0.2, 0.75)) = 0.52
        _Swell ("Độ uốn lượn của tấm (ô)", Range(0, 0.2)) = 0.08
        _SwellScale ("Cỡ sóng uốn (càng nhỏ sóng càng dài)", Float) = 0.7
        _EndInset ("Đầu tấm lùi khỏi mép điểm dừng (ô)", Range(0, 0.3)) = 0.05
        _EndRound ("Bo góc đầu tấm (ô)", Range(0.01, 0.5)) = 0.36
        _Bend ("Độ bo góc trong khúc cua (ô)", Range(0, 0.5)) = 0.4
        _Wobble ("Độ lởm chởm mép (ô)", Range(0, 0.1)) = 0.025
        _Color ("Màu giữa tấm", Color) = (0.76, 0.91, 0.99, 1)
        _DeepColor ("Màu sát viền", Color) = (0.56, 0.80, 0.96, 1)
        _Opacity ("Độ đục", Range(0, 1)) = 0.93
        _RimColor ("Màu viền sương", Color) = (0.95, 0.99, 1, 1)
        _RimWidth ("Bề dày viền sương (ô)", Range(0, 0.3)) = 0.15
        _RimVary ("Viền dày mỏng thất thường", Range(0, 1)) = 0.6
        _Thickness ("Độ dày tấm băng (ô)", Range(0, 0.2)) = 0.06
        _SideColor ("Màu thành tấm", Color) = (0.36, 0.62, 0.86, 1)
        _ShadowColor ("Màu bóng đổ (alpha = độ đậm)", Color) = (0.02, 0.06, 0.16, 0.35)
        _ShadowOffset ("Bóng đổ lệch thêm (ô)", Range(0, 0.2)) = 0.03
        _SpeckColor ("Màu chấm sương", Color) = (1, 1, 1, 0.6)
        _SpeckScale ("Mật độ chấm sương", Float) = 9
        _CrackColor ("Màu vết nứt", Color) = (1, 1, 1, 0.45)
        _CrackScale ("Cỡ vết nứt", Float) = 1.3
        _ShatterScale ("Mật độ vết nứt khi sắp vỡ", Float) = 2.6
        _SheenColor ("Màu vệt lấp lánh", Color) = (1, 1, 1, 0.45)
        _SheenPeriod ("Chu kỳ lấp lánh (giây)", Float) = 3.5
        _SheenWidth ("Bề rộng vệt lấp lánh (ô)", Float) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            // Mảnh của các ô kề chồng mép nhau: mỗi điểm ảnh chỉ tô một lần.
            Stencil { Ref 64 ReadMask 64 WriteMask 64 Comp NotEqual Pass Replace }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _HalfWidth;
                float _Swell;
                float _SwellScale;
                float _EndInset;
                float _EndRound;
                float _Bend;
                float _Wobble;
                half4 _Color;
                half4 _DeepColor;
                float _Opacity;
                half4 _RimColor;
                float _RimWidth;
                float _RimVary;
                float _Thickness;
                half4 _SideColor;
                half4 _ShadowColor;
                float _ShadowOffset;
                half4 _SpeckColor;
                float _SpeckScale;
                half4 _CrackColor;
                float _CrackScale;
                float _ShatterScale;
                half4 _SheenColor;
                float _SheenPeriod;
                float _SheenWidth;
            CBUFFER_END

            // Đường đi của đoạn (toạ độ vật thể): đầu, các khúc cua, cuối — IceView đặt qua MaterialPropertyBlock.
            #define MAX_POINTS 24
            float4 _Points[MAX_POINTS];
            float _PointCount;
            // Hiệu ứng theo lượt chơi (IceView đặt qua MaterialPropertyBlock, mặc định 0 = không có):
            float4 _Crack;   // xy: nơi vết nứt bắt đầu (đầu rắn chạm), z: bán kính đã lan, w: độ rõ
            float4 _Glint;   // xy: tâm lóe sáng (chỗ chạm / đầu rắn đang trượt), z: độ sáng, w: bán kính
            float _Vanish;   // 0 = băng đủ, 1 = tan hết (vỡ / đóng băng lại khi hoàn tác)

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 world      : TEXCOORD0;
                float2 local      : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.world = world.xy;
                output.local = input.positionOS.xy;
                return output;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 Hash2(float2 p) { return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            // Khoảng cách tới đoạn a→b; đầu nào là đầu tấm (flat) thì cắt vuông bo góc _EndRound thay vì tròn [user 21/10, ảnh mẫu].
            // Trả về "khoảng cách tới đường đi" để Slab trừ _HalfWidth như các đoạn thường.
            float Segment(float2 p, float2 a, float2 b, bool flatA, bool flatB)
            {
                float2 ab = b - a;
                float len = max(length(ab), 1e-5);
                float2 dir = ab / len;
                float u = dot(p - a, dir);
                float perp = abs(dir.x * (p - a).y - dir.y * (p - a).x);
                if (!flatA && u <= 0.0) return length(p - a);
                if (!flatB && u >= len) return length(p - b);
                float x = max(flatA ? -u : -1e4, flatB ? u - len : -1e4);
                float2 q = float2(x, perp - _HalfWidth) + _EndRound;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _EndRound + _HalfWidth;
            }

            // Khoảng cách có dấu tới mép tấm (âm trong tấm). Cả tấm uốn lượn theo nhiễu dài (dời điểm lấy mẫu): một mép móp thì mép kia
            // phình, bề ngang giữ nguyên [user 21/10]; các đoạn của đường đi hoà mềm (smooth min) để góc trong khúc cua bo tròn.
            float Slab(float2 q)
            {
                float2 warp = float2(Noise(q * _SwellScale + 2.3), Noise(q * _SwellScale + 8.9)) - 0.5;
                float2 at = q + warp * 2.0 * _Swell;
                float d = 1e4;
                float before = 1e4;
                int count = (int)_PointCount;
                [loop] for (int i = 0; i + 1 < count && i + 1 < MAX_POINTS; i++)
                {
                    float di = Segment(at, _Points[i].xy, _Points[i + 1].xy, i == 0, i + 2 == count);
                    if (i > 0)
                    {
                        // Chỉ bo góc trong (phía lõm của khúc cua); góc ngoài giữ tròn tự nhiên, không phình ra lấn mép đường.
                        float2 corner = _Points[i].xy;
                        float2 arm1 = _Points[i - 1].xy - corner, arm2 = _Points[i + 1].xy - corner;
                        if (dot(at - corner, arm1) > 0.0 && dot(at - corner, arm2) > 0.0)
                        {
                            float m = saturate(0.5 + 0.5 * (di - before) / max(_Bend, 1e-4));
                            d = min(d, lerp(di, before, m) - _Bend * m * (1.0 - m));
                        }
                    }
                    d = min(d, di);
                    before = di;
                }
                return d - _HalfWidth + (Noise(q * 7.0) - 0.5) * 2.0 * _Wobble + (Noise(q * 19.0) - 0.5) * _Wobble;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.world;
                float2 q = input.local;
                float top = Slab(q);                                  // mặt trên
                float side = Slab(q + float2(0, _Thickness));         // thành tấm (camera nhìn hơi chếch nên thấy ở phía dưới)
                float shadow = Slab(q + float2(0.03, _Thickness + _ShadowOffset));
                float topAlpha = smoothstep(0.012, -0.012, top);
                float sideAlpha = smoothstep(0.012, -0.012, side);
                float shadowAlpha = smoothstep(0.06, -0.04, shadow) * _ShadowColor.a;
                clip(max(max(topAlpha, sideAlpha), shadowAlpha) - 0.01);

                // Bóng đổ xuống mặt đường, rồi thành tấm (xanh đậm, sáng dần lên mặt trên).
                half4 under = half4(_ShadowColor.rgb, shadowAlpha);
                float wall = saturate(-side / max(_Thickness, 1e-4));
                half4 sideColor = half4(lerp(_SideColor.rgb, _DeepColor.rgb, wall * 0.5), _Opacity);
                under = lerp(under, sideColor, sideAlpha);

                float inside = -top;
                // Thân: xanh nhạt ở giữa, đậm dần sát viền, loang mây nhẹ.
                float cloud = Noise(p * 1.7) * 0.6 + Noise(p * 4.3 + 7.0) * 0.4;
                half3 color = lerp(_DeepColor.rgb, _Color.rgb, smoothstep(0.0, _HalfWidth * 0.9, inside) * (0.7 + 0.3 * cloud));

                // Chấm sương: chấm trắng nhỏ rải ngẫu nhiên.
                float2 grid = p * _SpeckScale;
                float2 id = floor(grid);
                float2 spot = Hash2(id) * 0.8 + 0.1;
                float radius = lerp(0.05, 0.16, Hash(id + 3.1));
                float speck = (1.0 - smoothstep(radius * 0.4, radius, length(frac(grid) - spot))) * step(0.5, Hash(id + 9.7));
                color = lerp(color, _SpeckColor.rgb, speck * _SpeckColor.a);

                // Vết nứt mờ: đường mảnh theo đường đồng mức của nhiễu, chỗ có chỗ không.
                float n = Noise(p * _CrackScale + 11.0);
                float crack = (1.0 - smoothstep(0.0, 0.012, abs(n - 0.5))) * smoothstep(0.45, 0.7, Noise(p * 0.9 + 3.0));
                color = lerp(color, _CrackColor.rgb, crack * _CrackColor.a);

                // Viền sương trắng: dày mỏng thất thường dọc mép (nhiễu dài), mép trong lởm chởm, tan dần vào thân.
                float rimEdge = _RimWidth * lerp(1.0 - _RimVary, 1.0 + _RimVary, Noise(p * 2.2 + 5.0)) * (0.85 + 0.3 * Noise(p * 12.0));
                float rim = 1.0 - smoothstep(rimEdge * 0.6, rimEdge, inside);
                rim = max(rim, (1.0 - smoothstep(rimEdge, rimEdge * 1.6, inside)) * speck); // chấm sương rắc thêm ngay trong viền
                color = lerp(color, _RimColor.rgb, rim * _RimColor.a);

                // Vết nứt (rắn đủ nặng bước lên): đường nứt kiểu kính vỡ, lan dần từ chỗ đầu rắn chạm.
                if (_Crack.w > 0.0)
                {
                    float2 cq = q * _ShatterScale;
                    float f1 = 8.0, f2 = 8.0;
                    [unroll] for (int y = -1; y <= 1; y++)
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 r = g + Hash2(floor(cq) + g) - frac(cq);
                        float dd = dot(r, r);
                        if (dd < f1) { f2 = f1; f1 = dd; } else if (dd < f2) { f2 = dd; }
                    }
                    float crackLine = 1.0 - smoothstep(0.03, 0.08, sqrt(f2) - sqrt(f1));
                    float spread = 1.0 - smoothstep(_Crack.z - 0.35, _Crack.z, length(q - _Crack.xy));
                    color = lerp(color, _RimColor.rgb, crackLine * spread * _Crack.w);
                }
                // Lóe sáng: chỗ đầu rắn chạm / đầu rắn đang trượt trên băng.
                float glint = _Glint.z * exp(-dot(q - _Glint.xy, q - _Glint.xy) / max(_Glint.w * _Glint.w, 1e-4));
                color += glint * _SheenColor.rgb;

                // Vệt lấp lánh chạy chéo theo chu kỳ.
                float sweep = frac(_Time.y / _SheenPeriod) * 40.0 - 10.0;
                float sheen = saturate(1.0 - abs(p.x + p.y * 0.55 - sweep) / _SheenWidth);
                color += sheen * sheen * _SheenColor.rgb * _SheenColor.a;

                half4 surface = half4(color, _Opacity);
                half4 result = lerp(under, surface, topAlpha);
                result.a *= 1.0 - saturate(_Vanish);
                return result;
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
