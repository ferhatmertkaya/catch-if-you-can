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
    // axis exactly the way the reference photographs do.
    //
    // WHY WORLD SPACE. Every value below comes from _OriginWS and the three _Axis*WS vectors
    // rather than from the object matrix. The previous version worked in object space and drew
    // nothing, for a reason that was never established (CLAUDE.md mistake 33). Reading the
    // origin from a uniform makes the result independent of how the projection object is
    // parented, scaled or nested, which removes that entire class of failure instead of
    // explaining it.
    //
    // WHAT IT DOES NOT DO. This is not a shadow map. A surface the CAMERA cannot see never gets
    // dots, because there is no pixel for it - so dots never appear through a wall you are
    // looking at. A surface the camera CAN see but the projector cannot reach round a corner is
    // only handled by _FacingStrength below, which drops the pattern on faces that point away
    // from the lens. Real per-dot occlusion would cost a shadow map per frame.
    Properties
    {
        _DotColor        ("Dot Colour", Color) = (0.22, 1.0, 0.29, 1.0)
        _Density         ("Angular Density (cells around)", Range(16, 320)) = 144
        _DotSize         ("Dot Size (fraction of a cell)", Range(0.02, 0.45)) = 0.14
        _Range           ("Range (m)", Float) = 5.5
        _Intensity       ("Intensity", Range(0, 12)) = 3.0
        _FadeStart       ("Fade Start (fraction of range)", Range(0.05, 1.0)) = 0.55
        _NearBoost       ("Near Boost", Range(0, 4)) = 1.2
        _NearFade        ("Near Fade (m)", Range(0, 1)) = 0.12
        _FacingStrength  ("Face-Away Cull", Range(0, 1)) = 0.7
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
                float  _Range;
                half   _Intensity;
                float  _FadeStart;
                half   _NearBoost;
                float  _NearFade;
                half   _FacingStrength;
            CBUFFER_END

            // Written from C# every frame the projection runs. The lens, and the projector's
            // own three axes, both in world space.
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
                // azimuth, because elevation spans half the angle.
                float2 sphereUV = float2(azimuth * (1.0 / (2.0 * PI)) + 0.5,
                                         elevation * (1.0 / PI) + 0.5);
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
                if (dotMask <= 0.0) return half4(0, 0, 0, 0);

                // Bright close in, gently weaker with distance, gone by the far end. Held off
                // the last stretch rather than squared, so the far side of a room still reads.
                float travel      = dist / _Range;
                float distanceFade = 1.0 - smoothstep(_FadeStart, 1.0, travel);
                distanceFade *= 1.0 + _NearBoost * saturate(1.0 - dist);

                // And a short fade right at the lens, so standing on top of the device is not a
                // wall of light.
                float nearFade = saturate(dist / max(_NearFade, 1e-4));

                // A surface whose normal points away from the lens cannot be lit by it. The
                // normal is derived from how the reconstructed position changes across the
                // screen, which is cheap and is noisy on a depth edge - hence a soft ramp and a
                // strength dial rather than a hard cull.
                float3 dx      = ddx(worldPos);
                float3 dy      = ddy(worldPos);
                float3 normal  = cross(dy, dx);
                float  normalLen = length(normal);
                float  facing  = 1.0;
                if (normalLen > 1e-6)
                {
                    normal = normal / normalLen;
                    facing = lerp(1.0, smoothstep(-0.15, 0.35, dot(normal, -dir)), _FacingStrength);
                }

                float strength = dotMask * distanceFade * nearFade * facing * _Intensity;

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
