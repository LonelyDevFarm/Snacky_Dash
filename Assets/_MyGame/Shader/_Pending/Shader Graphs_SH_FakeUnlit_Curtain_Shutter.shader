Shader "Shader Graphs/SH_FakeUnlit_Curtain_Shutter" {
	Properties {
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Texture_Tiling ("Texture_Tiling", Float) = 1
		_FlashStrength ("FlashStrength", Range(0, 1)) = 0
		_FlashColor ("FlashColor", Vector) = (1,0.9333333,0.6941177,0)
		_AlphaDither ("AlphaDither", Float) = 0
		_Shutter ("Shutter", Float) = 0
		_ShadowColor ("ShadowColor", Vector) = (0.3207547,0.108533,0.0257209,0)
		_ShadowSize ("ShadowSize", Float) = 0.05
		_ShadowEdge_1 ("ShadowEdge_1", Float) = 0
		_ShadowEdge_2 ("ShadowEdge_2", Float) = 1
		_ThresholdOffset ("ThresholdOffset", Float) = 0
		_ParentWorldPos ("ParentWorldPos", Vector) = (0,0,0,0)
		_MinY ("MinY", Float) = -10
		_MaxY ("MaxY", Float) = 10
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