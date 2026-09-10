using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// A small, brightly lit magenta room built behind the portal when there is nothing else to
    /// show. It exists to answer one question and only that question.
    ///
    /// <para>
    /// <b>A dark portal centre has two completely different causes and they look identical.</b>
    /// Either the portal is not rendering at all - no camera, no buffer, a material that never
    /// got its texture - or it is rendering perfectly and the world on the far side is simply
    /// black. Every fix for one is wasted effort on the other, and from a screenshot there is
    /// no way to tell which.
    /// </para>
    ///
    /// <para>
    /// So: magenta, unmissable, and lit by its own light so it cannot be dark for any reason.
    /// If the portal shows this, the whole render path works and the darkness is the mission
    /// world. If the portal is still black with THIS behind it, the render path is broken and
    /// the lighting was never the problem.
    /// </para>
    ///
    /// <para>
    /// It is a diagnostic, not a destination. Nothing can walk into it: the portal refuses entry
    /// unless <c>MissionWorldLoader.WorldReady</c>, and this is not that. It is built far below
    /// the lobby so it cannot be seen except through the opening.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalProbeRoom : MonoBehaviour
    {
        /// <summary>Well below any playable geometry, so it is only ever seen through a portal.</summary>
        private static readonly Vector3 Origin = new Vector3(0f, -500f, 0f);

        private static PortalProbeRoom _instance;

        /// <summary>Where a portal camera should stand to look into the room.</summary>
        public Transform ViewPoint { get; private set; }

        /// <summary>Builds it once, or returns the one that already exists.</summary>
        public static PortalProbeRoom Ensure()
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject("Portal_ProbeRoom");
            go.transform.position = Origin;
            _instance = go.AddComponent<PortalProbeRoom>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            Shader shader = CiycShaders.FindLit();

            // Unlit would prove less: a lit room that arrives lit says the portal camera is
            // rendering lighting too, which is the thing being questioned.
            Material wall = shader != null
                ? new Material(shader) { name = "Portal_Probe_Magenta" }
                : null;
            if (wall != null)
            {
                wall.color = new Color(1f, 0f, 0.85f);
                if (wall.HasProperty("_BaseColor"))
                    wall.SetColor("_BaseColor", new Color(1f, 0f, 0.85f));
            }

            Material stripe = shader != null
                ? new Material(shader) { name = "Portal_Probe_Stripe" }
                : null;
            if (stripe != null)
            {
                stripe.color = new Color(0.05f, 0.05f, 0.05f);
                if (stripe.HasProperty("_BaseColor"))
                    stripe.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
            }

            // A box turned inside out: six slabs, so the camera inside sees walls rather than
            // the back faces of a cube it would be standing in.
            Face(wall, new Vector3(0f, -1.6f, 3f), new Vector3(7f, 0.2f, 8f));   // floor
            Face(wall, new Vector3(0f, 2.4f, 3f), new Vector3(7f, 0.2f, 8f));    // ceiling
            Face(wall, new Vector3(-3.4f, 0.4f, 3f), new Vector3(0.2f, 4f, 8f)); // left
            Face(wall, new Vector3(3.4f, 0.4f, 3f), new Vector3(0.2f, 4f, 8f));  // right
            Face(wall, new Vector3(0f, 0.4f, 7f), new Vector3(7f, 4f, 0.2f));    // far wall

            // Dark stripes on the far wall. Flat colour alone cannot show whether the view has
            // parallax; something with edges in it can.
            for (int i = -2; i <= 2; i++)
                Face(stripe, new Vector3(i * 1.2f, 0.4f, 6.85f), new Vector3(0.35f, 3.6f, 0.05f));

            var lightGo = new GameObject("Portal_Probe_Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.8f, 3f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.white;
            light.intensity = 6f;
            light.range = 18f;
            light.shadows = LightShadows.None;

            var view = new GameObject("Portal_Probe_ViewPoint");
            view.transform.SetParent(transform, false);
            view.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            view.transform.localRotation = Quaternion.identity;
            ViewPoint = view.transform;

            Debug.Log("[CIYC][Portal] Probe room built at " + Origin.ToString("F0") +
                      ". If the portal shows MAGENTA, the render path works and the mission " +
                      "world is what is dark. If it is still black, the render path is the bug " +
                      "and lighting is not.");
        }

        private void Face(Material material, Vector3 localPosition, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Probe_Face";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            if (material != null)
                go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
