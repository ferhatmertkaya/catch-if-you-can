Shader "CatchIfYouCan/PlanarMirror"
{
    // Aged Victorian mirror glass.
    //
    // The reflection arrives as a render texture drawn by a camera at the player's mirrored eye,
    // with the player's own field of view, and is sampled in SCREEN SPACE. That is the whole
    // reason this shader exists: a reflection camera whose pose is the reflected player pose
    // renders the same pixels the player is looking at, so the correct place to read it is where
    // this fragment lands on screen - not where it lands on the mesh.
    //
    // The horizontal flip below is the one flip a mirror needs, and it is here rather than in the
    // camera because a Transform cannot hold the basis it belongs to. Reflecting forward, right
    // and up across a plane produces a LEFT-handed basis, which is not a rotation; building the
    // pose from reflected forward and up instead gives a proper rotation whose right vector is
    // the negative of the reflected one. Flipping screen u undoes exactly that, and it is exact
    // rather than approximate because a plain perspective frustum is symmetric about its centre.
    // Doing it this way means no GL.invertCulling anywhere - nothing renders with inverted
    // winding, so nothing can be left inverted after an early return.
    //
    // Unlit on purpose. A lit mirror is a surface the room's light falls on, which takes a bite
    // out of the reflection before anybody sees it; the ageing here is in the glass, not in a
    // lighting accident.
    Properties
    {
        _ReflectionTex  ("Reflection", 2D) = "black" {}
        _Tint           ("Glass Tint", Color) = (0.94, 0.92, 0.88, 1)
        _Exposure       ("Reflection Exposure", Range(0.4, 1.4)) = 0.94
        _GrimeStrength  ("Grime", Range(0, 0.5)) = 0.13
        _GrimeScale     ("Grime Scale", Range(0.5, 12)) = 3.1
        _EdgeDirt       ("Edge Dirt", Range(0, 0.7)) = 0.34
        _EdgeWidth      ("Edge Dirt Width", Range(0.01, 0.45)) = 0.17
        _ScratchStrength("Micro Scratches", Range(0, 0.25)) = 0.05
        _Desaturate     ("Age Desaturation", Range(0, 0.6)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "PlanarMirror"

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _Exposure;
                float  _GrimeStrength;
                float  _GrimeScale;
                float  _EdgeDirt;
                float  _EdgeWidth;
                float  _ScratchStrength;
                float  _Desaturate;
            CBUFFER_END

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.screenPos = ComputeScreenPos(p.positionCS);
                OUT.uv = IN.uv;
                return OUT;
            }

            // Value noise. Three octaves at frequencies that are not multiples of one another, so
            // the result does not settle into the grid it is built on - obvious procedural tiling
            // is the difference between aged glass and a noise texture.
            float hash21(float2 p)
            {
                p = frac(p * float2(127.31, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                return vnoise(p) * 0.55 +
                       vnoise(p * 2.17 + 19.3) * 0.30 +
                       vnoise(p * 4.61 + 41.7) * 0.15;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);

                // The mirror flip. See the note at the top: it replaces the right vector the
                // camera pose cannot carry.
                screenUV.x = 1.0 - screenUV.x;

                half3 reflection =
                    SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, screenUV).rgb;

                // --- ageing, all of it restrained -------------------------------------------
                // The reflection has to stay the image. Everything below is a small multiply on
                // top of it; none of it is allowed to become the thing you look at.

                // Low-frequency mottle, centred on 1 so it darkens and lifts rather than only
                // darkening - old silvering is uneven, not merely dirty.
                float mottle = fbm(IN.uv * _GrimeScale) - 0.5;

                // Dirt gathers where the glass meets the frame. Modulated by its own noise so the
                // band is not a clean rectangle.
                float2 d = min(IN.uv, 1.0 - IN.uv);
                float edge = 1.0 - smoothstep(0.0, _EdgeWidth, min(d.x, d.y));
                edge *= 0.55 + 0.45 * fbm(IN.uv * _GrimeScale * 2.3 + 7.1);

                // Sparse near-horizontal hairlines. Stretched hard along u so they read as
                // scratches rather than as noise.
                float scratch = smoothstep(0.92, 1.0, vnoise(float2(IN.uv.x * 190.0 + IN.uv.y * 11.0,
                                                                   IN.uv.y * 2.7)));

                float grime = saturate(mottle * _GrimeStrength * 2.0 + edge * _EdgeDirt);

                half3 col = reflection * _Tint.rgb * _Exposure;
                col *= (1.0 - grime);
                col += scratch * _ScratchStrength * 0.5;

                // A touch of the colour drained out of it, and slightly more where it is dirtiest.
                float luma = dot(col, half3(0.2126, 0.7152, 0.0722));
                col = lerp(col, half3(luma, luma, luma), saturate(_Desaturate + grime * 0.35));

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    // No fallback. A built-in-pipeline fallback under URP draws solid magenta, which is the
    // failure this project has already shipped once.
    Fallback Off
}
