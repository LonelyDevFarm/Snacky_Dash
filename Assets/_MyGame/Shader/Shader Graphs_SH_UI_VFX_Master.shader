// The original UI shader graph SH_UI_VFX_Master, rebuilt from its compiled GLES3 code (Tools/Restore/ExtractGlsl.ps1):
// the Home Level button's face and underlay while the win streak is on fire (Mat_UI_PlayButton_StreakActive*). Two
// textures drift over the graphic in canvas space (position / 512): the second (the flame pattern) plus its strength
// tints the colour, the first (a gradient) times its strength is added to the sprite; then the saturation is pulled
// toward grey. Vertex colour multiplies in twice, as in the original. Premultiplied alpha, UI stencil and clipping kept.
Shader "Shader Graphs/SH_UI_VFX_Master"
{
    Properties
    {
        [PerRendererData] _MainTex ("_MainTex", 2D) = "white" {}
        _Saturation ("Saturation", Float) = 1
        [NoScaleOffset] _Overlay_Texture ("Overlay_Texture", 2D) = "white" {}
        _Overlay_Tiling ("Overlay_Tiling", Float) = 0
        _Overlay_Strength ("Overlay_Strength", Float) = 0
        _Overlay_Speed ("Overlay_Speed", Vector) = (0,0,0,0)
        [NoScaleOffset] _Overlay_Texture_2 ("Overlay_Texture_2", 2D) = "white" {}
        _Overlay_Tiling_2 ("Overlay_Tiling_2", Float) = 0
        _Overlay_Strength_2 ("Overlay_Strength_2", Float) = 0
        _Overlay_Speed_2 ("Overlay_Speed_2", Vector) = (0,0,0,0)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("ColorMask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; };

            sampler2D _MainTex, _Overlay_Texture, _Overlay_Texture_2;
            float _Saturation, _Overlay_Tiling, _Overlay_Strength, _Overlay_Tiling_2, _Overlay_Strength_2;
            float4 _Overlay_Speed, _Overlay_Speed_2, _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.local = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.local.xy * 0.001953125; // canvas units / 512
                float t = _Time.y;
                float3 pattern = tex2D(_Overlay_Texture_2, p * _Overlay_Tiling_2 + t * _Overlay_Speed_2.xy).rgb + _Overlay_Strength_2;
                pattern *= i.color.rgb;
                float4 main = tex2D(_MainTex, i.uv);
                float3 c = _Overlay_Strength * tex2D(_Overlay_Texture, p * _Overlay_Tiling + t * _Overlay_Speed.xy).rgb + main.rgb;
                c *= pattern;
                float grey = dot(c, float3(0.212672904, 0.715152204, 0.0721750036));
                c = lerp(grey.xxx, c, _Saturation);
                c *= i.color.rgb;
                float a = main.a * i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                return float4(c * a, a);
            }
            ENDCG
        }
    }
}
