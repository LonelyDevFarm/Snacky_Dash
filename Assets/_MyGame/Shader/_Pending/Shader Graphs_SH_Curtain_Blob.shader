Shader "Shader Graphs/SH_Curtain_Blob" {
	Properties {
		_Color ("Color", Vector) = (0,0.8915043,1,0)
		_Smoothness ("Smoothness", Range(0, 1)) = 0
		_ShadowColor ("ShadowColor", Vector) = (0,0,0,0)
		_FresnelPower ("FresnelPower", Float) = 1
		_FresnelColor ("FresnelColor", Vector) = (0.7075472,0.9252973,1,0)
		_StepEdge_1 ("StepEdge_1", Float) = 0.65
		_StepEdge_2 ("StepEdge_2", Float) = 1
		[NoScaleOffset] _NoiseTexture ("NoiseTexture", 2D) = "white" {}
		_DistortionSize ("DistortionSize", Float) = 1
		_DisortionStrength ("DisortionStrength", Float) = 0
		_DistortionSpeed ("DistortionSpeed", Float) = 1
		_StepAmount ("StepAmount", Float) = 2
		_Step_1 ("Step_1", Float) = 0
		_Step_2 ("Step_2", Float) = 0
		_StepShadowColor ("StepShadowColor", Vector) = (1,1,1,0)
		_StepSideHeight ("StepSideHeight", Float) = 0
		_StepSideColor_1 ("StepSideColor_1", Vector) = (0.5372549,0.6509804,0.7058824,0)
		_StepSideColor_2 ("StepSideColor_2", Vector) = (0.5372549,0.6509804,0.7058824,0)
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
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

			float4 _Color;

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return _Color; // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}