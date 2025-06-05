// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Animated Water Shader
// Copyright (c) 2012, Stanislaw Adaszewski (http://algoholic.eu)
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met: 
//
// 1. Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer. 
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution. 
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
// ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

Shader "Custom/Cartoon Water" {
	Properties {
		_Color ("Water Color (RGB) Transparency (A)", COLOR) = (1, 1, 1, 0.5)
		_BumpMap ("Normal Map 1", 2D) = "white" {}
		_BumpMap2 ("Normal Map 2", 2D) = "white" {}
		_NoiseMap ("Noise Map", 2D) = "black" {}
		_Cube ("Reflection Cubemap", Cube) = "white" { TexGen CubeReflect }
		_Cycle ("Cycle", float) = 1.0
		_Speed ("Speed", float) = 0.05
		_SpecColor ("Specular Color", Color) = (0.5,0.5,0.5,1)
		_Shininess ("Shininess", Range (0.01, 2)) = 0.078125
		_ReflectColor ("Reflection Color", Color) = (1,1,1,0.5)
	}
	SubShader {
	Tags {"RenderType"="Transparent" "Queue"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		
		Cull Back
		
		
		  //Offset 0, 1//otherwise, we have flickering
	
		
		CGPROGRAM
		#pragma surface surf SimpleSpecular
		#pragma target 3.0

		float4 _Color;
		sampler2D _BumpMap;
		sampler2D _BumpMap2;
		samplerCUBE _Cube;
		sampler2D _NoiseMap;
		float _Cycle;
		float _Speed;
		float _Shininess;
		float4 _ReflectColor;

		struct Input {
			float2 uv_BumpMap;
			float3 worldRefl;
			float3 worldNormal;
			float3 worldPos;
			float3 viewDir;
			INTERNAL_DATA
		};
float rand(float3 co)
{
    return frac(sin( dot(co.xyz ,float3(12.9898,78.233,45.5432) )) * 43758.5453);
}


  half4 LightingSimpleSpecular (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten) {
        half3 h = normalize (lightDir + viewDir);
float dist= length(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0)));
        half diff = max (0, dot (s.Normal, -lightDir));

        float nh = max (0, dot (s.Normal, h));
        float spec = pow (nh, 48.0);

        half4 c;
        c.rgb = s.Albedo;
        c.a = s.Alpha;
        return c;
    }


		void surf (Input IN, inout SurfaceOutput o) {
			
			
			float3 flowDir = float3(-1,1,1);
			
			
			flowDir *= _Speed;
			
			float3 noise = tex2D(_NoiseMap, IN.uv_BumpMap);
			
			float phase = _Time[1] / _Cycle + noise.r * 0.5f;
			float f = frac(phase);
			flowDir.x*=sin(_Time[1]/9);
			flowDir.y*=cos(_Time[1]/4);
			half3 n1 = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap + flowDir.xy * frac(phase + 0.5f)));
			half3 n2 = UnpackNormal(tex2D(_BumpMap2, IN.uv_BumpMap + flowDir.xy * f));
			
			if (f > 0.5f)
				f = 2.0f * (1.0f - f);
			else
				f = 2.0f * f;
			
			o.Normal = lerp(n1, n2, f);
			o.Alpha = _Color.a;
			o.Gloss = 1;
			o.Specular = _Shininess;
			
			fixed4 reflcol = texCUBE (_Cube, WorldReflectionVector(IN, o.Normal));
    		o.Albedo = _Color.rgb;
    		
    		o.Emission = reflcol.rgb * _ReflectColor.rgb;
    		
		}
		ENDCG
	} 
	FallBack "Reflective/Bumped Specular"
}
