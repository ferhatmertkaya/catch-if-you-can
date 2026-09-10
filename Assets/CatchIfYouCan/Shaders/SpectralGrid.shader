Shader "CatchIfYouCan/SpectralGrid"
{
    // A spherical field of laser points thrown from one origin.
    //
    // WHAT THIS DRAWS. For every pixel of the volume it covers, the shader reconstructs the
    // world position of the SCENE SURFACE behind that pixel from the depth buffer, takes the
    // direction from the projector's lens to that point, and asks whether that direction falls
    // on a dot of an angular grid. So the dots belong to the floor, the wall, the ceiling and
    // the props they land on: they follow perspective across a corner, they stay put when the
    // player walks, and there is no dot anywhere in the scene graph - no GameObject per dot, no
    // particle, no light per dot, nothing instantiated while it runs.
    //
    // WHY ANGULAR AND NOT A COOKIE. A light cookie is a flat texture pushed through one cone,
    // so it covers a cone and stretches at the rim. This grid lives in the projector's own
    // SPHERICAL coordinates, which is what a multi-directional laser projector actually is: the
    // pattern surrounds the device, above it as much as below it, and converges towards its
    // axis exactly the way the reference photographs do. There is no hemisphere anywhere in
    // this file: azimuth runs the whole way round and elevation from pole to pole, so no
    // orientation of the device can remove the upper or the lower half of the field.
    //
    // WHY WORLD SPACE. Every value below comes from _OriginWS and the three _Axis*WS vectors
    // rather than from the object matrix. The previous version worked in object space and drew
    // nothing, for a reason that was never established (CLAUDE.md mistake 33). Reading the
    // origin from a uniform makes the result independent of how the projection object is
    // parented, scaled or nested, which removes that entire class of failure instead of
    // explaining it.
    //
    // WHAT BLOCKS A RAY. Two things, and neither is a shadow map. A surface the CAMERA cannot
    // see never gets dots, because there is no pixel for it. And ProjectorOcclusion below
    // marches the line from the lens to the lit point against the depth buffer, so a wall
    // standing between the two takes its dots away - see the note on that function for what a
    // screen-space test can and cannot see, and for why it is built to fail OPEN.
    Properties
    {
        _DotColor        ("Dot Colour", Color) = (0.22, 1.0, 0.29, 1.0)
        _Density         ("Angular Density (cells around)", Range(16, 320)) = 240
        _DotSize         ("Dot Size (fraction of a cell)", Range(0.02, 0.45)) = 0.16
        _GlowRadius      ("Glow Radius (x dot size)", Range(1, 3)) = 2.2
        _GlowStrength    ("Glow Strength", Range(0, 1)) = 0.35
        _Range           ("Range (m)", Float) = 5.5
        _Intensity       ("Intensity", Range(0, 12)) = 3.0
        _FadeStart       ("Fade Start (fraction of range)", Range(0.05, 1.0)) = 0.55
        _NearBoost       ("Near Boost", Range(0, 4)) = 1.2
        _NearFade        ("Near Fade (m)", Range(0, 1)) = 0.12
        _FacingStrength  ("Grazing Falloff", Range(0, 1)) = 0.7
        _OcclusionStrength ("Occlusion", Range(0, 1)) = 1.0
        _OcclusionBias   ("Occlusion Bias (m)", Range(0.005, 0.5)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "SpectralDots"

            // Purely additive. A pixel that is not on a dot outputs black and therefore changes
            // nothing at all, which is what keeps a dark room dark instead of washing it green.
            Blend One One
            ZWrite Off
            // Front faces are culled so the volume still draws when the camera is inside it -
            // walking into your own projection must not make it vanish.
            Cull Front
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _DotColor;
                float  _Density;
                float  _DotSize;
                float  _GlowRadius;
                half   _GlowStrength;
                float  _Range;
                half   _Intensity;
                float  _FadeStart;
                half   _NearBoost;
                float  _NearFade;
                half   _FacingStrength;
                half   _OcclusionStrength;
                float  _OcclusionBias;
            CBUFFER_END

            // Written from C# every frame the projection runs. The lens, and the projector's
            // own three axes, both in world space. Deliberately OUTSIDE UnityPerMaterial: a
            // property inside that buffer is served by the SRP Batcher from the material and a
            // MaterialPropertyBlock is then ignored, which would leave every dot measured from
            // wherever the lens happened to be when the material was authored.
            float4 _OriginWS;
            float4 _AxisXWS;
            float4 _AxisYWS;
            float4 _AxisZWS;

            // How many samples the occlusion march spends. Ten is enough to catch a wall across
            // a room; it is a compile-time constant so the loop can be unrolled.
            #define SPECTRAL_OCCLUSION_STEPS 10

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
            };

            // Is the straight line from the lens to this lit point clear, or is there something
            // standing in it?
            //
            // The line is marched in world space and each step is asked of the DEPTH BUFFER:
            // what does the camera actually see at this point's screen position? If that surface
            // is nearer to the camera than the step is, something is in the way, and this dot
            // does not exist.
            //
            // IT FAILS OPEN, BY CONSTRUCTION. Every way this test can be unsure - a step behind
            // the camera, a step off the side of the screen, a step against the sky, a
            // difference smaller than the bias or larger than the whole projection range - ends
            // that step having found nothing, and the dot stays lit. The opposite failure would
            // shadow everything and switch the effect off, and an invisible DOTS projector is a
            // bug this project has already shipped twice (CLAUDE.md mistakes 33 and 34). Setting
            // _OcclusionStrength to 0 removes the test entirely and costs nothing.
            //
            // WHAT IT CANNOT SEE: anything not in the depth buffer. An occluder behind the
            // camera, or off the side of the screen, does not block. That is the standing limit
            // of every screen-space test and it is the price of not rendering a cube shadow map
            // from the projector every frame on a phone.
            float ProjectorOcclusion(float3 worldPos, float3 originWS)
            {
                float3 toLens = originWS - worldPos;
                float  span   = length(toLens);
                if (span <= _OcclusionBias * 2.0)
                    return 1.0;

                float3 stepWS  = toLens / (float)SPECTRAL_OCCLUSION_STEPS;
                float  blocked = 0.0;

                UNITY_LOOP
                for (int i = 1; i <= SPECTRAL_OCCLUSION_STEPS; i++)
                {
                    // Half a step in from each end, so neither the lit surface nor the lens
                    // itself is sampled and neither can shadow this dot.
                    float3 probe = worldPos + stepWS * ((float)i - 0.5);

                    float4 probeCS = mul(UNITY_MATRIX_VP, float4(probe, 1.0));
                    if (probeCS.w <= 1e-5)
                        continue;

                    // Through ComputeScreenPos rather than by flipping y by hand: the vertex
                    // stage above already uses it to reach this same screen space, so the
                    // convention is the one that is known to work here rather than a guess
                    // about which way this platform's clip space runs.
                    float4 probeSP = ComputeScreenPos(probeCS);
                    float2 probeUV = probeSP.xy / probeSP.w;
                    if (probeUV.x < 0.0 || probeUV.x > 1.0 || probeUV.y < 0.0 || probeUV.y > 1.0)
                        continue;

                    float probeDepth = SampleSceneDepth(probeUV);
                    #if UNITY_REVERSED_Z
                        if (probeDepth <= 0.0) continue;
                    #else
                        if (probeDepth >= 1.0) continue;
                    #endif

                    float3 occluderWS = ComputeWorldSpacePosition(probeUV, probeDepth,
                                                                  UNITY_MATRIX_I_VP);

                    float probeFromCam    = distance(probe, _WorldSpaceCameraPos);
                    float occluderFromCam = distance(occluderWS, _WorldSpaceCameraPos);
                    float delta           = probeFromCam - occluderFromCam;

                    // Nearer than the step by more than the bias: something real is in front of
                    // it. Bounded by the projection range, because a difference larger than the
                    // whole field is a reading this test has no business trusting.
                    if (delta > _OcclusionBias && delta < _Range)
                    {
                        blocked = 1.0;
                        break;
                    }
                }

                return 1.0 - blocked * _OcclusionStrength;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos  = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);

                // The surface actually behind this pixel. Everything below is computed there,
                // which is what makes the dots sit on geometry instead of hanging in the air.
                float rawDepth = SampleSceneDepth(screenUV);

                // A pixel with nothing behind it is sky. Drawing dots on the sky would be a
                // projector with infinite range, which is exactly what this must not be.
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.0) return half4(0, 0, 0, 0);
                #else
                    if (rawDepth >= 1.0) return half4(0, 0, 0, 0);
                #endif

                float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);

                float3 toSurface = worldPos - _OriginWS.xyz;
                float  dist      = length(toSurface);

                // Out of range, or inside the lens itself.
                if (dist >= _Range || dist < 1e-4) return half4(0, 0, 0, 0);

                float3 dir = toSurface / dist;

                // Into the projector's own frame, so the pattern turns with the device and a
                // wall-mounted projector needs nobody to work out which way "outward" is.
                float3 local = float3(dot(dir, _AxisXWS.xyz),
                                      dot(dir, _AxisYWS.xyz),
                                      dot(dir, _AxisZWS.xyz));

                // Spherical coordinates about the device's own axis. Azimuth runs the whole way
                // round and elevation from pole to pole, so the field is a SPHERE: as much of it
                // reaches the ceiling as the floor.
                float azimuth   = atan2(local.z, local.x);
                float elevation = asin(clamp(local.y, -1.0, 1.0));

                // 0..1 in both, then scaled into cells. Half as many cells in elevation as in
                // azimuth, because elevation spans half the angle - which makes a cell square in
                // angle: 360/_Density degrees on both axes. At _Density 240 that is 1.5 degrees,
                // so dots stand about 8 cm apart on a surface 3 m away and roughly 2400 of them
                // fall inside a 90 x 60 degree view. Density costs nothing per pixel: the shader
                // evaluates a formula, not a list of dots.
                float2 sphereUV = float2(azimuth * (1.0 / (2.0 * PI)) + 0.5,
                                         elevation * (1.0 / PI) + 0.5);
                float2 grid     = sphereUV * float2(_Density, _Density * 0.5);

                float2 cell     = frac(grid) - 0.5;
                float  cellDist = length(cell);

                // fwidth keeps a point the same visual size whatever distance and angle it is
                // seen at. It is CLAMPED because it blows up across the azimuth seam and at the
                // poles, where the direction changes by half a turn between neighbouring pixels;
                // unclamped, that one meridian would swallow every dot on it.
                float aa = clamp(fwidth(cellDist), 1e-4, 0.25);

                // A crisp core with a soft halo just outside it, so a point reads as laser light
                // rather than as a flat disc. The halo is CLAMPED inside its own cell: a cell is
                // one unit across, so anything past 0.49 from its centre reaches the neighbour
                // and the dots run together into the green wash this must not be. Clamped rather
                // than left to the sliders, because _DotSize and _GlowRadius are independent and
                // their top ends multiply to 1.35 - a comment promising they stay apart would be
                // true at the defaults and false at the ends of two sliders (mistake 33).
                // Named dotMask rather than dot: dot() is an HLSL intrinsic and shadowing it is
                // the kind of thing that compiles on one compiler and not the next.
                float haloOuter = min(_DotSize * _GlowRadius, 0.49);
                float core      = 1.0 - smoothstep(_DotSize - aa, _DotSize + aa, cellDist);
                float halo      = 1.0 - smoothstep(_DotSize, haloOuter + aa, cellDist);
                float dotMask   = saturate(core + halo * _GlowStrength);
                if (dotMask <= 0.002) return half4(0, 0, 0, 0);

                // Only now, on the small fraction of pixels that actually carry a dot, is the
                // march worth paying for.
                float clearPath = ProjectorOcclusion(worldPos, _OriginWS.xyz);
                if (clearPath <= 0.002) return half4(0, 0, 0, 0);

                // Bright close in, gently weaker with distance, gone by the far end. Held off
                // the last stretch rather than squared, so the far side of a room still reads.
                float travel       = dist / _Range;
                float distanceFade = 1.0 - smoothstep(_FadeStart, 1.0, travel);
                distanceFade *= 1.0 + _NearBoost * saturate(1.0 - dist);

                // And a short fade right at the lens, so standing on top of the device is not a
                // wall of light.
                float nearFade = saturate(dist / max(_NearFade, 1e-4));

                // How square-on this surface is to the lens: a dot smears out and dims as the
                // surface turns edge-on, which is what makes the pattern wrap convincingly
                // across a corner. The normal comes from how the reconstructed position changes
                // across the screen, and the ORIENTATION of that cross product depends on which
                // way this platform's screen-space Y runs - a sign this shader cannot know.
                // abs() removes the question, because what is being measured here is how edge-on
                // the surface is and that is sign-free. Taken signed, an inverted convention
                // dims every surface FACING the lens to (1 - _FacingStrength) and leaves the
                // ones facing away at full: "the dots are too weak", on some platforms and not
                // others, which is mistakes 24 and 34 wearing a third face.
                float3 ddxWorld  = ddx(worldPos);
                float3 ddyWorld  = ddy(worldPos);
                float3 normal    = cross(ddyWorld, ddxWorld);
                float  normalLen = length(normal);
                float  facing    = 1.0;
                if (normalLen > 1e-6)
                {
                    normal = normal / normalLen;
                    facing = lerp(1.0, smoothstep(0.05, 0.40, abs(dot(normal, dir))),
                                  _FacingStrength);
                }

                float strength = dotMask * distanceFade * nearFade * facing * clearPath * _Intensity;

                half4 col = _DotColor;
                col.rgb *= strength;
                col.a    = saturate(strength);
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
