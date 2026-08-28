Shader "CatchIfYouCan/SpectralGrid"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0.2, 1, 0.35, 0.8)
        _GridScale ("Grid Scale", Float) = 4
        _LineWidth ("Line Width", Range(0.01, 0.5)) = 0.08
        _ScrollSpeed ("Scroll Speed", Float) = 0.5
        _Pulse ("Pulse", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GridColor;
                half _GridScale;
                half _LineWidth;
                half _ScrollSpeed;
                half _Pulse;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half GridLine(half2 uv, half width)
            {
                half2 grid = abs(frac(uv - 0.5) - 0.5) / fwidth(uv);
                half line = 1.0 - min(grid.x, grid.y);
                return smoothstep(1.0 - width, 1.0, line);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y * _ScrollSpeed;
                half2 uv = input.worldPos.xz * _GridScale + half2(0, t);
                half line = GridLine(uv, _LineWidth);
                half pulse = 0.5 + 0.5 * sin(_Time.y * 3.0) * _Pulse;
                half4 col = _GridColor;
                col.a *= line * pulse;
                col.rgb *= line * (1.0 + pulse);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
