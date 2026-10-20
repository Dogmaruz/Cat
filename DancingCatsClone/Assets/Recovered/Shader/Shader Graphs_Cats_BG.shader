Shader "RecoveredOriginal/Shader Graphs/Cats_BG" {
	Properties {
		[HDR] Color1 ("Color1", Vector) = (0.1084906,1,0.3724781,0)
		[HDR] Color2 ("Color2", Vector) = (0.1084906,1,0.3724781,0)
		[NoScaleOffset] TexAdd ("TexAdd", 2D) = "white" {}
		[NoScaleOffset] Mask ("Mask", 2D) = "white" {}
		_TillingOffset ("TillingOffset", Vector) = (1,1,0,0)
		Alpha ("Alpha", Range(0, 1)) = 0.1
		[ToggleUI] Color_Uv1 ("Color(Uv1)", Float) = 0
		[ToggleUI] Tex_Uv1 ("Tex(Uv1)", Float) = 1
		[ToggleUI] Mask_Uv1 ("Mask(Uv1)", Float) = 0
		[ToggleUI] Boolean_f327145916584868b02f26db7c793ac8 ("isColor", Float) = 0
		[HDR] Color_abf7fe455142484c99b8c1a689d6e5b8 ("Color", Vector) = (0,0,0,0)
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