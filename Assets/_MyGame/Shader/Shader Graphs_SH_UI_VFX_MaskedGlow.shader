// The original UI shader graph SH_UI_VFX_MaskedGlow, rebuilt from its compiled GLES3 code (Tools/Restore/ExtractGlsl.ps1):
// the flickering fire glows of the Home Level button and streak bar while the streak is on fire (Mat_UI_Streak_Glow*).
// The sprite only gives the shape (its alpha); a mask texture (Perlin noise) turning about the middle at
// _Mask_Rotation_Speed and drifting at _Mask_Panning_Speed, tiled _Mask_Tiling, cuts it; the colour is vertex colour x
// _Color (HDR, so it burns bright). Premultiplied alpha, UI stencil and clipping kept.
Shader "Shader Graphs/SH_UI_VFX_MaskedGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("_MainTex", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        [NoScaleOffset] _Mask ("Mask", 2D) = "white" {}
        _Mask_Tiling ("Mask_Tiling", Float) = 1
        _Mask_Rotation_Speed ("Mask_Rotation_Speed", Float) = 0
        _Mask_Panning_Speed ("Mask_Panning_Speed", Vector) = (0,0,0,0)
        _Strength ("Strength", Float) = 1

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

            sampler2D _MainTex, _Mask;
            float4 _Color, _Mask_Panning_Speed, _ClipRect;
            float _Mask_Tiling, _Mask_Rotation_Speed;

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
                float t = _Time.y;
                float angle = t * _Mask_Rotation_Speed;
                float s = sin(angle), c = cos(angle);
                float2 d = i.uv - 0.5;
                float2 turned = float2(dot(d, float2(c, s)), dot(d.yx, float2(c, -s))) + 0.5; // the graph's Rotate node
                float mask = tex2D(_Mask, turned * _Mask_Tiling + t * _Mask_Panning_Speed.xy).r;
                float a = tex2D(_MainTex, i.uv).a * mask * i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                return float4(i.color.rgb * _Color.rgb * a, a);
            }
            ENDCG
        }
    }
}
