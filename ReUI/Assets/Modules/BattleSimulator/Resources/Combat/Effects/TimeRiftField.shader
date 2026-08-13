Shader "ThreeBody/TimeRiftField"
{
    Properties
    {
        _Spacing ("Spacing", Float) = 11
        _FieldCenter ("Field Center", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+80" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 world : TEXCOORD0;
            };

            float _Spacing;
            float4 _FieldCenter;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy - _FieldCenter.xy;
                return o;
            }

            float periodicDistance(float value, float spacing)
            {
                return abs(frac(value / spacing + 0.5) - 0.5) * spacing;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.world;
                float d0 = periodicDistance(p.y + sin(p.x * 0.19) * 1.35, _Spacing);
                float d1 = periodicDistance(p.x + sin(p.y * 0.17 + 1.7) * 1.35, _Spacing);
                float d2 = periodicDistance((p.x + p.y) * 0.7071068 + sin((p.x - p.y) * 0.11) * 1.65,
                                            _Spacing * 1.37);
                float d3 = periodicDistance((p.x - p.y) * 0.7071068 + sin((p.x + p.y) * 0.13 + 2.4) * 1.65,
                                            _Spacing * 1.61);
                float d = min(min(d0, d1), min(d2, d3));

                float glow = saturate(1.0 - d / 2.6);
                float body = saturate(1.0 - d / 0.82);
                float core = saturate(1.0 - d / 0.22);
                float flicker = 0.84 + 0.16 * sin(_Time.y * 18.0 + p.x * 0.7 + p.y * 0.43);
                float alpha = (glow * 0.18 + body * 0.52 + core) * flicker;
                clip(alpha - 0.008);
                float3 color = lerp(float3(0.25, 0.34, 1.0), float3(0.92, 0.54, 1.0),
                                    saturate(body + sin(p.x * 0.2) * 0.2));
                color += core * float3(0.75, 0.95, 1.0) * 1.8;
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
