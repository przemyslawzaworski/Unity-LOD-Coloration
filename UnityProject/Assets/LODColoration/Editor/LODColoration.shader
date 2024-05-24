Shader "Hidden/LODColoration" 
{
	Properties
	{
		[PerRendererData] _LODColoration ("Main Color", Color) = (0.0, 0.0, 0.0, 1.0)
	}
	SubShader 
	{
		Tags { "Queue" = "Transparent" }
		Pass 
		{
			Cull Off
			CGPROGRAM
			#pragma vertex VSMain
			#pragma fragment PSMain
			#pragma target 3.0

			float4 _LODColoration;
			sampler2D _MainTex;
			fixed _Cutoff;
			float3 unity_BillboardNormal;
			float3 unity_BillboardTangent;
			float4 unity_BillboardCameraParams;
			float4 unity_BillboardInfo;
			float4 unity_BillboardSize;
			float4 unity_BillboardImageTexCoords[16];

			float4 VSMain (float4 vertex : POSITION, inout float3 normal : NORMAL, inout float2 texcoord : TEXCOORD0, float4 texcoord1 : TEXCOORD1) : SV_POSITION
			{
				float3 worldPos = vertex.xyz + float3(unity_ObjectToWorld[0].w, unity_ObjectToWorld[1].w, unity_ObjectToWorld[2].w);
				float angle = unity_BillboardCameraParams.w;
				float2 percent = texcoord.xy;
				float3 billboardPos = (percent.x - 0.5f) * unity_BillboardSize.x * texcoord1.x * unity_BillboardTangent;
				billboardPos.y += (percent.y * unity_BillboardSize.y + unity_BillboardSize.z) * texcoord1.y;
				if (unity_BillboardSize.y > 0.0)
				{
					vertex.xyz += billboardPos;
					vertex.w = 1.0f;
				}
				angle += texcoord1.z;
				float imageIndex = fmod(floor(angle * unity_BillboardInfo.y + 0.5f), unity_BillboardInfo.x);
				float4 coords = unity_BillboardImageTexCoords[imageIndex];
				texcoord.xy = (coords.w < 0) ? coords.xy - coords.zw * percent.yx : coords.xy + coords.zw * percent;
				normal = normalize(mul(unity_ObjectToWorld, float4(normal, 0.0))).xyz;
				return UnityObjectToClipPos( vertex );
			}

			float4 PSMain (float4 vertex : SV_POSITION, float3 normal : NORMAL, float2 texcoord : TEXCOORD0 ) : SV_TARGET
			{
				if (unity_BillboardSize.y > 0.0)
				{
					half4 diffuseColor = tex2D(_MainTex, texcoord.xy);
					clip(diffuseColor.a - _Cutoff);
					return float4(0,0,1,1);
				}
				else
				{
					float diffuse = max(dot(normal, normalize(_WorldSpaceLightPos0).xyz), 0.0);
					return any(_LODColoration) ? diffuse * _LODColoration : float4(diffuse.xxx, 1.0);
				}
			}
			ENDCG
		}
	}
}