#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Makes the candle flame material actually emit light-coloured pixels.
    ///
    /// <para>
    /// The flame was authored on <c>Universal Render Pipeline/Particles/Lit</c> with
    /// <c>_EmissionColor</c> left at black and no <c>_EMISSION</c> keyword, which means the only
    /// colour it could ever produce was whatever the scene lighting reflected off it. The room's
    /// ambient is black and its lights are dim by design, so a flame that is lit rather than
    /// emissive is a flame you cannot see. Additive blending was the only reason anything showed
    /// at all.
    /// </para>
    ///
    /// <para>
    /// A flame is a light source, so it belongs on the unlit shader: it should not be shaded by
    /// the room, and on a phone an additive transparent quad that skips the whole PBR path is
    /// simply cheaper. The switch is done by name through <see cref="Shader.Find"/> rather than by
    /// writing a shader GUID into the .mat, for the same reason the skybox is built in code — a
    /// built-in shader's file ID lives inside Unity's own resource bundle and guessing it produces
    /// a magenta material with no explanation. If the unlit shader cannot be resolved the material
    /// keeps the shader it has and emission is switched on there instead, which is worse but never
    /// invisible.
    /// </para>
    ///
    /// <para>
    /// The existing material is upgraded in place. Its GUID is unchanged, so the three
    /// ParticleSystemRenderers in the scene keep pointing at it and no duplicate asset appears.
    /// </para>
    ///
    /// <para>
    /// The tint is close to white on purpose. <c>CIYC_CandleFlame.png</c> already runs from a pale
    /// yellow core through orange to a dark red tip; multiplying that by an orange would push the
    /// whole flame red. The brightness above 1 is what produces the hot core — additive blending
    /// clips the centre toward white while the thinner edges stay orange, which is how a real
    /// flame reads. Note this does not depend on bloom: the project's Bloom override is authored
    /// at intensity 0, so nothing here is relying on a glow that is switched off.
    /// </para>
    /// </summary>
    public static class CandleFlameSetup
    {
        private const string FlameMaterialPath =
            "Assets/CatchIfYouCan/Art/Environment/Props/HauntedCandleHolder/Materials/" +
            "MAT_CandleFlame.mat";

        private const string UnlitParticleShader = "Universal Render Pipeline/Particles/Unlit";

        /// <summary>
        /// Warm, but barely tinted: the texture carries the colour, this carries the heat. 1.8
        /// puts the core over 1 so it saturates toward white while the edges stay in range.
        /// </summary>
        private static readonly Color FlameTint = new Color(1f, 0.93f, 0.82f, 1f);
        private const float FlameIntensity = 1.8f;

        [MenuItem("Catch If You Can/Assets bauen/Kerzenflammen-Material [SCHREIBT ASSET]", false, 1008)]
        public static void BuildMenuItem()
        {
            var log = new StringBuilder();
            log.AppendLine("[CIYC] Candle flame material");
            Build(log);
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Idempotent. Safe to call from the automatic setup on every editor load.
        /// </summary>
        public static void Build(StringBuilder log)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
            if (material == null)
            {
                log.AppendLine("  MISSING: " + FlameMaterialPath);
                return;
            }

            var unlit = Shader.Find(UnlitParticleShader);
            if (unlit != null && material.shader != unlit)
            {
                material.shader = unlit;
                log.AppendLine("  shader -> " + UnlitParticleShader);
            }
            else if (unlit == null)
            {
                log.AppendLine("  WARNING: '" + UnlitParticleShader + "' not found; keeping " +
                               material.shader.name + " and enabling emission on it instead");
            }

            Color hot = FlameTint * FlameIntensity;
            hot.a = 1f;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", hot);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", hot);

            // Belt and braces for the fallback path: on the lit shader the base colour alone is
            // still shaded, so emission is what rescues it there. Harmless on the unlit shader.
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", hot);
                // The flame moves and flickers; it must never be baked into a lightmap.
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            // Additive. A flame adds light to what is behind it, it does not occlude it, and
            // additive is also why it never needs to sort against the other two flames.
            material.SetFloat("_Surface", 1f);              // transparent
            material.SetFloat("_Blend", 2f);                // additive
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            log.AppendLine("  " + material.shader.name);
            log.AppendLine("  base/emission " + hot.r.ToString("0.00") + ", " +
                           hot.g.ToString("0.00") + ", " + hot.b.ToString("0.00") +
                           " (tint " + FlameTint.r.ToString("0.00") + ", " +
                           FlameTint.g.ToString("0.00") + ", " + FlameTint.b.ToString("0.00") +
                           " x " + FlameIntensity + ")");
            log.AppendLine("  additive, no depth write, no shadow pass");
            log.AppendLine("  per-flame brightness is written at runtime by CandleFlameFlicker " +
                           "through a MaterialPropertyBlock, so this asset is never instanced");
        }

        /// <summary>
        /// True when the material is already emissive. Used by the automatic setup so a project
        /// that has run this once does not pay for it on every editor load.
        /// </summary>
        public static bool IsBuilt()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
            if (material == null)
                return true;   // nothing to do; do not nag about an asset that is not there

            if (!material.HasProperty("_BaseColor"))
                return false;

            Color c = material.GetColor("_BaseColor");
            return c.maxColorComponent > 1.05f;
        }
    }
}
#endif
