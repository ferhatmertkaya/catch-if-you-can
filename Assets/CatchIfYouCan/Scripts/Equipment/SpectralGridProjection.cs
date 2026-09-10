using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The projector's field of points, as PROJECTED LIGHT: a small fixed cluster of spot
    /// lights sharing one dot mask as their cookie.
    ///
    /// <para>
    /// The dots belong to the surfaces they fall on. They follow perspective across a corner,
    /// they stay put when the player walks, and the whole field costs five lights and one
    /// texture - no GameObject per dot, no particle system, no decal projector, nothing
    /// instantiated or destroyed while it runs, and nothing to replicate over a network later
    /// but the projector's transform and whether it is on.
    /// </para>
    ///
    /// <para>
    /// <b>Why a cluster and not one spot.</b> One 70-degree spot is a torch: a bright patch on
    /// the wall opposite and nothing on the side walls, the floor or the ceiling. The device is
    /// bolted to a wall, so what it needs to light is the HEMISPHERE in front of it. A cone
    /// cannot do that; a small number of overlapping cones can, and a point light cannot be
    /// masked back to a hemisphere without lighting the masonry behind it.
    /// </para>
    ///
    /// <para>
    /// <b>Why five and not four.</b> Four cones on their own have to be 120 degrees wide to
    /// close the gaps between them, and a spot cookie is projected by perspective - a dot at
    /// the rim of a 120-degree cone is stretched by 1/cos(60) = 2.0 across and 1/cos^2(60) = 4.0
    /// along, which is the elongated pill this class was rewritten to stop drawing. Adding one
    /// axial light lets the four tilted ones stay at 100 degrees, where the worst rim stretch is
    /// 1.56. Five lights, no shadows, one shared cookie.
    /// </para>
    ///
    /// <para>
    /// <b>What is NOT covered.</b> Cones cannot tile a hemisphere exactly. At the four axis
    /// azimuths the cluster reaches 10 degrees PAST the wall plane; at the diagonals between
    /// them it stops about 10 degrees SHORT of it. So the corner where a side wall meets the
    /// mounting wall gets fewer dots, and a little light passes into the wall behind. Neither is
    /// avoidable with a finite number of cones, and the alternative - wider cones - brings the
    /// pills back. Nothing is AIMED backwards: every cone axis points into the room.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Spectral Grid Projection")]
    public sealed class SpectralGridProjection : MonoBehaviour
    {
        // ------------------------------------------------------------------ cluster geometry

        /// <summary>One axial light plus four tilted ones. Also what the diagnostic asserts.</summary>
        public const int LightCount = 5;

        /// <summary>
        /// How far the four outer cones lean off the outward axis, in degrees. With
        /// <see cref="ConeAngle"/> at 100 this reaches about 80 degrees from the axis at the
        /// worst azimuth and about 100 at the best.
        /// </summary>
        private const float OuterTiltDegrees = 50f;

        /// <summary>
        /// Full cone angle of every light in the cluster, in degrees.
        ///
        /// <para>
        /// One value for all five, so a dot is the same size whichever light drew it. Raising it
        /// widens coverage and stretches the dots at the rim; lowering it opens gaps between the
        /// cones.
        /// </para>
        /// </summary>
        private const float ConeAngle = 100f;

        // ------------------------------------------------------------------ authored look

        [Header("Look")]
        [SerializeField] private Color dotColor = new Color(0.2f, 1f, 0.35f, 1f);

        [Tooltip("Brightness of each light in the cluster. Five overlapping cones add up, so " +
                 "this is well below what a single projector spot wanted.")]
        [SerializeField] private float lightIntensity = 4.5f;

        [Tooltip("How far the dots reach, in metres. A room, not a building.")]
        [SerializeField, Range(2f, 8f)] private float lightRange = 5f;

        [Tooltip("How far the lens sits off the device body, along the device's own working " +
                 "axis, in metres. An origin inside the wall is occluded by that wall.")]
        [SerializeField] private float lensForwardOffset = 0.06f;

        /// <summary>
        /// The dot mask, by Resources path so it ships with the build.
        ///
        /// <para>
        /// 512 and deliberately sparse. A cookie's dot COUNT is free at runtime - it is a
        /// texture, sampled once per lit pixel whatever is drawn on it - so the count is a
        /// readability decision, not a performance one. What costs per frame is the five lights.
        /// </para>
        /// </summary>
        private const string CookieResourcePath = "Equipment/DOTS/T_DOTS_RoomCookie_512";

        private const string ClusterChildName = "SpectralGrid_Cluster";

        private Light[] _lights;
        private Transform _cluster;
        private Texture _cookie;
        private bool _running;

        /// <summary>
        /// Attaches a projection to a device head. The cluster is a child, so it inherits the
        /// device's orientation and a wall-mounted projector throws into the room without
        /// anything having to work out which way that is.
        /// </summary>
        public static SpectralGridProjection Attach(Transform head)
        {
            if (head == null)
                return null;

            var go = new GameObject("SpectralGridProjection");
            go.transform.SetParent(head, false);
            return go.AddComponent<SpectralGridProjection>();
        }

        /// <summary>
        /// Sets how far the field reaches. Cheap; safe to call whenever it changes.
        ///
        /// <para>
        /// The angle argument is the device's EVIDENCE cone, which
        /// <see cref="SpectralGridProjector.FieldStrengthAt"/> uses to decide whether a ghost is
        /// standing in the field. It deliberately does not size the lights any more: the lit
        /// field is a hemisphere and that cone is 70 degrees forward, so a ghost lit by a side
        /// cone is not currently counted. Widening the evidence cone is an evidence-contract
        /// decision and is not made here.
        /// </para>
        /// </summary>
        public void Configure(float evidenceRange, float evidenceConeDegrees)
        {
            // Both arguments describe the device's EVIDENCE field, and neither sizes the lights.
            //
            // They used to. One spot could be the evidence cone, because it WAS a cone: 6 m at
            // 70 degrees, the same shape FieldStrengthAt measures, so what the player saw and
            // what the ghost scan counted could not disagree. A hemisphere cannot hold that
            // deal - it is wider than any cone - so the two are now separate numbers with
            // separate jobs, and the lit field is the one authored here.
            //
            // The consequence is real and is NOT fixed here: a ghost standing in a side cone is
            // lit by dots and is not counted, because FieldStrengthAt still tests 70 degrees
            // forward. Widening it changes what the projector can prove, which is an
            // evidence-contract decision rather than a rendering one.
            EnsureCluster();
            ApplyShape();
        }

        /// <summary>
        /// Turns the field on and off. Everything expensive is behind this: a projector that is
        /// switched off, stowed or in a bag renders nothing at all.
        /// </summary>
        public void SetRunning(bool running)
        {
            _running = running;
            EnsureCluster();

            if (_lights != null)
            {
                for (int i = 0; i < _lights.Length; i++)
                {
                    if (_lights[i] != null)
                        _lights[i].enabled = running;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (running)
                ReportProjection();
#endif
        }

        /// <summary>
        /// Builds the cluster once, and adopts the one a CLONE already carries.
        ///
        /// <para>
        /// Every piece of equipment in this project reaches the world as an
        /// <c>Instantiate</c> of a live template, and <c>Instantiate</c> copies GameObjects and
        /// components while dropping the private field that pointed at them. So on the clone the
        /// cluster and its five lights are already there and <c>_lights</c> is null - and a
        /// <c>new GameObject</c> here would hang a SECOND cluster on the same device. Ten
        /// coincident lights are not visibly ten; they are five at double brightness, and the
        /// count keeps climbing every time the item is picked up and put down. That is mistakes
        /// 27 and 30 wearing a third face, so the CHILD is looked for by name and its lights by
        /// component - both survive the copy; the field does not.
        /// </para>
        /// </summary>
        private void EnsureCluster()
        {
            if (_lights != null && _lights.Length == LightCount)
                return;

            _cluster = transform.Find(ClusterChildName);
            if (_cluster == null)
            {
                var host = new GameObject(ClusterChildName);
                host.transform.SetParent(transform, false);
                _cluster = host.transform;
            }

            // Off the surface it is mounted on, along the axis it throws along. A light whose
            // origin sits inside the wall is occluded by that wall and lights nothing in the
            // room - indistinguishable from a light that never switched on.
            _cluster.localPosition = new Vector3(0f, lensForwardOffset, 0f);
            _cluster.localRotation = Quaternion.identity;

            var existing = _cluster.GetComponentsInChildren<Light>(true);
            _lights = new Light[LightCount];

            for (int i = 0; i < LightCount; i++)
            {
                Light light = i < existing.Length ? existing[i] : null;
                if (light == null)
                {
                    var go = new GameObject("DotsCone_" + i);
                    go.transform.SetParent(_cluster, false);
                    light = go.AddComponent<Light>();
                }

                light.transform.localPosition = Vector3.zero;
                light.transform.localRotation = Quaternion.LookRotation(
                    ConeDirection(i), ConeUpReference(i));

                light.type = LightType.Spot;
                light.color = dotColor;
                light.shadows = LightShadows.None;
                light.cookie = LoadCookie();
                light.enabled = _running;

                _lights[i] = light;
            }

            // A clone that somehow arrived with MORE than the cluster wants: switch the surplus
            // off rather than leave it lighting the room from a transform nothing updates.
            for (int i = LightCount; i < existing.Length; i++)
            {
                if (existing[i] != null)
                    existing[i].enabled = false;
            }

            ApplyShape();
        }

        /// <summary>
        /// Which way cone <paramref name="index"/> points, in the projection's OWN space.
        ///
        /// <para>
        /// Local +Y is the device's working axis by the shared carried-transform convention -
        /// the same axis <c>FieldStrengthAt</c> measures along and the same one the placement's
        /// quarter turn puts along the wall normal. Deriving the cluster from it rather than
        /// from a world axis is what makes the same code correct on every wall of every room.
        /// </para>
        /// </summary>
        private static Vector3 ConeDirection(int index)
        {
            if (index == 0)
                return Vector3.up;

            float azimuth = (index - 1) * 90f * Mathf.Deg2Rad;
            float tilt = OuterTiltDegrees * Mathf.Deg2Rad;

            return new Vector3(
                Mathf.Sin(tilt) * Mathf.Cos(azimuth),
                Mathf.Cos(tilt),
                Mathf.Sin(tilt) * Mathf.Sin(azimuth));
        }

        /// <summary>
        /// An up-vector for <see cref="Quaternion.LookRotation"/> that is never parallel to the
        /// direction it is paired with. The axial cone looks straight along +Y, so world up
        /// would be degenerate there and produce an undefined roll.
        /// </summary>
        private static Vector3 ConeUpReference(int index) =>
            index == 0 ? Vector3.forward : Vector3.up;

        private void ApplyShape()
        {
            if (_lights == null)
                return;

            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] == null)
                    continue;

                _lights[i].range = Mathf.Max(0.5f, lightRange);
                _lights[i].spotAngle = ConeAngle;
                _lights[i].intensity = lightIntensity;
            }
        }

        /// <summary>
        /// The generated cookie, loaded once and shared by all five lights.
        ///
        /// <para>
        /// A null here is an ERROR rather than a shrug: a cookie-less spot is a plain green
        /// blob, which reads as a broken dot pattern rather than as a missing texture, and those
        /// need different fixes.
        /// </para>
        /// </summary>
        private Texture LoadCookie()
        {
            if (_cookie != null)
                return _cookie;

            _cookie = Resources.Load<Texture2D>(CookieResourcePath);
            if (_cookie == null)
            {
                Core.CIYCLog.Error("[CIYC][DOTS][Projection] Resources.Load(\"" +
                                   CookieResourcePath + "\") is NULL, so the cluster has no dot " +
                                   "mask and will project plain green cones. Run " +
                                   "Catch If You Can > 4. SPIELINHALT > Equipment > " +
                                   "DOTS-Cookies erzeugen.");
            }

            return _cookie;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// One block, at switch-on, naming every value that can make a lit projector invisible.
        /// Never per frame: this runs from <see cref="SetRunning"/>, which runs on a keypress.
        /// </summary>
        private void ReportProjection()
        {
            int live = 0;
            for (int i = 0; _lights != null && i < _lights.Length; i++)
            {
                if (_lights[i] != null && _lights[i].enabled)
                    live++;
            }

            var all = GetComponentsInChildren<Light>(true);
            var cookie = _lights != null && _lights.Length > 0 && _lights[0] != null
                ? _lights[0].cookie : null;

            string block =
                "[CIYC][DOTS][Projection]" +
                " mode=RoomHemisphere" +
                " lights=" + live +
                " cookie=" + (cookie != null ? cookie.name : "null") +
                " cookieSize=" + (cookie != null ? cookie.width + "x" + cookie.height : "-") +
                " range=" + lightRange.ToString("F1") +
                " coneAngle=" + ConeAngle.ToString("F0") +
                " outerTilt=" + OuterTiltDegrees.ToString("F0") +
                " intensity=" + lightIntensity.ToString("F2") +
                " shadows=False" +
                " directionBasis=" + transform.up.ToString("F2") +
                " estimatedCoverage=hemisphere";

            if (_lights == null || live != LightCount || cookie == null)
                Core.CIYCLog.Error(block + "  <- FAILED PROJECTION STATE");
            else
                Core.CIYCLog.Info(block);

            // The failure this cannot see any other way: a clone that accumulated lights. The
            // count is fixed by construction, so anything above it is a build that ran twice.
            if (all.Length > LightCount)
            {
                Core.CIYCLog.Error("[CIYC][DOTS][ERROR] unexpectedLightCount=" + all.Length +
                                   " expected=" + LightCount +
                                   " - a second cluster was built on a clone that already had one.");
            }
        }
#endif
    }
}
