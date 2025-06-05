// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/height_based_w_light"
{
    Properties {
        _Tex0 ("Tex 0", 2D) = "white" {}
        _Tex1 ("Tex 1", 2D) = "white" {}
        _Tex2 ("Tex 2", 2D) = "white" {}
        _Tex3 ("Tex 3", 2D) = "white" {}
        _Tex4 ("Tex 4", 2D) = "white" {}
        _Tex5 ("Tex 5", 2D) = "white" {}
        _Tex6 ("Tex 6", 2D) = "white" {}
        
    _Color("Color", Color) = (0, 0, 0, 1)
      
        _Size("Size", Float) = 0.01
      _Radius("Radius_k", Float) = 1
     

        _Blend0to1and1to2 ("Blend between 0 and 1, 1 and 2", Vector) = (0,1,2,3)
        _Blend2to3and3to4 ("Blend between 2 and 3, 3 and 4", Vector) = (0,1,2,3)
        _Blend4to5and5to6 ("Blend between 4 and 5, 5 and 6", Vector) = (0,1,2,3)
    }
    SubShader {
     //  Lighting Off
       // Fog { Mode Off }
        
        Pass {
           Tags {"LightMode" = "ForwardBase" "Queue" ="Geometry" "RenderType" = "Geometry"}
           	  	//Blend SrcAlpha OneMinusSrcAlpha
            LOD 200
            Cull Back
            //Zwrite off
         
            CGPROGRAM
// Upgrade NOTE: excluded shader from DX11 and Xbox360; has structs without semantics (struct v2f members viewDir,worldRefl)
//#pragma exclude_renderers d3d11 xbox360

                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"
                #include "Lighting.cginc"
                #pragma multi_compile_fwdbase    
                #pragma fragmentoption ARB_fog_exp2
               #pragma fragmentoption ARB_precision_hint_fastest
                #pragma target 3.0
               #include "AutoLight.cginc"
               
                sampler2D _Tex0;
                sampler2D _Tex1;
                sampler2D _Tex2;
                sampler2D _Tex3;
                sampler2D _Tex4;
                sampler2D _Tex5;
                sampler2D _Tex6;
                float4 _Blend0to1and1to2;
                float4 _Blend2to3and3to4;
                float4 _Blend4to5and5to6;
                uniform float4 _Tex0_ST;
                 uniform float4 _Tex1_ST;
                  uniform float4 _Tex2_ST;
                   uniform float4 _Tex3_ST;
                    uniform float4 _Tex4_ST;
                     uniform float4 _Tex5_ST;
                      uniform float4 _Tex6_ST;
  				uniform float4 _Color;
  				uniform float _Radius;
  				
         
       //uniform float4 _LightColor0;
                struct v2f {
                    float4 pos : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float4 col : COLOR;
               		float3 normal : TEXCOORD1;
                    float3 worldvertpos : TEXCOORD2;
                      float3 pole : TEXCOORD3;
                 fixed4 color: COLOR1;
                 LIGHTING_COORDS(4,5)
                 
                };
            
             
                
               
                v2f vert (appdata_base v) {
                    v2f OUT;
                    UNITY_INITIALIZE_OUTPUT(v2f, OUT);
                  //  OUT.pos = mul (UNITY_MATRIX_MVP, v.vertex);
                 //   OUT.uv = TRANSFORM_TEX(v.texcoord, _Tex0);
                    
                     OUT.pos = UnityObjectToClipPos (v.vertex);
                
                    OUT.worldvertpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                    OUT.normal = v.normal;
                    OUT.col = length(mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0))-OUT.worldvertpos)*_Radius ;
                    OUT.uv = TRANSFORM_TEX(v.texcoord, _Tex0);
                         OUT.pole = clamp(pow(abs(mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0)).y - OUT.worldvertpos.y)*_Radius/20,6),-1.0,1.0) ;
                    fixed3 lightDirection;
               fixed attenuation;
               // add diffuse
               if(_WorldSpaceLightPos0.w == 0.0)//directional light
               {
                  attenuation = 2;
                  lightDirection = normalize(mul(_WorldSpaceLightPos0,unity_ObjectToWorld).xyz);
               }
               else// point or spot light
               {
                  lightDirection = normalize(mul(_WorldSpaceLightPos0,unity_ObjectToWorld).xyz - v.vertex.xyz);
                  attenuation = 1.0/(length(mul(_WorldSpaceLightPos0,unity_ObjectToWorld).xyz - v.vertex.xyz)) * 0.5;
                
               }
               
               fixed3 normalDirection = normalize(v.normal);
               fixed3 diffuseLight =  max(dot(normalDirection,lightDirection),0);
               // combine the lights (diffuse + ambient)
               OUT.color.xyz = diffuseLight *attenuation*float3(_LightColor0.xyz);
            
                    
                   TRANSFER_VERTEX_TO_FRAGMENT(OUT);
                    return OUT;
                }
  half4 frag (v2f fInput) : COLOR {
  // c3 and c3_ are using the same texture with different tilling in order to make tilling less noticable after blending. 
  //c3 represents an average height so seems to be the main texture on the planet. Doing this for all the textures will affect performance
                    half4 c0 = tex2D (_Tex0, fInput.uv);
                    half4 c1 = tex2D (_Tex1, fInput.uv);
                    half4 c2 = tex2D (_Tex2, fInput.uv);
                    half4 c3_ = tex2D (_Tex3, fInput.uv*-.25);
                    half4 c3 = tex2D (_Tex3, fInput.uv);
                    half4 c4 = tex2D (_Tex4, fInput.uv);
                    half4 c5 = tex2D (_Tex5, fInput.uv);
                    half4 c6 = tex2D (_Tex6, fInput.uv);
 
   
                   
          
                    float4 color = tex2D(_Tex0, fInput.uv)*_Color;
                   
 
                     if (fInput.col.x < _Blend0to1and1to2.x) color.rgb = c0; else 
                    if (fInput.col.x > _Blend0to1and1to2.x && fInput.col.x < _Blend0to1and1to2.y) color.rgb = lerp(c0,c1,((fInput.col.x - _Blend0to1and1to2.x)/(_Blend0to1and1to2.y-_Blend0to1and1to2.x)));else 
                    if (fInput.col.x > _Blend0to1and1to2.y && fInput.col.x < _Blend0to1and1to2.z) color.rgb = c1;else 
                    if (fInput.col.x > _Blend0to1and1to2.z && fInput.col.x < _Blend0to1and1to2.w) color.rgb = lerp(c1,c2,((fInput.col.x - _Blend0to1and1to2.z)/(_Blend0to1and1to2.w-_Blend0to1and1to2.z)));else 
                    if (fInput.col.x > _Blend0to1and1to2.w && fInput.col.x < _Blend2to3and3to4.x) color.rgb = c2;else 
                    if (fInput.col.x > _Blend2to3and3to4.x && fInput.col.x < _Blend2to3and3to4.y) color.rgb = lerp(c2,lerp(c3,c3_,.5),((fInput.col.x - _Blend2to3and3to4.x)/(_Blend2to3and3to4.y-_Blend2to3and3to4.x)));else 
                    if (fInput.col.x > _Blend2to3and3to4.y && fInput.col.x < _Blend2to3and3to4.z) color.rgb = lerp(c3,c3_,.5);else 
                    if (fInput.col.x > _Blend2to3and3to4.z && fInput.col.x < _Blend2to3and3to4.w) color.rgb = lerp(lerp(c3,c3_,.5),c4,((fInput.col.x - _Blend2to3and3to4.z)/(_Blend2to3and3to4.w-_Blend2to3and3to4.z)));else 
                    if (fInput.col.x > _Blend2to3and3to4.w && fInput.col.x < _Blend4to5and5to6.x) color.rgb = c4;else 
                    if (fInput.col.x > _Blend4to5and5to6.x && fInput.col.x < _Blend4to5and5to6.y) color.rgb = lerp(c4,c5,((fInput.col.x - _Blend4to5and5to6.x)/(_Blend4to5and5to6.y-_Blend4to5and5to6.x)));else 
                    if (fInput.col.x > _Blend4to5and5to6.y && fInput.col.x < _Blend4to5and5to6.z) color.rgb = c5;else 
                    if (fInput.col.x > _Blend4to5and5to6.z && fInput.col.x < _Blend4to5and5to6.w) color.rgb = lerp(c5,c6,((fInput.col.x - _Blend4to5and5to6.z)/(_Blend4to5and5to6.w-_Blend4to5and5to6.z)));else 
                    color.rgb = c6;
                    
                     color.rgb= lerp(color.rgb,c6,fInput.pole);
                    
                  fixed atten = LIGHT_ATTENUATION(fInput);
                  // color.rgb=normalize(fInput.normal);
                //  return  color*dot(normalize((mul(_Object2World, float4(0.0,0.0,0.0,1.0))-_WorldSpaceLightPos0)), -fInput.normal);
                    return  color*fInput.color*atten;
                    //*dot(normalize(fInput.worldvertpos-_WorldSpaceLightPos0), -fInput.normal);
                  
                    //return color;
               
 
                }
            ENDCG
        }
     
       Pass {
        
          // Blend One One
           	
            Tags {"LightMode" = "ForwardAdd" "Queue" ="Geometry" "RenderType" = "Geometry"}
          //  Blend One One
          Blend SrcAlpha OneMinusSrcAlpha 
            LOD 200
            ZWrite off
            
            Cull Back
            CGPROGRAM
// Upgrade NOTE: excluded shader from DX11 and Xbox360; has structs without semantics (struct v2f members viewDir,worldRefl)
//#pragma exclude_renderers d3d11 xbox360

                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"
               // #include "Lighting.cginc"
                #pragma fragmentoption ARB_fog_exp2
               #pragma fragmentoption ARB_precision_hint_fastest
                #pragma target 3.0
                #pragma multi_compile_fwdbase    
              
               #include "AutoLight.cginc"
                sampler2D _Tex0;
                sampler2D _Tex1;
                sampler2D _Tex2;
                sampler2D _Tex3;
                sampler2D _Tex4;
                sampler2D _Tex5;
                sampler2D _Tex6;
                float4 _Blend0to1and1to2;
                float4 _Blend2to3and3to4;
                float4 _Blend4to5and5to6;
                uniform float4 _Tex0_ST;
                 uniform float4 _Tex1_ST;
                  uniform float4 _Tex2_ST;
                   uniform float4 _Tex3_ST;
                    uniform float4 _Tex4_ST;
                     uniform float4 _Tex5_ST;
                      uniform float4 _Tex6_ST;
  				uniform float4 _Color;
             	uniform float _Radius;
                   uniform float4 _LightColor0; 
         uniform sampler2D _LightTextureB0; 
  
                struct v2f {
                    float4 pos : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float4 col : COLOR;
               		float3 normal : TEXCOORD1;
                    float3 worldvertpos : TEXCOORD2;
                     float posLight : TEXCOORD3;
                 fixed4 color: COLOR1;
                  float pole : TEXCOORD4;
                  LIGHTING_COORDS(5,6)
                };
            
             
                float rand(float3 co)
{
    return frac(sin( dot(co.xyz ,float3(12.9898,78.233,45.5432) )) * 43758.5453);
}
               
                v2f vert (appdata_base v) {
                    v2f OUT;
                 UNITY_INITIALIZE_OUTPUT(v2f, OUT);
                    
                     OUT.pos = UnityObjectToClipPos (v.vertex);
                
                    OUT.worldvertpos = mul(unity_ObjectToWorld, v.vertex);
OUT.posLight=length(mul(_WorldSpaceLightPos0,unity_ObjectToWorld).xyz - OUT.worldvertpos.xyz);
                       OUT.normal = v.normal;
                        OUT.pole = clamp(pow(abs(mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0)).y - OUT.worldvertpos.y)*_Radius/20,6),-1.0,1.0) ;
                    OUT.col = length(mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0))-OUT.worldvertpos)*_Radius ;
                    OUT.uv = TRANSFORM_TEX(v.texcoord, _Tex0);
         
            
                      TRANSFER_VERTEX_TO_FRAGMENT(OUT);
                    
                    return OUT;
                }
  half4 frag (v2f fInput) : COLOR {
  
   float3 normalDirection = normalize(fInput.normal);
 
            float3 viewDirection = normalize(
               _WorldSpaceCameraPos - float3(fInput.worldvertpos));
            float3 lightDirection;
            float attenuation;
            float attenuation_p;
            float distance = fInput.posLight;
			 attenuation_p =   tex2D(_LightTextureB0, float2(distance,0)).a;
            if (0.0 == _WorldSpaceLightPos0.w) // directional light?
            {
               attenuation = 1.0; // no attenuation
               lightDirection = 
                  normalize(float3(_WorldSpaceLightPos0.xyz));
            } 
            else // point or spot light
            {

                  lightDirection = normalize(mul(_WorldSpaceLightPos0,unity_ObjectToWorld).xyz - fInput.worldvertpos.xyz);
                  attenuation = attenuation_p;
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
 
  
  
  
  
  
  
  
  
                    half4 c0 = tex2D (_Tex0, fInput.uv);
                    half4 c1 = tex2D (_Tex1, fInput.uv);
                    half4 c2 = tex2D (_Tex2, fInput.uv);
                    half4 c3_ = tex2D (_Tex3, fInput.uv*-.25);
                    half4 c3 = tex2D (_Tex3, fInput.uv);
                    half4 c4 = tex2D (_Tex4, fInput.uv);
                    half4 c5 = tex2D (_Tex5, fInput.uv);
                    half4 c6 = tex2D (_Tex6, fInput.uv);
 
  
                   
                  
                   
             // atmo.a=0;
                    float4 color = tex2D(_Tex0, fInput.uv)*_Color;
                   
 
                    if (fInput.col.x < _Blend0to1and1to2.x) color.rgb = c0; else 
                    if (fInput.col.x > _Blend0to1and1to2.x && fInput.col.x < _Blend0to1and1to2.y) color.rgb = lerp(c0,c1,((fInput.col.x - _Blend0to1and1to2.x)/(_Blend0to1and1to2.y-_Blend0to1and1to2.x)));else 
                    if (fInput.col.x > _Blend0to1and1to2.y && fInput.col.x < _Blend0to1and1to2.z) color.rgb = c1;else 
                    if (fInput.col.x > _Blend0to1and1to2.z && fInput.col.x < _Blend0to1and1to2.w) color.rgb = lerp(c1,c2,((fInput.col.x - _Blend0to1and1to2.z)/(_Blend0to1and1to2.w-_Blend0to1and1to2.z)));else 
                    if (fInput.col.x > _Blend0to1and1to2.w && fInput.col.x < _Blend2to3and3to4.x) color.rgb = c2;else 
                    if (fInput.col.x > _Blend2to3and3to4.x && fInput.col.x < _Blend2to3and3to4.y) color.rgb = lerp(c2,lerp(c3,c3_,.5),((fInput.col.x - _Blend2to3and3to4.x)/(_Blend2to3and3to4.y-_Blend2to3and3to4.x)));else 
                    if (fInput.col.x > _Blend2to3and3to4.y && fInput.col.x < _Blend2to3and3to4.z) color.rgb = lerp(c3,c3_,.5);else 
                    if (fInput.col.x > _Blend2to3and3to4.z && fInput.col.x < _Blend2to3and3to4.w) color.rgb = lerp(lerp(c3,c3_,.5),c4,((fInput.col.x - _Blend2to3and3to4.z)/(_Blend2to3and3to4.w-_Blend2to3and3to4.z)));else 
                    if (fInput.col.x > _Blend2to3and3to4.w && fInput.col.x < _Blend4to5and5to6.x) color.rgb = c4;else 
                    if (fInput.col.x > _Blend4to5and5to6.x && fInput.col.x < _Blend4to5and5to6.y) color.rgb = lerp(c4,c5,((fInput.col.x - _Blend4to5and5to6.x)/(_Blend4to5and5to6.y-_Blend4to5and5to6.x)));else 
                    if (fInput.col.x > _Blend4to5and5to6.y && fInput.col.x < _Blend4to5and5to6.z) color.rgb = c5;else 
                    if (fInput.col.x > _Blend4to5and5to6.z && fInput.col.x < _Blend4to5and5to6.w) color.rgb = lerp(c5,c6,((fInput.col.x - _Blend4to5and5to6.z)/(_Blend4to5and5to6.w-_Blend4to5and5to6.z)));else 
                    color.rgb = c6;
                    color.rgb= lerp(color.rgb,c6,fInput.pole);
                      fixed atten = LIGHT_ATTENUATION(fInput);
                   return  color*float4(diffuseReflection, 1.0)*atten;
                  
               }
            ENDCG
            }
      Pass {
        ZWrite On
        ColorMask 0
    }
        
        
      
    }
    FallBack "VertexLit" 
}
