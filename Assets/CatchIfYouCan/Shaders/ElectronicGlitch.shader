Shader "CatchIfYouCan/ElectronicGlitch"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0.8, 0.85, 0.9, 1)
        _GlitchAmount ("Glitch Amount", Range(0, 1)) = 0.35
        _ScanlineStrength ("Scanlines", Range(0, 1)) = 0.25
        _Distortion ("Distortion", Range(0, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _GlitchAmount;
                half _ScanlineStrength;
                half _Distortion;
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
            };

            half Hash11(half p)
            {
                return frac(sin(p * 127.1) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half t = _Time.y;
                half band = floor(input.uv.y * 32.0);
                half shift = (Hash11(band + floor(t * 20.0)) - 0.5) * _Distortion * _GlitchAmount;
                half2 uv = input.uv + half2(shift, 0);
                uv.x += sin(uv.y * 40.0 + t * 12.0) * _Distortion * _GlitchAmount;
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                half scan = sin(input.uv.y * 600.0) * _ScanlineStrength * _GlitchAmount;
                col.rgb -= scan;
                half rgbSplit = _GlitchAmount * 0.01;
                col.r = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + half2(rgbSplit, 0)).r;
                col.b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - half2(rgbSplit, 0)).b;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
