Shader "CatchIfYouCan/Portal"
{
    // A hole in the world, not a screen on a wall.
    //
    // The view arrives as a render texture drawn by a camera standing where the player would be
    // if they were already on the far side, and is sampled in SCREEN SPACE. That is what makes
    // it an opening: the ray through any pixel of the surface is the continuation of the
    // player's own ray through that same pixel, so the far room slides with parallax exactly as
    // a real doorway does. A texture mapped with the mesh's own UVs is a television.
    //
    // NO horizontal flip, and that is the one line where this differs from PlanarMirror. A
    // mirror's camera basis is improper - reflecting forward, right and up gives a left-handed
    // set no Transform can hold - so its shader flips screen u to supply the handedness the
    // pose could not. A portal is a rigid motion: the basis stays right-handed, nothing is
    // flipped, and flipping here would put the far room's left on the player's right.
    Properties
    {
        _PortalTex      ("Portal View", 2D) = "black" {}
        _RimColor       ("Rim Colour", Color) = (0.35, 0.75, 1.0, 1)
        _RimInner       ("Rim Inner Colour", Color) = (0.75, 0.45, 1.0, 1)
        _RimWidth       ("Rim Width", Range(0.002, 0.35)) = 0.09
        _RimPower       ("Rim Falloff", Range(0.5, 8)) = 2.4
        _RimIntensity   ("Rim Intensity", Range(0, 6)) = 2.1
        _Distortion     ("Edge Distortion", Range(0, 0.06)) = 0.014
        _DistortSpeed   ("Distortion Speed", Range(0, 4)) = 0.7
        _Tint           ("View Tint", Color) = (0.92, 0.95, 1.0, 1)
        _Opacity        ("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        // Transparent, so the opening can come UP rather than appear. An opaque portal can only
        // be switched on, and a doorway that snaps from wall to window in one frame reads as a
        // bug. ZWrite is off because this is a single flat quad inside a door frame with
        // nothing between it and the frame - there is no sorting for it to get wrong.
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Portal"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float4 _RimInner;
                float4 _Tint;
                float  _RimWidth;
                float  _RimPower;
                float  _RimIntensity;
                float  _Distortion;
                float  _DistortSpeed;
                float  _Opacity;
            CBUFFER_END

            TEXTURE2D(_PortalTex);
            SAMPLER(sampler_PortalTex);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
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

            float hash21(float2 p)
            {
                p = frac(p * float2(127.31, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p); float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x),
                            lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);

                // How close this fragment is to the edge of the opening, 0 in the middle and 1
                // at the frame. Everything paranormal below is driven by this, so the centre -
                // the part that has to stay a clean view of the far room - is untouched.
                float2 d = min(IN.uv, 1.0 - IN.uv);
                float edge = 1.0 - saturate(min(d.x, d.y) / max(_RimWidth, 1e-4));
                float rim = pow(edge, _RimPower);

                // The view is dragged very slightly sideways near the boundary, which reads as
                // air bending round the opening. Scaled by rim so the middle is undistorted:
                // the brief is a spatial opening, and a wobbling centre is a screen effect.
                float t = _Time.y * _DistortSpeed;
                float2 wobble = float2(vnoise(IN.uv * 7.0 + t) - 0.5,
                                       vnoise(IN.uv * 7.0 - t + 19.7) - 0.5);
                screenUV += wobble * _Distortion * rim;

                half3 view = SAMPLE_TEXTURE2D(_PortalTex, sampler_PortalTex, screenUV).rgb;
                view *= _Tint.rgb;

                // Two colours across the band, so the ring has depth rather than being one
                // flat glow. Added, not blended, because a portal edge is light rather than paint.
                half3 rimColour = lerp(_RimColor.rgb, _RimInner.rgb, saturate(edge * 1.4));
                float shimmer = 0.75 + 0.25 * vnoise(IN.uv * 14.0 + t * 1.7);

                half3 col = view + rimColour * rim * _RimIntensity * shimmer;

                // The edge comes up before the middle does. While the opening is forming, the
                // rim is already burning and the view behind it is still fading in, which is
                // what makes it read as something tearing open rather than as a picture being
                // switched on.
                float alpha = saturate(_Opacity + rim * _Opacity * 0.6);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    // No fallback. A built-in-pipeline fallback under URP draws solid magenta.
    Fallback Off
}
