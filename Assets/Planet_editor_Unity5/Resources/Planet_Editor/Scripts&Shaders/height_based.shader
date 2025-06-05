// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/height_based"
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
        Lighting Off
        Fog { Mode Off }
        Pass {
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
                    float4 col : COLOR;
                };
               
                v2f vert (appdata_base vInput) {
                    v2f OUT;
                    OUT.pos = UnityObjectToClipPos (vInput.vertex);
                    OUT.uv = TRANSFORM_TEX(vInput.texcoord, _Tex0);
                    OUT.col = length(mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0))- mul(unity_ObjectToWorld, vInput.vertex).xyz)*_Radius;
                    return OUT;
                }
               
                half4 frag (v2f fInput) : COLOR {
                    half4 c0 = tex2D (_Tex0, fInput.uv);
                    half4 c1 = tex2D (_Tex1, fInput.uv);
                    half4 c2 = tex2D (_Tex2, fInput.uv);
                    half4 c3 = tex2D (_Tex3, fInput.uv);
                    half4 c4 = tex2D (_Tex4, fInput.uv);
                    half4 c5 = tex2D (_Tex5, fInput.uv);
                    half4 c6 = tex2D (_Tex6, fInput.uv);
 
                    if (fInput.col.x < _Blend0to1and1to2.x) return c0;
                    if (fInput.col.x > _Blend0to1and1to2.x && fInput.col.x < _Blend0to1and1to2.y) return lerp(c0,c1,((fInput.col.x - _Blend0to1and1to2.x)/(_Blend0to1and1to2.y-_Blend0to1and1to2.x)));
                    if (fInput.col.x > _Blend0to1and1to2.y && fInput.col.x < _Blend0to1and1to2.z) return c1;
                    if (fInput.col.x > _Blend0to1and1to2.z && fInput.col.x < _Blend0to1and1to2.w) return lerp(c1,c2,((fInput.col.x - _Blend0to1and1to2.z)/(_Blend0to1and1to2.w-_Blend0to1and1to2.z)));
                    if (fInput.col.x > _Blend0to1and1to2.w && fInput.col.x < _Blend2to3and3to4.x) return c2;
                    if (fInput.col.x > _Blend2to3and3to4.x && fInput.col.x < _Blend2to3and3to4.y) return lerp(c2,c3,((fInput.col.x - _Blend2to3and3to4.x)/(_Blend2to3and3to4.y-_Blend2to3and3to4.x)));
                    if (fInput.col.x > _Blend2to3and3to4.y && fInput.col.x < _Blend2to3and3to4.z) return c3;
                    if (fInput.col.x > _Blend2to3and3to4.z && fInput.col.x < _Blend2to3and3to4.w) return lerp(c3,c4,((fInput.col.x - _Blend2to3and3to4.z)/(_Blend2to3and3to4.w-_Blend2to3and3to4.z)));
                    if (fInput.col.x > _Blend2to3and3to4.w && fInput.col.x < _Blend4to5and5to6.x) return c4;
                    if (fInput.col.x > _Blend4to5and5to6.x && fInput.col.x < _Blend4to5and5to6.y) return lerp(c4,c5,((fInput.col.x - _Blend4to5and5to6.x)/(_Blend4to5and5to6.y-_Blend4to5and5to6.x)));
                    if (fInput.col.x > _Blend4to5and5to6.y && fInput.col.x < _Blend4to5and5to6.z) return c5;
                    if (fInput.col.x > _Blend4to5and5to6.z && fInput.col.x < _Blend4to5and5to6.w) return lerp(c5,c6,((fInput.col.x - _Blend4to5and5to6.z)/(_Blend4to5and5to6.w-_Blend4to5and5to6.z)));
                    return c6;
 
                }
            ENDCG
        }
    }
}
