Shader "CatchIfYouCan/GhostDissolve"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (0.1, 0.9, 0.3, 0.85)
        _EmissionColor ("Emission", Color) = (0.2, 1, 0.4, 1)
        _DissolveAmount ("Dissolve", Range(0, 1)) = 0
        _DissolveEdge ("Edge Width", Range(0, 0.2)) = 0.05
        _NoiseScale ("Noise Scale", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

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
                half4 _EmissionColor;
                half _DissolveAmount;
                half _DissolveEdge;
                half _NoiseScale;
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

            half Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
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
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half noise = Hash21(input.uv * _NoiseScale);
                half edge = 1.0 - smoothstep(_DissolveAmount, _DissolveAmount + _DissolveEdge, noise);
                clip(noise - _DissolveAmount);
                half3 emission = _EmissionColor.rgb * (1.0 + edge * 4.0);
                albedo.rgb += emission;
                albedo.a *= saturate(1.0 - _DissolveAmount);
                return albedo;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
