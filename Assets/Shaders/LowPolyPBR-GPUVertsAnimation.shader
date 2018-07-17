Shader "LowPolyPBR-GPUVertsAnimation" {
	Properties{
		_MainTex("MainTex",2D) = "white"{}
		_SkinningTex("SkinningTex",2D) = "black"{}
		_SkinningTexSize("SkinningTexSize", Float) = 0
		_StartPixelIndex("StartPixelIndex", Float) = 0
	}
	SubShader{
		Tags{
			"RenderType"="Opaque"
		}
		Pass{
			Cull Off
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"

			uniform sampler2D _MainTex;
			uniform fixed4 _LightColor0;
			uniform sampler2D _SkinningTex;
			uniform float _SkinningTexSize;
			uniform float _StartPixelIndex;

			struct VertexInput{
				float4 vertex : POSITION;
				float4 normal : NORMAL;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			inline float4 getUV(float startIndex){
				float y = (int)(startIndex / _SkinningTexSize);
				float u = (startIndex - y * _SkinningTexSize) / _SkinningTexSize;
				float v = y / _SkinningTexSize;
				return float4(u, v, 0, 0);
			}

			VertexOutput vert(VertexInput input, uint vid : SV_VertexID){
				VertexOutput output;
				UNITY_SETUP_INSTANCE_ID(input);
				float startPixelIndex = _StartPixelIndex + vid;
				float4 uv = getUV(startPixelIndex);
				float4 vertex = tex2Dlod(_SkinningTex, uv);
				output.vertex = UnityObjectToClipPos(vertex);
				output.uv = input.uv;
				return output;
			}

			float4 frag(VertexOutput output) : COLOR{
				float4 col = tex2D(_MainTex, output.uv);
				return col;
			}
			ENDCG
		}
	}
}
