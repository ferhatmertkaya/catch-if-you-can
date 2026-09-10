Shader "CatchIfYouCan/SpectralGrid"
{
    // A spherical field of laser points thrown from one origin.
    //
    // THIS IS THE MINIMAL REBUILD, AND IT IS BUILT AS A LADDER. The version before it had grown
    // an occlusion march, a glow halo and a grazing term derived from screen-space derivatives -
    // three layers that no camera had ever confirmed, sitting on top of a base nobody had
    // confirmed either. The whole shader has never once been observed drawing a pixel: not at
    // 51c76f2, not at db070a6, not at c3ad2d8, and CLAUDE.md's mistake 33 says the same of its
    // predecessor. So everything unproven is gone, and what is left is the shortest path from a
    // fragment to a green dot, cut into five rungs that can be climbed one at a time. Layers come
    // back only after a camera has said the rung below them works.
    //
    //   0  the finished dots                        the game.
    //   1  magenta over the whole volume            does this pass rasterise at all?  CONFIRMED
    //   2  the screen UV as a gradient              is the UV right, before depth uses it?
    //   3  the RAW depth value, unclassified        is a depth buffer reaching this pass?
    //   4  the reconstructed world position         is the inverse view-projection right?
    //   5  green in range, RED out of it            did the lens arrive, and how big is the ball?
    //   6  a coarse angular chequerboard            is the mapping a sphere, and does it wrap?
    //
    // RUNG 2 IS NEW AND SITS ABOVE THE DEPTH RUNG ON PURPOSE. Sampling the depth texture USES
    // the screen UV, so a broken UV and an unbound depth texture produce the same picture: every
    // fragment reading one constant. Asking the UV first separates them, and it costs nothing -
    // the UV is already computed by then. Every suspect that can be eliminated by a rung is one
    // that does not have to be eliminated by an afternoon.
    //
    // Every rung is a `return` placed ABOVE the work the next rung needs, so a rung can never be
    // taken down by a failure further along. Stage 1 is the FIRST statement in the function for
    // that reason: nothing above it can go wrong. There is deliberately no rung for the finished
    // dots: it would have no branch of its own and would fall through to what stage 0 already
    // draws, so a reader who selected it would be told about a stage that is not the one they
    // asked for. Every rung offered here is a rung that exists, and a guard counts them.
    //
    // AND NO RUNG SITS BELOW AN INVISIBLE EARLY-OUT. The first version of this ladder returned
    // transparent black for sky ABOVE rung 2 and for out-of-range ABOVE rung 3, so ONE dead
    // depth texture would have blacked out three rungs at once and read as three separate
    // failures. Above the last rung there is now no early-out at all: sky is BLUE, out of range
    // is RED, and every condition that would have returned nothing returns a colour that names
    // itself. A bisect whose rungs can share a cause is not a bisect (mistake 44).
    //
    // WHAT IT DRAWS. For every pixel of the volume it covers, the shader reconstructs the world
    // position of the SCENE SURFACE behind that pixel from the depth buffer, takes the direction
    // from the projector's lens to that point, and asks whether that direction falls on a dot of
    // an angular grid in the device's own SPHERICAL coordinates. So the dots belong to the
    // floor, the wall, the ceiling and the props they land on; the pattern surrounds the device,
    // above it as much as below; and there is no dot anywhere in the scene graph - no GameObject
    // per dot, no particle, no light, nothing instantiated while it runs.
    //
    // CULL OFF, DELIBERATELY. Front-face culling was correct for a camera inside the box and
    // was still one more thing that had to be right for anything at all to appear. Off, the
    // question cannot be asked: both faces rasterise, so the volume covers its footprint
    // whatever side the camera is on and whatever the winding of the mesh turns out to be. The
    // price is that a box is shaded twice over its silhouette, which on an additive pass is a
    // uniform doubling of brightness and a doubling of fill - a tuning problem, and tuning
    // problems are the ones worth having. It goes back to Front when a camera has confirmed the
    // pass runs, not before.
    //
    // ADDITIVE, INCLUDING FOR THE MAGENTA RUNG. Alpha blending would be one fewer thing to be
    // right about on rung 1, and it is not available per instance: blend state is fixed in the
    // pass and comes from the MATERIAL, while this device drives everything through a
    // MaterialPropertyBlock - so a per-instance alpha blend would mean mutating a shared
    // Resources material at runtime, which every other clone of the projector would then wear.
    // Nothing is lost by it. `One One` with an output of (1, 0, 1) ADDS a whole unit of red and
    // blue to whatever is behind it, so on the dark surfaces this device is used against it is
    // brighter and more obvious than the 0.75-alpha version, not less.
    Properties
    {
        _DotColor   ("Dot Colour", Color) = (0.22, 1.0, 0.29, 1.0)
        _Density    ("Angular Density (cells around)", Range(16, 320)) = 144
        _DotSize    ("Dot Size (fraction of a cell)", Range(0.02, 0.45)) = 0.16
        _Range      ("Range (m)", Float) = 5.5
        _Intensity  ("Intensity", Range(0, 12)) = 3.0
        _FadeStart  ("Fade Start (fraction of range)", Range(0.05, 1.0)) = 0.55
        [IntRange] _DebugMode ("Debug Stage (0 = off)", Range(0, 6)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "SpectralDots"
            Tags { "LightMode"="SRPDefaultUnlit" }

            // Purely additive. A pixel that is not on a dot outputs black and therefore changes
            // nothing at all, which is what keeps a dark room dark instead of washing it green.
            Blend One One
            ZWrite Off
            Cull Off
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
                float  _Range;
                half   _Intensity;
                float  _FadeStart;
                float  _DebugMode;
            CBUFFER_END

            // Written from C# every frame the projection runs. The lens, and the projector's own
            // three axes, both in world space. Deliberately OUTSIDE UnityPerMaterial: a property
            // inside that buffer is served by the SRP Batcher from the material and a
            // MaterialPropertyBlock is then ignored, which would leave every dot measured from
            // wherever the lens happened to be when the material was authored.
            float4 _OriginWS;
            float4 _AxisXWS;
            float4 _AxisYWS;
            float4 _AxisZWS;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos  = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                int stage = (int)round(_DebugMode);

                // RUNG 1 - does this pass rasterise at all? The FIRST statement in the shader:
                // nothing above it can fail and take the answer with it.
                // CONFIRMED IN UNITY: this draws magenta over the whole view.
                if (stage == 1) return half4(1, 0, 1, 1);

                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);

                // RUNG 2 - is the screen UV right? Red rises left to right, green bottom to top.
                //
                //   a smooth red/green gradient across the whole view -> the UV is a real screen
                //                                                        coordinate.
                //   one FLAT colour                                   -> every fragment is
                //                                                        reading the same point,
                //                                                        so the depth rung below
                //                                                        would read one constant
                //                                                        and look exactly like an
                //                                                        unbound texture.
                //
                // This rung answers a question the depth rung cannot answer for itself, which is
                // why it sits above it. Note the render scale is 1 in CIYC_URP.asset, so a scaled
                // buffer is not among the suspects here - that was checked in the file rather
                // than guessed at.
                if (stage == 2) return half4(screenUV.x, screenUV.y, 0, 1);

                float rawDepth = SampleSceneDepth(screenUV);

                // RUNG 3 - is a depth buffer reaching this pass? THE RAW VALUE, WITH NO SKY
                // CLASSIFICATION AT ALL. That is the whole point of this rung: "sky" is an
                // interpretation, and an interpretation cannot be trusted to report on the number
                // it interprets. The three outcomes are flat and nameable.
                //
                //   the WHOLE view flat BLUE   -> the value is exactly 0 everywhere. Under
                //                                 reversed Z that is the far plane, and it is
                //                                 also what an unbound sampler returns from
                //                                 Unity's default black texture. The depth
                //                                 texture is not reaching this pass.
                //   the WHOLE view flat GREEN  -> exactly 1 everywhere: bound to a white texture.
                //   STRIPES over the room,     -> real, varying depth. Blue then appears ONLY
                //   blue only in the window       through the window, which is the correct
                //                                 picture: sky is genuinely 0 under reversed Z.
                //
                // So "all blue including the wall in front of you" and "blue only where the sky
                // is" are the two answers, and they are not confusable.
                if (stage == 3)
                {
                    if (rawDepth <= 0.0)
                        return half4(0, 0, 1, 1);

                    if (rawDepth >= 1.0)
                        return half4(0, 1, 0, 1);

                    return half4(frac(rawDepth * 64.0), rawDepth, 1.0 - rawDepth, 1);
                }

                // Now, and only now, the interpretation. A pixel with nothing behind it is sky.
                // This is a QUESTION rather than a return: an unbound or empty depth texture
                // reads as sky for every pixel on the screen, and that has to be visible as
                // itself rather than as an absence.
                #if UNITY_REVERSED_Z
                    bool isSky = rawDepth <= 0.0;
                #else
                    bool isSky = rawDepth >= 1.0;
                #endif

                float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);

                // RUNG 4 - is the reconstruction right? The world position wrapped to a colour
                // every metre.
                //
                //   bands GLUED to the walls as you turn on the spot -> correct.
                //   bands that SWIM with the view                    -> the inverse
                //                                                       view-projection is not
                //                                                       what this thinks.
                //
                // Turning on the spot is the test, not walking: a wrong matrix moves the pattern
                // with the camera, a right one leaves it painted on the room.
                if (stage == 4)
                {
                    if (isSky)
                        return half4(0, 0, 1, 1);

                    return half4(frac(abs(worldPos)), 1);
                }

                float3 toSurface = worldPos - _OriginWS.xyz;
                float  dist      = length(toSurface);

                // RUNG 5 - did the lens arrive, and is the range the right size? Green inside,
                // RED outside rather than nothing, so the boundary itself is drawn.
                //
                //   a green ball of room centred on the device -> origin and range both arrived.
                //   all red                                    -> _OriginWS is somewhere else, or
                //                                                 _Range is 0.
                //   all green                                  -> _Range is enormous.
                if (stage == 5)
                {
                    if (isSky)
                        return half4(0, 0, 1, 1);

                    return dist < _Range ? half4(0, 1, 0, 1) : half4(1, 0, 0, 1);
                }

                // Guarded rather than early-returned: a surface exactly at the lens would divide
                // by zero, and the axis is a harmless stand-in for a direction nobody can see.
                float3 dir = dist > 1e-4 ? toSurface / dist : float3(0, 1, 0);

                // Into the projector's own frame, so the pattern turns with the device and a
                // mounted projector needs nobody to work out which way "outward" is.
                float3 local = float3(dot(dir, _AxisXWS.xyz),
                                      dot(dir, _AxisYWS.xyz),
                                      dot(dir, _AxisZWS.xyz));

                // Spherical coordinates about the device's own axis. Azimuth runs the whole way
                // round and elevation from pole to pole, so the field is a SPHERE: as much of it
                // reaches the ceiling as the floor. There is no cone test anywhere in this file,
                // and the device's axis only ROTATES the pattern - it cannot remove a hemisphere.
                float azimuth   = atan2(local.z, local.x);
                float elevation = asin(clamp(local.y, -1.0, 1.0));

                float2 sphereUV = float2(azimuth * (1.0 / (2.0 * PI)) + 0.5,
                                         elevation * (1.0 / PI) + 0.5);

                // RUNG 6 - is the mapping a SPHERE, and does it wrap the room? A coarse
                // chequerboard in cells of twenty degrees rather than the effect's own, because
                // the question here is coverage and shape - a 2.5 degree chequer aliases into a
                // grey wash at the far end of a room and proves nothing.
                //
                //   squares on the floor, the ceiling and all four walls -> a sphere.
                //   squares in one direction only                        -> a cone, which is what
                //                                                           two earlier attempts
                //                                                           were.
                //
                // Fixed at 18 x 9 so this rung says the same thing whatever the density slider is
                // set to.
                if (stage == 6)
                {
                    if (isSky || dist >= _Range)
                        return half4(0, 0, 0, 0);

                    float2 coarse = floor(sphereUV * float2(18.0, 9.0));
                    float  parity = fmod(coarse.x + coarse.y, 2.0);
                    return parity < 0.5 ? half4(0, 0.85, 0.85, 1) : half4(0.95, 0.35, 0, 1);
                }

                // ------------------------------------------------------------- STAGE 0, the game
                //
                // Below here the early-outs are real: this is the effect, and a pixel that carries
                // no dot must add nothing at all. That is what keeps a dark room dark.
                if (isSky)
                    return half4(0, 0, 0, 0);

                if (dist >= _Range || dist < 1e-4)
                    return half4(0, 0, 0, 0);

                float2 grid     = sphereUV * float2(_Density, _Density * 0.5);
                float2 cell     = frac(grid) - 0.5;
                float  cellDist = length(cell);

                // fwidth keeps a point the same visual size whatever distance and angle it is
                // seen at. It is CLAMPED because it blows up across the azimuth seam and at the
                // poles, where the direction changes by half a turn between neighbouring pixels;
                // unclamped, that one meridian would swallow every dot on it.
                // Named dotMask rather than dot: dot() is an HLSL intrinsic and shadowing it is
                // the kind of thing that compiles on one compiler and not the next.
                float aa      = clamp(fwidth(cellDist), 1e-4, 0.25);
                float dotMask = 1.0 - smoothstep(_DotSize - aa, _DotSize + aa, cellDist);
                if (dotMask <= 0.002)
                    return half4(0, 0, 0, 0);

                // Bright close in, gently weaker with distance, gone by the far end.
                float travel       = dist / _Range;
                float distanceFade = 1.0 - smoothstep(_FadeStart, 1.0, travel);

                float strength = dotMask * distanceFade * _Intensity;

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
