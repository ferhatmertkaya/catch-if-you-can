Shader "CatchIfYouCan/SpectralGrid"
{
    // A projected field of green points.
    //
    // The previous version of this shader was a scrolling grid of LINES computed from world XZ
    // and drawn on whatever mesh it happened to be applied to. That is a floor decal: it had no
    // projector, no direction, no distance falloff and no way to be stopped by a wall, and it
    // drew continuous lines rather than the individual luminous points the device is supposed to
    // throw. It could not have been made into this by tuning, so it is replaced rather than
    // layered over.
    //
    // How this one works: one cone mesh is drawn for the projection volume, and for every pixel
    // of it the shader reconstructs the world position of the SCENE SURFACE behind that pixel
    // from the depth buffer. The dots are computed at that reconstructed position, so they land
    // on the real floor, the real wall and the real prop. A wall nearer than the cone's far end
    // means the reconstructed point is the wall, so the dots stop there - occlusion falls out of
    // the method rather than being a second system.
    //
    // Points diverge with distance because the pattern is computed in angular coordinates about
    // the projector's axis, which is what a real dot projector does.
    //
    // REQUIRES the URP asset's Depth Texture. Without it every pixel reconstructs to the far
    // plane and the effect is invisible rather than wrong.
    Properties
    {
        _DotColor      ("Dot Colour", Color) = (0.2, 1, 0.35, 1)
        _Density       ("Angular Density", Range(4, 128)) = 34
        _DotSize       ("Dot Size", Range(0.02, 0.6)) = 0.22
        _Range         ("Range (m)", Float) = 6
        _HalfAngle     ("Half Angle (rad)", Float) = 0.61
        _Intensity     ("Intensity", Range(0, 8)) = 2.2
        _EdgeSoftness  ("Edge Softness", Range(0.01, 0.9)) = 0.35
        _NearFade      ("Near Fade (m)", Range(0, 2)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "SpectralGridVolume"

            // Additive: the field adds light to what is already there and never darkens it.
            Blend SrcAlpha One
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
                half4 _DotColor;
                half  _Density;
                half  _DotSize;
                half  _Range;
                half  _HalfAngle;
                half  _Intensity;
                half  _EdgeSoftness;
                half  _NearFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float4 screenPos   : TEXCOORD0;
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
                // projection with infinite range, which is exactly what this must not be.
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.0) return half4(0, 0, 0, 0);
                #else
                    if (rawDepth >= 1.0) return half4(0, 0, 0, 0);
                #endif

                float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);

                // Into the projector's own frame. Its +Y is the axis it throws along, which is
                // the same carried-transform convention every item in this project uses.
                float3 lp = mul(GetWorldToObjectMatrix(), float4(worldPos, 1.0)).xyz;

                float axial = lp.y;
                if (axial <= 0.0 || axial >= _Range) return half4(0, 0, 0, 0);

                float radial = length(lp.xz);
                float coneRadius = axial * tan(_HalfAngle);
                if (radial >= coneRadius) return half4(0, 0, 0, 0);

                // Angular coordinates, so the points spread as they travel - a real projector
                // throws a diverging pattern, not a parallel one.
                float2 angular = lp.xz / max(axial, 1e-4);
                float2 cell = frac(angular * _Density) - 0.5;
                float  cellDist = length(cell);

                // fwidth keeps a point the same visual size whatever the distance and angle it
                // is being viewed at, instead of aliasing into noise across the room.
                // Named dotMask rather than dot: dot() is an HLSL intrinsic, and shadowing it
                // is the kind of thing that compiles on one compiler and not the next.
                float aa      = max(fwidth(cellDist), 1e-4);
                float dotMask = 1.0 - smoothstep(_DotSize - aa, _DotSize + aa, cellDist);
                if (dotMask <= 0.0) return half4(0, 0, 0, 0);

                // Bright near the projector, gone by the far end. Squared so the last third of
                // the range is genuinely dim rather than merely dimmer.
                float distanceFade = saturate(1.0 - axial / _Range);
                distanceFade *= distanceFade;

                // And faded at the edge of the cone, so the volume has no visible rim.
                float edgeFade = saturate((1.0 - radial / max(coneRadius, 1e-4)) / _EdgeSoftness);

                // Plus a short fade right at the lens, so standing on top of it is not a wall
                // of light.
                float nearFade = saturate(axial / max(_NearFade, 1e-4));

                float strength = dotMask * distanceFade * edgeFade * nearFade * _Intensity;

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
