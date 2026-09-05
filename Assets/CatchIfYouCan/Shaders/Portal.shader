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
    //
    // THE SILHOUETTE IS AN ELLIPSE, CUT HERE. The mesh is still a quad, because a quad is what
    // screen-space sampling and a rectangular render texture want, but everything outside the
    // oval ends at alpha zero. The previous version derived its rim from min(uv, 1-uv), which
    // is a RECTANGLE - so the "portal" was a glowing box, and at full opacity the whole quad
    // including its corners showed the far room. That is the single biggest reason this never
    // looked like a portal.
    Properties
    {
        _PortalTex      ("Portal View", 2D) = "black" {}

        // Colour is authored, never hard-coded: the game ships spectral green but the same
        // shader has to be able to be blue, red or violet without an edit.
        [HDR] _CoreColor   ("Core Colour", Color) = (0.90, 1.00, 0.94, 1)
        [HDR] _EnergyColor ("Energy Colour", Color) = (0.34, 1.00, 0.41, 1)
        [HDR] _OuterColor  ("Outer Colour", Color) = (0.09, 0.36, 0.19, 1)

        _CoreIntensity   ("Core Intensity", Range(0, 16)) = 6.0
        _EnergyIntensity ("Energy Intensity", Range(0, 16)) = 2.6

        _RimWidth        ("Rim Width", Range(0.01, 0.9)) = 0.26
        _RimSoftness     ("Rim Softness", Range(0.05, 4)) = 1.15

        _NoiseScale      ("Noise A Scale", Range(0.5, 24)) = 5.5
        _NoiseStrength   ("Noise Strength", Range(0, 0.6)) = 0.16
        _NoiseSpeed      ("Noise A Speed", Range(0, 4)) = 0.35

        _SecondaryNoiseScale ("Noise B Scale", Range(0.5, 40)) = 13.0
        _SecondaryNoiseSpeed ("Noise B Speed", Range(0, 6)) = 0.85

        _DistortionStrength ("View Distortion", Range(0, 0.12)) = 0.018
        _RotationSpeed      ("Rotation Speed", Range(-3, 3)) = 0.22

        _PulseSpeed      ("Pulse Speed", Range(0, 12)) = 2.1
        _PulseStrength   ("Pulse Strength", Range(0, 1)) = 0.14

        _Opacity         ("Opacity", Range(0, 1)) = 1
        _ViewOpacity     ("Destination Opacity", Range(0, 1)) = 1

        _Tint            ("View Tint", Color) = (0.92, 0.98, 0.94, 1)

        // Semi-axes of the oval in normalised (-1..1) surface space, and the quad's own
        // width/height. Written by PortalSurface from the doorway it was sized to; leaving the
        // oval a little inside the quad is what gives the outer energy somewhere to live
        // without the glow being clipped by the door frame.
        _Fit             ("Breach Half Size", Vector) = (0.78, 0.90, 0, 0)
        _Aspect          ("Surface Aspect", Float) = 0.44

        // How far open the tear is, 0 to 1. At ZERO the breach has no size at all and this
        // shader draws nothing anywhere - the wall is whole. Everything else is unchanged by it.
        _Open            ("Tear Open", Range(0, 1)) = 1
        _TearAmount      ("Tear Ragged", Range(0, 0.5)) = 0.16
        _TearScale       ("Tear Scale", Range(0.5, 24)) = 4.5

        // ---- purchased artwork ----------------------------------------------------------
        // A bought portal pack is shaders, materials, textures and particles. Under URP its
        // SHADERS are unusable - they are authored against HDRP's shader library and resolve
        // to the magenta error shader - but its TEXTURES are just images, and they are where
        // the look actually lives. These two slots let that artwork drive this shader.
        //
        // OFF by default, and the keyword is what makes that free: with _PORTAL_TEXTURED
        // undefined the compiler removes both samplers, so a project that never adopts a pack
        // pays nothing at all for these existing.
        [Toggle(_PORTAL_TEXTURED)] _Textured ("Use purchased artwork", Float) = 0
        [NoScaleOffset] _EnergyTex ("Purchased Energy (RGB)", 2D) = "black" {}
        [NoScaleOffset] _MaskTex   ("Purchased Edge Mask (R)", 2D) = "white" {}
        _TexScale        ("Artwork Scale", Range(0.05, 8)) = 1
        _TexSpeed        ("Artwork Drift", Range(-4, 4)) = 0.35
        _TexInfluence    ("Artwork Influence", Range(0, 1)) = 0.75
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

            // Fragment-only and local: the vertex stage does not sample, and a global keyword
            // would spend one of the project's limited global slots on one material.
            #pragma shader_feature_local_fragment _PORTAL_TEXTURED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Every non-texture property lives here, in declaration order, or the SRP Batcher
            // quietly drops this material out of its batch.
            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _EnergyColor;
                float4 _OuterColor;
                float4 _Tint;
                float4 _Fit;
                float  _CoreIntensity;
                float  _EnergyIntensity;
                float  _RimWidth;
                float  _RimSoftness;
                float  _NoiseScale;
                float  _NoiseStrength;
                float  _NoiseSpeed;
                float  _SecondaryNoiseScale;
                float  _SecondaryNoiseSpeed;
                float  _DistortionStrength;
                float  _RotationSpeed;
                float  _PulseSpeed;
                float  _PulseStrength;
                float  _Opacity;
                float  _ViewOpacity;
                float  _Aspect;
                float  _Open;
                float  _TearAmount;
                float  _TearScale;
                float  _Textured;
                float  _TexScale;
                float  _TexSpeed;
                float  _TexInfluence;
            CBUFFER_END

            TEXTURE2D(_PortalTex);
            SAMPLER(sampler_PortalTex);

            // Declared unconditionally. The SRP Batcher compares the CBUFFER, not the sampler
            // set, and a texture declaration outside it costs nothing when the keyword strips
            // every read of it.
            TEXTURE2D(_EnergyTex);
            SAMPLER(sampler_EnergyTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

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

            // Two octaves. A single octave of value noise reads as blobs; four would look
            // better and cost twice as much on a phone for a difference nobody sees through a
            // burning rim.
            float fbm2(float2 p)
            {
                return vnoise(p) * 0.65 + vnoise(p * 2.17 + 11.3) * 0.35;
            }

            float2 rot2(float2 v, float a)
            {
                float s = sin(a), c = cos(a);
                return float2(c * v.x - s * v.y, s * v.x + c * v.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // -1..1 across the quad. The breach is a RECTANGLE with a torn edge, not an
                // oval and not a doorway: the fiction is that something came through the wall,
                // so the opening is a hole punched in plaster with ragged sides.
                float2 c = (IN.uv - 0.5) * 2.0;

                // The tear grows. Height opens first and width follows, which reads as a crack
                // splitting and then being pulled apart rather than a shape fading up. At
                // _Open = 0 the half size is zero, the box distance is positive everywhere, and
                // every mask below is zero - the wall has no hole in it at all.
                float open = saturate(_Open);
                float2 grow = float2(smoothstep(0.18, 1.0, open), smoothstep(0.0, 0.62, open));
                float2 fit = max(_Fit.xy * grow, 1e-4);

                // Signed distance to that rectangle: negative inside, positive outside.
                float2 q = abs(c) - fit;
                float box = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);

                // Isotropic in metres, so the tear's chunks are the same size on a narrow wall
                // as on a wide one rather than being stretched with the quad.
                float2 iso = float2(c.x * _Aspect, c.y);

                // The ragged edge. A big slow layer breaks the straight sides into chunks; the
                // fine layer crumbles them. Both scale with how far open the tear is, so a
                // barely-open crack is not already fringed like a finished hole.
                float tearA = fbm2(iso * _TearScale + 13.7);
                float tearB = fbm2(iso * _TearScale * 3.1 - 5.2);
                float ragged = (tearA - 0.5) * 0.75 + (tearB - 0.5) * 0.25;
                float r = 1.0 + box + ragged * _TearAmount * open;

                // Two layers that share nothing: different scale, different speed, opposite
                // rotation. Identical frequencies are what make procedural energy read as a
                // repeating pattern instead of as plasma.
                float2 qa = rot2(iso, t * _RotationSpeed) * _NoiseScale;
                float2 qb = rot2(iso, -t * _RotationSpeed * 0.63) * _SecondaryNoiseScale;

                float nA = fbm2(qa + float2(0.0, -t * _NoiseSpeed));
                float nB = fbm2(qb + float2(t * _SecondaryNoiseSpeed, t * _SecondaryNoiseSpeed * 0.37));

                // The oval's own edge is pushed in and out by the turbulence, so the silhouette
                // wavers instead of being a drawn outline. This is the difference between
                // "energy" and "a glowing ring".
                float wob = (nA - 0.5) * _NoiseStrength + (nB - 0.5) * _NoiseStrength * 0.6;
                float rd = r + wob;

                float w = max(_RimWidth, 1e-4);
                float pulse = 1.0 + sin(t * _PulseSpeed) * _PulseStrength;

                // Three masks off one distance field.
                //   view  - the clean centre, where the far room shows
                //   rim   - the burning band, peaked on the wavering edge
                //   outer - what spills beyond it, which is the volumetric part
                float view = 1.0 - smoothstep(1.0 - w, 1.0 - w * 0.15, rd);
                float band = 1.0 - saturate(abs(rd - 1.0) / w);
                float rim = pow(saturate(band), max(_RimSoftness, 1e-3));
                float outer = (1.0 - smoothstep(1.0, 1.0 + w * 1.6, rd)) * (1.0 - view);

                // ---- the destination ------------------------------------------------------
                // Screen space, not mesh UV. Dragged sideways only near the boundary, so the
                // middle of the opening stays readable - the brief is a spatial opening, and a
                // wobbling centre is a screen effect.
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);
                float bend = saturate(1.0 - view) * saturate(rd);
                float2 wobble = float2(nA - 0.5, nB - 0.5);
                screenUV += wobble * _DistortionStrength * bend;

                half3 destination = SAMPLE_TEXTURE2D(_PortalTex, sampler_PortalTex, screenUV).rgb;
                destination *= _Tint.rgb;

                // ---- the energy -----------------------------------------------------------
                // Outer to energy to core as the band gets hotter, so the narrow inside of the
                // rim approaches white and the spill outside loses colour gradually. HDR, and
                // intensity is separate from colour, so Bloom has something above 1 to find
                // without the colour being pushed to white to fake it.
                float hot = saturate(rim * (0.85 + 0.4 * nA));
                half3 energy = lerp(_OuterColor.rgb, _EnergyColor.rgb, saturate(hot * 1.5));
                energy = lerp(energy, _CoreColor.rgb, saturate(pow(hot, 2.6)));

#ifdef _PORTAL_TEXTURED
                // The purchased pack's own artwork, sampled in the SAME isotropic rotating
                // space the procedural layers use, so it turns with the energy instead of
                // sitting still underneath a moving rim.
                float2 tuv = rot2(iso, t * _RotationSpeed * 0.5) * _TexScale * 0.5 + 0.5;
                tuv += float2(0.0, -t * _TexSpeed * 0.1);

                half3 art  = SAMPLE_TEXTURE2D(_EnergyTex, sampler_EnergyTex, tuv).rgb;
                half  mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, tuv).r;

                // Influence is a LERP and it only ever reaches colour and heat. The silhouette
                // - the signed box field, the tear, the gate - is computed above and is not
                // touched here at any value, so adopting a pack changes what the energy looks
                // like and can never change where the hole is or whether a closed portal draws.
                // At influence 0 this is the procedural portal, pixel for pixel.
                energy = lerp(energy, energy * art * 2.0, _TexInfluence);
                hot    = saturate(lerp(hot, hot * mask, _TexInfluence));
#endif

                float emission = (_EnergyIntensity * rim + _CoreIntensity * pow(hot, 5.0)) * pulse;
                half3 glow = energy * emission + _OuterColor.rgb * outer * _EnergyIntensity * 0.35;

                // The gate is not decoration. A fully collapsed box still measures zero distance
                // at its own centre, so without this one pixel would sit at r == 1 and burn on a
                // wall that is supposed to be whole. Closed means NOTHING drawn.
                //
                // DECLARED HERE, above both of its uses, and that is load-bearing rather than
                // tidy: HLSL has no hoisting, so a `gate` used one statement before this line
                // is not a warning and not a wrong pixel - it is a compile error, and a shader
                // that fails to compile is drawn by Unity's magenta error shader. The portal
                // shipped exactly that way and read as "the purchased HDRP pack is magenta".
                float gate = smoothstep(0.0, 0.02, open);

                // The far room is multiplied by its OWN fade, not by the portal's. Before the
                // destination camera exists the centre is black behind a burning rim, which is
                // an opening that has not finished forming - honest, and visibly different from
                // an empty doorway.
                half3 col = (destination * view * _ViewOpacity + glow) * gate;

                // Outside the breach plus its spill, this is zero, and that is what makes the
                // quad's corners disappear.
                float alpha = saturate((view + rim + outer * 0.45) * _Opacity) * gate;

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    // No fallback. A built-in-pipeline fallback under URP draws solid magenta.
    Fallback Off
}
