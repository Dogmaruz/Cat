Shader "RecoveredOriginal/Shader Graphs/Twins_Common_Tex" {
	Properties {
		[HDR] BaseColor ("BaseColor", Vector) = (0,0,0,0)
		[ToggleUI] _Light ("Light", Float) = 0
		_LightDir ("LightDir", Vector) = (0,1,0.77,0)
		[ToggleUI] Boolean_d66bb56d4b4f45d18fbf3478e3f2ca92 ("SpeclurLight", Float) = 0
		_SpeclurPower ("SpeclurPower", Range(1, 10)) = 2
		[NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
		_TillingOffset ("TillingOffset", Vector) = (1,1,0,0)
		[ToggleUI] Fresnel ("Fresnel", Float) = 0
		[HDR] FreColor ("FreColor", Vector) = (0.0235849,0.4325759,1,0)
		FrePower ("FrePower", Range(1, 5)) = 1
		[ToggleUI] VertexOffset_1 ("VertexOffset", Float) = 1
		_Distance ("Distance", Float) = 120
		_StartPos ("StartPos", Vector) = (0,0,5,0)
		_OffsetValue ("OffsetValue", Vector) = (0,-120,0,0)
		_Lerp ("Lerp", Range(0, 1)) = 1
		_Position ("Position", Vector) = (0.5,0.8,0,0)
		[ToggleUI] _HaHa ("HaHa", Float) = 0
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
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy);
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}