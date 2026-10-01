Shader "Shader Graphs/SH_FX_Projection" {
	Properties {
		[NoScaleOffset] _MagnetTexture ("MagnetTexture", 2D) = "white" {}
		[NoScaleOffset] _RadiusTexture ("RadiusTexture", 2D) = "white" {}
		_RadiusAlpha ("RadiusAlpha", Range(0, 1)) = 1
		_BaseAlpha ("BaseAlpha", Range(0, 1)) = 1
		_ColorA ("ColorA", Vector) = (0,0,0,0)
		_ColorB ("ColorB", Vector) = (0,0,0,0)
		_ColorRadius ("ColorRadius", Vector) = (1,1,1,0)
		_ColorSpeed ("ColorSpeed", Float) = 1
		_Intensity ("Intensity", Range(-2, 1)) = 0
		[NoScaleOffset] _NoiseTexture ("NoiseTexture", 2D) = "white" {}
		_NoiseTiling ("NoiseTiling", Vector) = (1,1,0,0)
		_NoiseSpeed ("NoiseSpeed", Float) = 1
		[NoScaleOffset] _DistortionTexture ("DistortionTexture", 2D) = "white" {}
		_DistortionTiling ("DistortionTiling", Vector) = (1,1,0,0)
		_DistortionSpeed ("DistortionSpeed", Vector) = (0,0,0,0)
		_DistortionStrength ("DistortionStrength", Float) = 0.01
		[HideInInspector] _DrawOrder ("Draw Order", Range(-50, 50)) = 0
		[Enum(Depth Bias, 0, View Bias, 1)] [HideInInspector] _DecalMeshBiasType ("DecalMesh BiasType", Float) = 0
		[HideInInspector] _DecalMeshDepthBias ("DecalMesh DepthBias", Float) = 0
		[HideInInspector] _DecalMeshViewBias ("DecalMesh ViewBias", Float) = 0
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