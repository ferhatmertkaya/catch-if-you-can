Shader "CatchIfYouCan/UVEvidence"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Hidden Color", Color) = (0.02, 0.02, 0.02, 0)
        _RevealColor ("UV Color", Color) = (0.35, 0.05, 0.8, 1)
        _UVReveal ("UV Reveal", Range(0, 1)) = 0
        _GlowStrength ("Glow", Range(0, 5)) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
                half4 _RevealColor;
                half _UVReveal;
                half _GlowStrength;
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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half reveal = saturate(_UVReveal);
                half4 hidden = _BaseColor * tex;
                half4 shown = _RevealColor * tex;
                shown.rgb *= (1.0 + _GlowStrength * reveal);
                half4 col = lerp(hidden, shown, reveal);
                col.a = lerp(hidden.a, shown.a, reveal);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
