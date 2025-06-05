Shader "Atmosphere" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	_NoiseTex ("Noise (RGB) Trans (A)", 2D) = "white" {}
	
}

SubShader {
	Tags { "Queue"="Transparent"  "RenderType"="Transparent"}
	LOD 200
Blend One One
Cull Back
Offset 0, -1 
CGPROGRAM
#pragma surface surf Lambert alpha:blend 
#pragma target 3.0


sampler2D _MainTex;
sampler2D _NoiseTex;
fixed4 _Color;

struct Input {
	float2 uv_MainTex;
	float2 uv_NoiseTex;
	float3 worldRefl;
			float3 worldNormal;
			float3 worldPos;
			float3 viewDir;
			INTERNAL_DATA
	
};

    
void surf (Input IN, inout SurfaceOutput o) {
	fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color+.2;
	c.a-= tex2D(_NoiseTex, IN.uv_NoiseTex).r;
	IN.uv_MainTex += float4(.45, 0, 0, 0);
	c += tex2D(_MainTex, IN.uv_MainTex) * _Color+.2;
	
IN.uv_NoiseTex += float4(.45, 0, 0, 0);
c.a *= tex2D(_NoiseTex, IN.uv_NoiseTex).r*tex2D(_NoiseTex, IN.uv_NoiseTex).r*tex2D(_NoiseTex, IN.uv_NoiseTex).r;


			o.Alpha = clamp(c.a,0.0,1.0)+.1;

    		o.Albedo = clamp(c.rgb,0.0,1.0);

}
ENDCG
}

Fallback "Transparent/VertexLit"
}
