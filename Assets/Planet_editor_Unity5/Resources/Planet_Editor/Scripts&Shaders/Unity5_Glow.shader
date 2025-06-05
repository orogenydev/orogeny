// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/Glow"
{
    Properties {
       
 _Color("Color", Color) = (0, 0, 0, 1)
        _AtmoColor("Atmosphere Color", Color) = (0.5, 0.5, 1.0, 1)
        _Size("Size", Float) = 0.1
        _Falloff("Falloff", Float) = 5
        _FalloffPlanet("Falloff Planet", Float) = 5
        _Transparency("Transparency", Float) = 15
        _TransparencyPlanet("Transparency Planet", Float) = 1
        _Radius("Radius_k", Float) = 1
   
    }
    SubShader {
     //Offset 50, 0
       Tags {"LightMode" = "Always" "Queue" = "Transparent+1" "RenderType"="Transparent"}
           Pass
        {
            Name "AtmosphereBase"
         
            Cull Front
         
            Blend SrcAlpha OneMinusSrcAlpha
           
            CGPROGRAM
              // #pragma exclude_renderers d3d11 xbox360
                #pragma vertex vert
                #pragma fragment frag
            
                #pragma fragmentoption ARB_fog_exp2
                #pragma fragmentoption ARB_precision_hint_fastest
               
                #include "UnityCG.cginc"
              #include "Lighting.cginc"
                
                uniform float4 _Color;
                uniform float4 _AtmoColor;
                uniform float _Size;
                uniform float _Falloff;
                uniform float _Transparency;
                uniform float _Radius;
                struct v2f
                {
                    float4 pos : SV_POSITION;
                    float3 normal : TEXCOORD0;
                    float3 worldvertpos : TEXCOORD1;
                     fixed4 color: COLOR1;
                     fixed3 cam: TEXCOORD2;
                     float brightness:TEXCOORD3;
                };

                v2f vert(appdata_base v)
                {
                    v2f o;
                    UNITY_INITIALIZE_OUTPUT(v2f, o);
                    v.vertex.xyz += v.normal*_Size;
                    o.pos = UnityObjectToClipPos (v.vertex);
                   
                    o.normal = v.normal;
                    o.worldvertpos = mul(unity_ObjectToWorld, v.vertex);
                fixed3 lightDirection;
               fixed attenuation;
                fixed3 camDirection;
               
               // add diffuse
               if(_WorldSpaceLightPos0.w == 0.0)//directional light
               {
                  attenuation = 1.5;
                  lightDirection = normalize(mul(_WorldSpaceLightPos0,unity_ObjectToWorld).xyz);
               }
               else// point or spot light
               {
                  lightDirection = normalize(mul(_WorldSpaceLightPos0,unity_ObjectToWorld).xyz - v.vertex.xyz);
                  attenuation = 1.0/(length(mul(_WorldSpaceLightPos0,unity_ObjectToWorld).xyz - v.vertex.xyz)) * 0.5;
               }
               camDirection=normalize(mul(float4(_WorldSpaceCameraPos, 1.0) ,unity_ObjectToWorld).xyz - mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0)));
               fixed3 normalDirection = normalize(v.normal);
               fixed3 diffuseLight =  max(dot(normalDirection,lightDirection),0);
               fixed3 cam_ =  max(dot(normalDirection,camDirection),0);
               o.color.xyz = diffuseLight *attenuation;
            o.cam.xyz=camDirection;
                      o.brightness = clamp(length(mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0))-_WorldSpaceCameraPos)-25/_Radius,0.0,1.0);
                    return o;
                }
              
                float4 frag(v2f i) : COLOR
                {
              
                   i.normal = normalize(i.normal);
                    float3 viewdir = mul(float4(normalize((i.worldvertpos-_WorldSpaceCameraPos)),1.0),unity_ObjectToWorld);
                   
                    float4 color = _AtmoColor;
                    color.a = pow(saturate(dot(viewdir, i.normal)), _Falloff);
                    color.a *= _Transparency*_Color*i.color*i.brightness;
                    return color;
                }
            ENDCG
        }
    
           Pass//pass for point light
        {
          
           Tags {"LightMode" = "ForwardAdd" "Queue" = "Transparent" "RenderType"="Transparent"}
            Cull Front

            Blend SrcAlpha OneMinusSrcAlpha 
          
            CGPROGRAM
             
                #pragma vertex vert
                #pragma fragment frag
             
                #pragma fragmentoption ARB_fog_exp2
                #pragma fragmentoption ARB_precision_hint_fastest
               
                #include "UnityCG.cginc"
              #include "Lighting.cginc"
                
                uniform float4 _Color;
                uniform float4 _AtmoColor;
                uniform float _Size;
                uniform float _Falloff;
                uniform float _Transparency;
                	uniform float _Radius;
                   uniform sampler2D _LightTextureB0; 
                struct v2f
                {
                    float4 pos : SV_POSITION;
                    float3 normal : TEXCOORD0;
                    float3 worldvertpos : TEXCOORD1;
                     fixed4 color: COLOR1;
                     fixed3 cam: TEXCOORD2;
                       float4 posLight : TEXCOORD3;
                     float brightness:TEXCOORD4;
                };

                v2f vert(appdata_base v)
                {
                    v2f o;
                         UNITY_INITIALIZE_OUTPUT(v2f, o);
                    v.vertex.xyz += v.normal*_Size;
                    o.pos = UnityObjectToClipPos (v.vertex);
                   
                    o.normal = v.normal;
                    o.worldvertpos = mul(unity_ObjectToWorld, v.vertex);
              
                fixed3 camDirection;
        
               camDirection=normalize(mul(float4(_WorldSpaceCameraPos, 1.0) ,unity_ObjectToWorld).xyz - mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0)));
               fixed3 normalDirection = normalize(v.normal);
            
               fixed3 cam_ =  max(dot(normalDirection,camDirection),0);
             
            o.cam.xyz=camDirection;
                   
                   
                   o.brightness = clamp(length(mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0))-_WorldSpaceCameraPos)-25/_Radius,0.0,1.0);
                   
                    return o;
                }
              
                float4 frag(v2f i) : COLOR
                {
               
                 float3 normalDirection = normalize(i.normal);
 
            float3 viewDirection = normalize(
               _WorldSpaceCameraPos - float3(i.worldvertpos));
            float3 lightDirection;
            float attenuation;
 
            if (0.0 == _WorldSpaceLightPos0.w) // directional light?
            {
               attenuation = 1.0; // no attenuation
               lightDirection = 
                  normalize(float3(_WorldSpaceLightPos0.xyz));
            } 
            else // point or spot light
            {
               float3 vertexToLightSource = 
                  float3(_WorldSpaceLightPos0 - i.worldvertpos);
               lightDirection = normalize(vertexToLightSource);
 
               float distance = i.posLight.z; 
                  // use z coordinate in light space as signed distance
               attenuation = 
                  tex2D(_LightTextureB0, float2(distance,distance)).a;
            
            }
 
            float3 diffuseReflection = 
               attenuation * float3(_LightColor0.xyz) * float3(_Color.xyz)
               * max(0.0, dot(normalDirection, lightDirection));
 
            float3 specularReflection;
            if (dot(normalDirection, lightDirection) < 0.0) 
               // light source on the wrong side?
            {
               specularReflection = float3(0.0, 0.0, 0.0); 
                  // no specular reflection
            }
            else // light source on the right side
            {
               specularReflection = attenuation * float3(_LightColor0.xyz) 
                  * float3(0.5,0.5,1) * pow(max(0.0, dot(
                  reflect(-lightDirection, normalDirection), 
                  viewDirection)), 1);
            }
                   i.normal = normalize(i.normal);
                    float3 viewdir = mul(float4(normalize((i.worldvertpos-_WorldSpaceCameraPos)),1.0),unity_ObjectToWorld);
                   
                    float4 color = _AtmoColor;
                    color.a = pow(saturate(dot(viewdir, i.normal)), _Falloff);
                   color.a *= _Transparency*_Color*float4(diffuseReflection + specularReflection, 1.0)*i.brightness;
                    return color;
                }
            ENDCG
        }
      
    }
}
