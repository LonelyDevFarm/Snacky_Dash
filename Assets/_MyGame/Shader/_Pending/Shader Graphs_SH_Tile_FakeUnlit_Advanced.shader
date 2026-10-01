Shader "Shader Graphs/SH_Tile_FakeUnlit_Advanced" {
	Properties {
		_Main_Color_1 ("Main_Color_1", Vector) = (1,0,0,0)
		_Main_Color_2 ("Main_Color_2", Vector) = (0,0.4149446,1,0)
		_Side_Color ("Side_Color", Vector) = (0.8261385,0,1,0)
		_Side_Height_Top ("Side_Height_Top", Float) = 0
		_Side_Height_Bottom ("Side_Height_Bottom", Float) = 1.4
		_Shadow_Color ("Shadow_Color", Vector) = (0,0,0,0)
		_Shadow_Edge_1 ("Shadow_Edge_1", Float) = 0.2
		_Shadow_Edge_2 ("Shadow_Edge_2", Float) = 1
		_Shadow_Steps ("Shadow_Steps", Float) = 3
		_Shadow_Strength ("Shadow_Strength", Float) = 1
		[NoScaleOffset] _Overlay_Texture ("Overlay_Texture", 2D) = "white" {}
		_Overlay_Tiling ("Overlay_Tiling", Float) = 1
		_Overlay_Rotation ("Overlay_Rotation", Range(0, 360)) = 0
		_Overlay_Strength ("Overlay_Strength", Float) = 0
		_Overlay_Color ("Overlay_Color", Vector) = (1,1,1,0)
		[NoScaleOffset] _Overlay_Texture_2 ("Overlay_Texture_2", 2D) = "white" {}
		_Overlay_Tiling_2 ("Overlay_Tiling_2", Float) = 0
		_Overlay_Rotation_2 ("Overlay_Rotation_2", Float) = 0
		_Overlay_Strength_2 ("Overlay_Strength_2", Float) = 1
		_Overlay_Color_2 ("Overlay_Color_2", Vector) = (1,0,0,0)
		_Vignette_Color ("Vignette_Color", Vector) = (1,1,1,0)
		_Vignette_Strength_1 ("Vignette_Strength_1", Float) = 0
		_Vignette_Strength_2 ("Vignette_Strength_2", Float) = 1
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}