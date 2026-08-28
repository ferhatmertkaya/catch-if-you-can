Shader "CatchIfYouCan/UI/Slime"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.15, 0.55, 0.25, 0.85)
        _DripSpeed ("Drip Speed", Float) = 0.35
        _DripScale ("Drip Scale", Float) = 3
        _DripStrength ("Drip Strength", Range(0, 0.3)) = 0.08
        _Glow ("Glow", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
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
            float _DripSpeed;
            float _DripScale;
            float _DripStrength;
            float _Glow;

            v2f vert(appdata v)
            {
                v2f o;
                float drip = sin((v.uv.x * _DripScale + _Time.y * _DripSpeed) * 6.28318) * _DripStrength;
                v.vertex.y += drip;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                col.rgb += _Glow * col.g;
                return col;
            }
            ENDCG
        }
    }
}
