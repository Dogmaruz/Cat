Shader "RecoveredOriginal/Shader Graphs/Cats_JumpTile" {
	Properties {
		_LightDir ("LightDir", Vector) = (0,1,0.77,0)
		MainColor ("MainColor", Vector) = (0,0,0,1)
		[NoScaleOffset] MainTex ("MainTex", 2D) = "white" {}
		[HDR] EmissionColor ("EmissionColor", Vector) = (1,1,1,1)
		[NoScaleOffset] EmissionTex ("EmissionTex", 2D) = "black" {}
		E ("E", Range(1, 5)) = 1
		[ToggleUI] Fresnel ("Fresnel", Float) = 0
		[HDR] FreColor ("FreColor", Vector) = (0.0235849,0.4325759,1,0)
		FrePower ("FrePower", Range(1, 5)) = 1
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