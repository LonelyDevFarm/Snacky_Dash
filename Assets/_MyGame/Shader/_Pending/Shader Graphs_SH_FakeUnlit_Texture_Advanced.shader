Shader "Shader Graphs/SH_FakeUnlit_Texture_Advanced" {
	Properties {
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Saturation ("Saturation", Float) = 1
		_Smoothness ("Smoothness", Range(0, 1)) = 0
		_FlashStrength ("FlashStrength", Range(0, 1)) = 0
		_FlashColor ("FlashColor", Vector) = (1,0.9333333,0.6941177,0)
		_AlphaDither ("AlphaDither", Float) = 0
		_StarPowerActivate ("StarPowerActivate", Range(0, 1)) = 0
		_ColorDarken ("ColorDarken", Vector) = (0.08872458,0.07751515,0.2150944,0)
		_ColorStrength ("ColorStrength", Float) = 0.15
		_FresnelFlickerSpeed ("FresnelFlickerSpeed", Float) = 1
		_FresnelLerpA ("FresnelLerpA", Float) = 2
		_FresnelLerpB ("FresnelLerpB", Float) = 3
		_GradientSpeed ("GradientSpeed", Float) = 1
		_GradientTiling ("GradientTiling", Vector) = (1,1,0,0)
		[NoScaleOffset] _Gradient ("Gradient", 2D) = "white" {}
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