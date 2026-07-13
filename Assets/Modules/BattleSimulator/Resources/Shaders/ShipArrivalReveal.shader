Shader "ThreeBody/ShipArrivalReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Reveal ("Bow To Stern Reveal", Range(0,1)) = 0
        _EdgeColor ("Arrival Edge", Color) = (0.45,0.92,1,1)
        _EdgeWidth ("Edge Width", Range(0.002,0.2)) = 0.055
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

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
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _EdgeColor;
            float _Reveal;
            float _EdgeWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Ship source sprites face upward in local space.  Moving this
                // threshold downward therefore reveals bow before stern.
                float threshold = 1.0 - saturate(_Reveal);
                clip(i.uv.y - threshold);
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                float edge = 1.0 - saturate((i.uv.y - threshold) / max(_EdgeWidth, 0.002));
                c.rgb = lerp(c.rgb, _EdgeColor.rgb * max(c.a, 0.4), edge * _EdgeColor.a);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
