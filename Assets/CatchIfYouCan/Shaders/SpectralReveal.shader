Shader "CatchIfYouCan/SpectralReveal"
{
    // What a body looks like standing in a field of projected points.
    //
    // This is applied to a PRESENTATION SHELL that shares the gameplay ghost's mesh and bones -
    // never to the ghost's own renderer. The gameplay ghost keeps its own material at all
    // times; nothing here mutates it, and when the reveal ends the shell is simply switched off.
    //
    // The points are computed in the PROJECTOR's frame, not the ghost's, so a dot on the ghost's
    // shoulder lines up with the dots on the wall behind it. That is what sells it as one field
    // falling across a room rather than a texture on a monster.
    Properties
    {
        _DotColor   ("Dot Colour", Color) = (0.2, 1, 0.35, 1)
        _BodyColor  ("Body Tint", Color)  = (0.08, 0.35, 0.16, 0.16)
        _Density    ("Angular Density", Range(4, 128)) = 34
        _DotSize    ("Dot Size", Range(0.02, 0.6)) = 0.22
        _Range      ("Projector Range (m)", Float) = 6
        _HalfAngle  ("Projector Half Angle (rad)", Float) = 0.61
        _Intensity  ("Intensity", Range(0, 8)) = 2.6
        _RimPower   ("Rim Power", Range(0.5, 8)) = 2.5
        _Reveal     ("Reveal", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+110" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "SpectralReveal"

            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _SKINNED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4   _DotColor;
                half4   _BodyColor;
                half    _Density;
                half    _DotSize;
                half    _Range;
                half    _HalfAngle;
                half    _Intensity;
                half    _RimPower;
                half    _Reveal;
            CBUFFER_END

            // Where the projector is. Pushed per renderer, so several projectors can each light
            // their own shell without any of them owning a material instance.
            float4x4 _ProjectorWorldToLocal;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Into the projector's frame. Its +Y is the axis it throws along.
                float3 lp = mul(_ProjectorWorldToLocal, float4(input.positionWS, 1.0)).xyz;

                float axial = lp.y;
                if (axial <= 0.0 || axial >= _Range) return half4(0, 0, 0, 0);

                float radial = length(lp.xz);
                float coneRadius = axial * tan(_HalfAngle);
                if (radial >= coneRadius) return half4(0, 0, 0, 0);

                float2 angular = lp.xz / max(axial, 1e-4);
                float2 cell = frac(angular * _Density) - 0.5;
                float  cellDist = length(cell);

                float aa      = max(fwidth(cellDist), 1e-4);
                float dotMask = 1.0 - smoothstep(_DotSize - aa, _DotSize + aa, cellDist);

                float distanceFade = saturate(1.0 - axial / _Range);
                distanceFade *= distanceFade;

                float edgeFade = saturate(1.0 - radial / max(coneRadius, 1e-4));

                // A faint rim so the silhouette reads as a body between the points rather than
                // as points floating in the shape of one.
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float  rim = pow(1.0 - saturate(dot(normalize(input.normalWS), viewDir)), _RimPower);

                float shell = distanceFade * edgeFade * _Reveal;

                half3 rgb = _DotColor.rgb * dotMask * _Intensity * shell
                          + _BodyColor.rgb * rim * shell;

                half alpha = saturate((dotMask * _Intensity + rim * _BodyColor.a) * shell);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
