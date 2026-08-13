// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Sprites/Additive"
{
	Properties
	{
		[PerRendererData] _MainTex ("Base", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_HdrIntensity ("HDR Intensity", Float) = 1
		[MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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
		Fog { Mode Off }
		Blend One One

		Pass
		{
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile DUMMY PIXELSNAP_ON
			#include "UnityCG.cginc"
			
			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex   : SV_POSITION;
				// Keep values above 1.0 intact for native HDR output. fixed4
				// silently saturated the high-luminance combat colours.
				float4 color    : COLOR;
				half2 texcoord  : TEXCOORD0;
			};
			
			float4 _Color;
			float _HdrIntensity;
			float _NativeHdrOutputActive;

			v2f vert(appdata_t IN)
			{
				v2f OUT;
				OUT.vertex = UnityObjectToClipPos(IN.vertex);
				OUT.texcoord = IN.texcoord;
				OUT.color = IN.color * _Color;
				#ifdef PIXELSNAP_ON
				OUT.vertex = UnityPixelSnap (OUT.vertex);
				#endif

				return OUT;
			}

			sampler2D _MainTex;

			half4 frag(v2f IN) : SV_Target
			{
				half4 c = (half4)tex2D(_MainTex, IN.texcoord) * (half4)IN.color;
				c.rgb *= c.a;
				// Float uniform preserves values above the UNorm vertex-colour limit.
				c.rgb *= lerp(1.0h, (half)_HdrIntensity, saturate((half)_NativeHdrOutputActive));
				return c;
			}
		ENDCG
		}
	}
}
