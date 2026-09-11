Shader "CatchIfYouCan/SpectralGrid"
{
    // The DOTS projector's laser points, drawn as REAL GEOMETRY.
    //
    // WHY THIS IS NOT A DEPTH-RECONSTRUCTION SHADER ANY MORE. Three rewrites of this file
    // reconstructed the world position of the surface behind each pixel from the depth buffer.
    // The technique is sound and the file compiled - the ladder proved it: rung 1 (magenta) drew,
    // rung 2 (screen UV) drew a correct gradient. Rung 3 read the RAW value out of
    // SampleSceneDepth with no interpretation on top of it and came back flat blue, which in that
    // rung means exactly 0.0 at every pixel on the screen. So the depth texture is not reaching
    // this pass in this project, on this platform, in this Unity version - and the effect was
    // never going to draw a pixel until that changed. It is chased no further: a technique that
    // needs a resource nobody can hand it is the wrong technique here, however correct it is.
    //
    // WHAT IT IS NOW. The C# side casts rays out of the lens in every direction and lays one small
    // camera-independent quad flat on each surface it hits. This shader draws a round dot on that
    // quad. That is all it does. It reads no depth texture, no screen position, no inverse
    // view-projection and no scene-wide state - every input is a vertex attribute or a material
    // property, so there is nothing left that can be unbound.
    //
    // THE DOT IS PROCEDURAL, NOT A TEXTURE, and that is deliberate. Mistake 35: the previous dot
    // artwork was drawn as perfect circles and arrived on screen as pills, because three importer
    // defaults - mipmaps on a texture that is only ever magnified, anisotropic filtering, and
    // Repeat on one axis against Clamp on the other - are all wrong for a mask and all on by
    // default. A distance from the quad's own centre cannot be turned into a pill by an import
    // setting, has no atlas to be resized into, and costs one length() per pixel.
    //
    // ADDITIVE, so a pixel that is not on a dot outputs black and changes nothing at all. That is
    // what keeps a dark room dark instead of washing it green (mistake 40): brightness and flood
    // are the same number on a light and two different things here.
    Properties
    {
        _DotColor  ("Dot Colour", Color) = (0.208, 1.0, 0.271, 1.0)
        _Intensity ("Intensity", Range(0, 12)) = 2.4
        _Softness  ("Edge Softness", Range(0.01, 0.6)) = 0.22
        [IntRange] _DebugMode ("Debug Stage (0 = off)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "SpectralDots"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DotColor;
                half  _Intensity;
                float _Softness;
                float _DebugMode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                // Per-dot brightness, baked into the mesh by the C# side: how far this dot's ray
                // travelled, already turned into a fade. A vertex colour rather than a second
                // draw call per distance band.
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv         = input.uv;
                output.color      = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // The one diagnostic that survives: is this pass running? Everything else the
                // ladder used to ask is now answered by the mesh existing at all, which the C#
                // side reports as a vertex count. Ships at 0 (mistake 23).
                if ((int)round(_DebugMode) == 1)
                    return half4(1, 0, 1, 1);

                // Distance from the quad's centre, in the quad's own UV. Round by construction:
                // there is no sampler, no filter and no atlas between this and the screen.
                float d = length(input.uv - 0.5) * 2.0;

                // Crisp core, mild soft edge - a projected laser dot, not a glow blob.
                float mask = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
                if (mask <= 0.002)
                    return half4(0, 0, 0, 0);

                half strength = (half)mask * input.color.a * _Intensity;

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
