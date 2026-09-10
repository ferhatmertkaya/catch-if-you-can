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
    //   1  magenta over the whole volume            does this pass rasterise at all?
    //   2  the reconstructed world position         is the depth texture readable here?
    //   3  solid green inside the range             did the lens arrive?
    //   4  the raw angular grid, no dot test        is the spherical mapping sane?
    //
    // Every rung is a `return` placed ABOVE the work the next rung needs, so a rung can never be
    // taken down by a failure further along. Stage 1 is the FIRST statement in the function for
    // that reason: nothing above it can go wrong. There is deliberately no rung for the finished
    // dots: it would have no branch of its own and would fall through to what stage 0 already
    // draws, so a reader who selected it would be told about a stage that is not the one they
    // asked for. Every rung offered here is a rung that exists, and a guard counts them.
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
        [IntRange] _DebugMode ("Debug Stage (0 = off)", Range(0, 4)) = 0
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
                // A staged bisect, because no compiler and no camera runs where this is written.
                // Each stage answers exactly one question, so one play session names the failing
                // layer instead of one session per guess. Stage 0 is the game.
                int stage = (int)round(_DebugMode);

                // STAGE 1 - is this pass running AT ALL? Magenta over the whole volume, before
                // anything is sampled, reconstructed or tested. This is the FIRST statement in
                // the shader for a reason: nothing above it can fail and take the answer with
                // it. If this is not on screen, the dots are not the question.
                if (stage == 1) return half4(1, 0, 1, 1);

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

                // STAGE 2 - is the depth reconstruction right? The world position wrapped to a
                // colour every metre. Smooth bands that stay put on the walls as the camera
                // turns are correct; a flat colour, black, or bands that swim with the view mean
                // the depth texture or the inverse view-projection is not what this thinks.
                if (stage == 2) return half4(frac(abs(worldPos)), 1);

                float3 toSurface = worldPos - _OriginWS.xyz;
                float  dist      = length(toSurface);

                // Out of range, or inside the lens itself.
                if (dist >= _Range || dist < 1e-4) return half4(0, 0, 0, 0);

                // STAGE 3 - is the lens where the shader thinks it is? Solid green on every
                // surface within range. A green ball of room centred on the projector means the
                // origin and the range arrived; green somewhere else, or none at all, means
                // _OriginWS did not.
                if (stage == 3) return half4(0, 1, 0, 1);

                float3 dir = toSurface / dist;

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

                // 0..1 in both, then scaled into cells. Half as many cells in elevation as in
                // azimuth, because elevation spans half the angle - which makes a cell square in
                // angle: 360/_Density degrees on both axes.
                float2 sphereUV = float2(azimuth * (1.0 / (2.0 * PI)) + 0.5,
                                         elevation * (1.0 / PI) + 0.5);
                float2 grid     = sphereUV * float2(_Density, _Density * 0.5);

                // STAGE 4 - is the angular grid itself sane? The cell coordinates as colour,
                // with no dot test at all. This has to look like a smooth chequer wrapped around
                // the projector; uniform or wild means the axes or the mapping are wrong and no
                // dot size will ever help.
                if (stage == 4) return half4(frac(grid.x), frac(grid.y), 0, 1);

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
                if (dotMask <= 0.002) return half4(0, 0, 0, 0);

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
