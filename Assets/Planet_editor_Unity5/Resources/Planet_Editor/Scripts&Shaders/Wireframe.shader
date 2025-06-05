Shader "Custom/Wireframe"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WireframeColor("Wireframe front colour", color) = (1.0, 1.0, 1.0, 1.0)
        _WireframeWidth("Wireframe Width", float) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="Transparent"}
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 col : COLOR;
            };

            // We add our barycentric variables to the geometry struct.
            struct g2f {
                float4 pos : SV_POSITION;
                float3 barycentric : TEXCOORD0;
                float4 col : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.col = length(mul(unity_ObjectToWorld, float4(0.0,0.0,0.0,1.0))- mul(unity_ObjectToWorld, v.vertex).xyz);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            // This applies the barycentric coordinates to each vertex in a triangle.
            [maxvertexcount(3)]
            void geom(triangle v2f IN[3], inout TriangleStream<g2f> triStream) {
                bool hidden = false;
                if (IN[0].col.x < 18 || IN[1].col.x < 18 || IN[2].col.x < 18) {
                    hidden = true;
                }

                g2f o;
                o.pos = IN[0].vertex;
                o.barycentric = hidden ? float3(0.5, 0.5, 0.5) : float3(1.0, 0.0, 0.0);
                o.col = IN[0].col;
                triStream.Append(o);
                o.pos = IN[1].vertex;
                o.barycentric = hidden ? float3(0.5, 0.5, 0.5) : float3(0.0, 1.0, 0.0);
                o.col = IN[1].col;
                triStream.Append(o);
                o.pos = IN[2].vertex;
                o.barycentric = hidden ? float3(0.5, 0.5, 0.5) : float3(0.0, 0.0, 1.0);
                o.col = IN[2].col;
                triStream.Append(o);
            }

            fixed4 _WireframeColor;
            float _WireframeWidth;

            // These are all pixels(?), not just the specific vertices we defined in geom.
            // All fields seen here have been interpolated between those extremes through some magic process
            fixed4 frag(g2f i) : SV_Target
            {
                // Find the barycentric coordinate closest to the edge.
                float closest = min(i.barycentric.x, min(i.barycentric.y, i.barycentric.z));
                float delta = fwidth(closest);
                closest = smoothstep(0.5*delta, 1.5*delta, closest);
                // Set alpha to 1 if within the threshold, else 0.
                float alpha = step(closest, _WireframeWidth);

                if (alpha != 0) {
                    alpha = step(22, i.col.x);
                }

                alpha *= i.col.x > 25.5 ? 0.75 : 0.4;

                // Set to our backwards facing wireframe colour.
                return fixed4(_WireframeColor.r, _WireframeColor.g, _WireframeColor.b, alpha);
            }
            ENDCG
        }
    }
}
