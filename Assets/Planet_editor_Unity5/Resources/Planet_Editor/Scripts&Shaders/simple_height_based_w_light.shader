// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/simple_height_based"
{
    Properties {
        _Tex0 ("Tex 0", 2D) = "white" {}
        _Tex1 ("Tex 1", 2D) = "white" {}
        _Tex2 ("Tex 2", 2D) = "white" {}
        _Tex3 ("Tex 3", 2D) = "white" {}
        _Tex4 ("Tex 4", 2D) = "white" {}
        _Tex5 ("Tex 5", 2D) = "white" {}
        _Tex6 ("Tex 6", 2D) = "white" {}
        
          _Radius("Radius_k", Float) = 1
        
        _Blend0to1and1to2 ("Blend between 0 and 1, 1 and 2", Vector) = (0,1,2,3)
        _Blend2to3and3to4 ("Blend between 2 and 3, 3 and 4", Vector) = (0,1,2,3)
        _Blend4to5and5to6 ("Blend between 4 and 5, 5 and 6", Vector) = (0,1,2,3)
    }
    SubShader {
         Tags { "RenderType"="Opaque"}
        Fog { Mode Off }
        Pass {
         Tags { "LightMode" = "Vertex" }
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"
                #pragma target 3.0
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
                  
       	uniform float _Radius;
                struct v2f {
                    float4 pos : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float3 worldvertpos : TEXCOORD1;
                     float3 pole : TEXCOORD2;
                    float4 col : COLOR;
                    float4 coll : COLOR1;
                };
               
                v2f vert (appdata_base vInput) {
                    v2f OUT;
              
                    OUT.pos = UnityObjectToClipPos (vInput.vertex);
                    OUT.uv = TRANSFORM_TEX(vInput.texcoord, _Tex0);
                    OUT.worldvertpos = mul(unity_ObjectToWorld, vInput.vertex).xyz;
                    OUT.col = length(mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0))- mul(unity_ObjectToWorld, vInput.vertex).xyz)*_Radius;
                    OUT.pole = clamp(pow(abs(mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0)).y - OUT.worldvertpos.y)*_Radius/20,6),-1.0,1.0) ;
                     // per vertex light calc
               fixed3 lightDirection;
               fixed attenuation;
               // add diffuse
               if(unity_LightPosition[0].w == 0.0)//directional light
               {
                  attenuation = 2;
                  lightDirection = normalize(mul(unity_LightPosition[0],UNITY_MATRIX_IT_MV).xyz);
               }
               else// point or spot light
               {
                  lightDirection = normalize(mul(unity_LightPosition[0],UNITY_MATRIX_IT_MV).xyz - vInput.vertex.xyz);
                  attenuation = 1.0/(length(mul(unity_LightPosition[0],UNITY_MATRIX_IT_MV).xyz - vInput.vertex.xyz)) * 0.5;
               }
               fixed3 normalDirction = normalize(vInput.normal);
               fixed3 diffuseLight =  unity_LightColor[0].xyz * max(dot(normalDirction,lightDirection),0);
               // combine the lights (diffuse + ambient)
               OUT.coll.xyz = diffuseLight * attenuation ;
              
                    return OUT;
                }
               
                half4 frag (v2f fInput) : COLOR {
                    half4 c0 = tex2D (_Tex0, fInput.uv);
                    half4 c1 = tex2D (_Tex1, fInput.uv);
                    half4 c2 = tex2D (_Tex2, fInput.uv);
                    half4 c3_ = tex2D (_Tex3, fInput.uv*-.25);
                    half4 c3 = tex2D (_Tex3, fInput.uv);
                    half4 c4 = tex2D (_Tex4, fInput.uv);
                    half4 c5 = tex2D (_Tex5, fInput.uv);
                    half4 c6 = tex2D (_Tex6, fInput.uv);
 float4 color = tex2D(_Tex0, fInput.uv);
 
                    if (fInput.col.x < _Blend0to1and1to2.x) color = c0;else
                    if (fInput.col.x > _Blend0to1and1to2.x && fInput.col.x < _Blend0to1and1to2.y) color = lerp(c0,c1,((fInput.col.x - _Blend0to1and1to2.x)/(_Blend0to1and1to2.y-_Blend0to1and1to2.x)));else
                    if (fInput.col.x > _Blend0to1and1to2.y && fInput.col.x < _Blend0to1and1to2.z) color = c1;else
                    if (fInput.col.x > _Blend0to1and1to2.z && fInput.col.x < _Blend0to1and1to2.w) color = lerp(c1,c2,((fInput.col.x - _Blend0to1and1to2.z)/(_Blend0to1and1to2.w-_Blend0to1and1to2.z)));else
                    if (fInput.col.x > _Blend0to1and1to2.w && fInput.col.x < _Blend2to3and3to4.x) color = c2;else
                  if (fInput.col.x > _Blend2to3and3to4.x && fInput.col.x < _Blend2to3and3to4.y) color.rgb = lerp(c2,lerp(c3,c3_,.5),((fInput.col.x - _Blend2to3and3to4.x)/(_Blend2to3and3to4.y-_Blend2to3and3to4.x)));else 
                    if (fInput.col.x > _Blend2to3and3to4.y && fInput.col.x < _Blend2to3and3to4.z) color.rgb = lerp(c3,c3_,.5);else 
                    if (fInput.col.x > _Blend2to3and3to4.z && fInput.col.x < _Blend2to3and3to4.w) color.rgb = lerp(lerp(c3,c3_,.5),c4,((fInput.col.x - _Blend2to3and3to4.z)/(_Blend2to3and3to4.w-_Blend2to3and3to4.z)));else 
                     if (fInput.col.x > _Blend2to3and3to4.w && fInput.col.x < _Blend4to5and5to6.x) color = c4;else
                    if (fInput.col.x > _Blend4to5and5to6.x && fInput.col.x < _Blend4to5and5to6.y) color = lerp(c4,c5,((fInput.col.x - _Blend4to5and5to6.x)/(_Blend4to5and5to6.y-_Blend4to5and5to6.x)));else
                    if (fInput.col.x > _Blend4to5and5to6.y && fInput.col.x < _Blend4to5and5to6.z) color = c5;else
                    if (fInput.col.x > _Blend4to5and5to6.z && fInput.col.x < _Blend4to5and5to6.w) color = lerp(c5,c6,((fInput.col.x - _Blend4to5and5to6.z)/(_Blend4to5and5to6.w-_Blend4to5and5to6.z)));else
                    color = c6;
                    color.rgb= lerp(color.rgb,c6,fInput.pole);
                    return color*fInput.coll;
 
                }
            ENDCG
        }
    }
}
