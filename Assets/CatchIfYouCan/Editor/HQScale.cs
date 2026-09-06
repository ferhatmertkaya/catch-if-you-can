#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// The one place that knows how big a piece of purchased architecture has to be in this game,
    /// and the one place that decides whether a given object already is.
    ///
    /// <para>
    /// The number was measured, not chosen: the hand-built lobby stood at 3.92 m clear height and
    /// wanted 2.95, so architecture is placed at 2.95 / 3.92. The quotient is written rather than
    /// the rounded 0.752551, so the ratio and its two inputs cannot drift apart later.
    /// </para>
    /// <para>
    /// It is shared because the alternative is four copies of one constant in four tools - the
    /// room scaler, the scale check, the migration and the placement wrapper - which would agree
    /// on the day they were written and diverge the first time the room was re-measured. Silently,
    /// because each tool would still look internally consistent.
    /// </para>
    /// </summary>
    public static class HQScale
    {
        /// <summary>The clear height the factor was derived FROM, in metres.</summary>
        public const float ReferenceClearHeight = 3.92f;

        /// <summary>The clear height wanted, in metres.</summary>
        public const float TargetClearHeight = 2.95f;

        /// <summary>2.95 / 3.92. Derived, never typed.</summary>
        public static float Factor => TargetClearHeight / ReferenceClearHeight;

        /// <summary>How far an effective scale may sit from a value and still count as it.</summary>
        public const float Tolerance = 0.01f;

        /// <summary>The wrapper the placement tool and the migration put a piece under.</summary>
        public const string WrapperPrefix = "HQ_";

        /// <summary>What a piece is, as far as the scale system is concerned.</summary>
        public enum Verdict
        {
            /// <summary>Already at game scale, by its own transform or by an ancestor's.</summary>
            Correct,

            /// <summary>Still at the vendor's own size. This is what conversion is for.</summary>
            OriginalSize,

            /// <summary>Furniture or a loose object. Never converted automatically.</summary>
            Prop,

            /// <summary>Neither test could decide. Reported, never guessed at.</summary>
            Ambiguous,

            /// <summary>Applying the factor here would apply it twice.</summary>
            DoubleScaleRisk
        }

        /// <summary>
        /// The scale an object actually has in the world, ancestors included.
        ///
        /// <para>
        /// <c>localScale</c> alone is the trap this exists to avoid: a vendor piece sitting at
        /// localScale 1 inside a wrapper scaled to 0.7526 is ALREADY at game scale, and reading
        /// its own field says otherwise. Every decision below is made on <c>lossyScale</c>.
        /// </para>
        /// </summary>
        public static Vector3 EffectiveScale(Transform t) =>
            t == null ? Vector3.one : t.lossyScale;

        /// <summary>Whether a scale is the same in all three axes, within tolerance.</summary>
        public static bool IsUniform(Vector3 s) =>
            Mathf.Abs(s.x - s.y) <= Tolerance && Mathf.Abs(s.y - s.z) <= Tolerance;

        /// <summary>Whether a uniform scale is within tolerance of a value.</summary>
        public static bool Is(Vector3 s, float value) =>
            IsUniform(s) && Mathf.Abs(s.x - value) <= Tolerance;

        /// <summary>
        /// Whether any ancestor already carries the game-scale correction, which is what makes a
        /// second application a double scaling rather than a fix.
        /// </summary>
        public static bool UnderCorrectedAncestor(Transform t)
        {
            for (Transform p = t == null ? null : t.parent; p != null; p = p.parent)
            {
                if (p.name == HQRoomMeasurement.ScaleRootName)
                    return true;
                if (Is(p.localScale, Factor))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Folders inside the pack whose contents are architecture. Path, not filename.
        ///
        /// <para>
        /// A filename classifier was tried and failed measurably: this pack numbers its prefabs
        /// and calls its glass "Steklo", so matching English words for "wall" and "door" caught
        /// 3 of 105 pieces. The pack's own folder layout is the thing it is actually consistent
        /// about, so that is what is read.
        /// </para>
        /// </summary>
        private static readonly string[] ArchitectureFolders =
        {
            "/moduls/", "/walls prefabs/", "/walls/", "/architecture/", "/wall panel",
            "/plinth", "/customization/"
        };

        /// <summary>Folders whose contents are furniture or loose objects.</summary>
        private static readonly string[] PropFolders =
        {
            "/props/", "/furniture/", "/library/", "/decor/"
        };

        /// <summary>
        /// Whether a vendor asset path is architecture, furniture, or neither answer.
        ///
        /// <para>
        /// Returns null when the path decides nothing. A null is passed up as AMBIGUOUS rather
        /// than resolved by a second guess: furniture may already be at real-world size, and
        /// shrinking a chair that was right is a silent wrong.
        /// </para>
        /// </summary>
        public static bool? IsArchitectureByPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            string p = assetPath.ToLowerInvariant();
            for (int i = 0; i < PropFolders.Length; i++)
                if (p.Contains(PropFolders[i]))
                    return false;
            for (int i = 0; i < ArchitectureFolders.Length; i++)
                if (p.Contains(ArchitectureFolders[i]))
                    return true;
            return null;
        }

        /// <summary>
        /// Whether a measured world box has the shape of a wall, a floor or a ceiling: flat in
        /// one axis and large in the other two.
        ///
        /// <para>
        /// Corroboration, not the decision. Where shape and folder disagree the piece is
        /// AMBIGUOUS - a bookcase is also thin, tall and wide.
        /// </para>
        /// </summary>
        public static bool HasStructuralShape(Vector3 size)
        {
            if (size == Vector3.zero)
                return false;

            float[] a = { size.x, size.y, size.z };
            System.Array.Sort(a);                       // a[0] thinnest, a[2] longest
            return a[0] < a[1] * 0.35f && a[1] >= 1.5f && a[2] >= 1.5f;
        }

        /// <summary>
        /// The vendor asset a scene object came from, or an empty string.
        ///
        /// <para>
        /// One API, not two. Asking first for the outermost instance root and then for its path
        /// is the same answer with twice the surface to be wrong about, and the offline typecheck
        /// harness carries neither - a stub written to agree with me is not verification
        /// (CLAUDE.md mistake 9), so the fewer of them the better.
        /// </para>
        /// </summary>
        public static string SourcePath(GameObject go)
        {
            if (go == null)
                return string.Empty;
            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) ?? string.Empty;
        }

        /// <summary>What one object is, and why.</summary>
        public struct Finding
        {
            public GameObject Object;
            public string Source;
            public Vector3 Effective;
            public Vector3 WorldSize;
            public Verdict Verdict;
            public string Why;
        }

        /// <summary>
        /// Judges one object. Reads only; the caller decides what to do about it.
        /// </summary>
        public static Finding Judge(GameObject go)
        {
            var f = new Finding
            {
                Object = go,
                Source = SourcePath(go),
                Effective = EffectiveScale(go.transform),
                WorldSize = MeasureWorld(go)
            };

            bool? architecture = IsArchitectureByPath(f.Source);
            bool structural = HasStructuralShape(f.WorldSize);
            bool underCorrected = UnderCorrectedAncestor(go.transform);

            if (!IsUniform(f.Effective))
            {
                f.Verdict = Verdict.Ambiguous;
                f.Why = "ungleichmaessig skaliert (" + Fmt(f.Effective) + ") - ein einziger " +
                        "Faktor kann das nicht richtig machen";
                return f;
            }

            if (Is(f.Effective, Factor))
            {
                f.Verdict = Verdict.Correct;
                f.Why = underCorrected
                    ? "steht unter einer bereits korrigierten Wurzel"
                    : "traegt den Spielmassstab selbst";
                return f;
            }

            if (underCorrected && !Is(f.Effective, Factor))
            {
                f.Verdict = Verdict.DoubleScaleRisk;
                f.Why = "steht unter einer korrigierten Wurzel, hat aber effektiv " +
                        f.Effective.x.ToString("F4") + " - der Faktor waere hier zweimal drin";
                return f;
            }

            if (architecture == false)
            {
                f.Verdict = Verdict.Prop;
                f.Why = "Moebel oder loses Objekt - kann schon Realmass haben und wird nicht " +
                        "automatisch verkleinert";
                return f;
            }

            if (architecture == true && Is(f.Effective, 1f))
            {
                f.Verdict = structural ? Verdict.OriginalSize : Verdict.Ambiguous;
                f.Why = structural
                    ? "Vendor-Originalgroesse, Form passt zu Architektur"
                    : "Ordner sagt Architektur, die Form nicht - erst ansehen";
                return f;
            }

            f.Verdict = Verdict.Ambiguous;
            f.Why = "weder Ordner noch Form entscheiden es (effektiv " +
                    f.Effective.x.ToString("F4") + ")";
            return f;
        }

        /// <summary>The world box of everything drawn under an object.</summary>
        public static Vector3 MeasureWorld(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            bool any = false;
            Bounds b = new Bounds();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].bounds.size == Vector3.zero)
                    continue;
                if (!any) { b = renderers[i].bounds; any = true; }
                else b.Encapsulate(renderers[i].bounds);
            }
            return any ? b.size : Vector3.zero;
        }

        /// <summary>
        /// Every HQ object in the open scene: the topmost match down each branch, inactive
        /// included, with the scaling root's own container excluded.
        /// </summary>
        public static List<GameObject> CollectHQObjects()
        {
            var found = new List<GameObject>();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                Walk(roots[i].transform, found);
            return found;
        }

        /// <summary>
        /// Objects this system must never touch, whatever they are made of.
        ///
        /// <para>
        /// The portal is a GAMEPLAY dimension, not an architectural one: its opening is fixed in
        /// metres, and the wall aperture is cut to it. Scaling it with the architecture would
        /// change how wide a doorway the player can walk through, which is a different decision
        /// with a different owner.
        /// </para>
        /// </summary>
        private static readonly string[] NeverTouch = { "Lobby_Portal" };

        private static void Walk(Transform t, List<GameObject> into)
        {
            if (t == null)
                return;

            for (int i = 0; i < NeverTouch.Length; i++)
                if (t.name == NeverTouch[i])
                    return;

            bool isHq = t.name.StartsWith(WrapperPrefix) && t.name != HQRoomMeasurement.ScaleRootName;
            bool isVendorInstance = !string.IsNullOrEmpty(SourcePath(t.gameObject)) &&
                                    SourcePath(t.gameObject).Contains("HQ Modular House");

            if (isHq || isVendorInstance)
            {
                into.Add(t.gameObject);
                return;
            }

            foreach (Transform child in t)
                Walk(child, into);
        }

        public static string Fmt(Vector3 v) =>
            v.x.ToString("F3") + ", " + v.y.ToString("F3") + ", " + v.z.ToString("F3");

        public static string Metres(Vector3 v) =>
            v.x.ToString("F2") + " x " + v.y.ToString("F2") + " x " + v.z.ToString("F2") + " m";
    }
}
#endif
