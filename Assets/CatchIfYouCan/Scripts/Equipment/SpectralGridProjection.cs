using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// The projector's field of points, as PROJECTED LIGHT: one spot light wearing a dot mask.
    ///
    /// <para>
    /// The dots belong to the surfaces they fall on. They follow perspective across a corner,
    /// they stay put when the player walks, and they cost one light and one texture - no
    /// GameObject per dot, nothing instantiated or destroyed while it runs, and nothing to
    /// replicate over a network later but the projector's transform and whether it is on.
    /// </para>
    ///
    /// <para>
    /// <b>A SPOT rather than a point light</b>, because the device is screwed to a wall. A point
    /// light is a sphere, so half of it is inside the masonry and the useful half still has to
    /// be masked back to a cone. A spot IS that cone, it cannot spill behind the wall it is
    /// mounted on, and its cookie is an ordinary 2D texture with no equirectangular mapping to
    /// get wrong. The spherical cookie is generated and committed beside the spot one for the
    /// point-light path, but what ships is the one whose behaviour is unambiguous.
    /// </para>
    ///
    /// <para>
    /// <b>What this replaced.</b> The projection used to be a cone mesh drawn with
    /// <c>CatchIfYouCan/SpectralGrid</c>, which reconstructs each pixel's world position from
    /// the depth buffer and computes the dots there. That is a real projection technique and it
    /// is not a volume of dots hanging in the air - occlusion falls out of it for free, which
    /// the cookie below does not get. It produced nothing on screen and I could not establish
    /// why: the depth texture it needs IS enabled (CIYC_URP.asset, m_RequireDepthTexture: 1),
    /// and its +Y throw axis matches the rest of the device. It is replaced rather than kept
    /// alongside, because two things drawing the same dots is how this project ends up with two
    /// flashlights. The shader and MAT_SpectralGrid are still in the project and still used by
    /// the lighting lab; nothing here loads them any more.
    /// </para>
    ///
    /// <para>
    /// <b>The trade this makes.</b> Real-time shadows are off - a cookied spot with shadows is
    /// not an effect on a phone, it is a slideshow - so the dots are not occluded by geometry
    /// between the lens and the surface. Inside one room, which is the range this device works
    /// at, that is not visible. It would be through a doorway.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Spectral Grid Projection")]
    public sealed class SpectralGridProjection : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private Color dotColor = new Color(0.2f, 1f, 0.35f, 1f);

        [Tooltip("Brightness of the projected dot field.")]
        [SerializeField] private float lightIntensity = 14f;

        [Tooltip("How far the lens sits in front of the device body, along the device's own " +
                 "working axis, in metres. A light origin inside the wall is occluded by that " +
                 "wall and lights nothing.")]
        [SerializeField] private float lensForwardOffset = 0.06f;

        /// <summary>
        /// The dot mask, by Resources path so it ships with the build.
        ///
        /// <para>
        /// Authored at 1024 because that is the size it is used at: CIYC_URP.asset sets
        /// m_AdditionalLightsCookieResolution to 2048, so a 2048 cookie is the entire atlas,
        /// and a mask the importer has to downscale loses its dots to the filter.
        /// </para>
        /// </summary>
        private const string CookieResourcePath = "Equipment/DOTS/T_DOTS_SpotCookie_1024";

        /// <summary>The child that carries the light, found by name when a clone already has it.</summary>
        private const string ProjectorChildName = "SpectralGrid_Projector";

        private Light _light;

        private float _range = 6f;
        private float _fullAngle = 70f;

        /// <summary>
        /// Attaches a projection to a device head. The light is a child, so it inherits the
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

        /// <summary>Sets the shape of the field. Cheap; safe to call whenever it changes.</summary>
        public void Configure(float range, float fullAngleDegrees)
        {
            _range = Mathf.Max(0.1f, range);
            _fullAngle = Mathf.Clamp(fullAngleDegrees, 5f, 170f);

            EnsureProjectorLight();
            ApplyShape();
        }

        /// <summary>
        /// Turns the field on and off. Everything expensive is behind this: a projector that is
        /// switched off, stowed or in a bag renders nothing at all.
        /// </summary>
        public void SetRunning(bool running)
        {
            EnsureProjectorLight();
            if (_light != null)
                _light.enabled = running;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (running)
                ReportProjection();
#endif
        }

        /// <summary>
        /// Builds the light once, and finds the one a CLONE already carries.
        ///
        /// <para>
        /// Every piece of equipment in this project reaches the world as an
        /// <c>Instantiate</c> of a live template, and <c>Instantiate</c> copies GameObjects and
        /// components while dropping the private field that pointed at them. So on the clone
        /// the child and its Light are already there and <c>_light</c> is null - and a
        /// <c>new GameObject</c> here would hang a SECOND light on the same device. Two
        /// coincident lights are not visibly two; they are one that is twice as bright, which
        /// is mistakes 27 and 30 wearing a third face. The component is the identity that
        /// survives the copy, so that is what is searched for.
        /// </para>
        /// </summary>
        private void EnsureProjectorLight()
        {
            if (_light != null)
                return;

            _light = GetComponentInChildren<Light>(true);

            if (_light == null)
            {
                var host = new GameObject(ProjectorChildName);
                host.transform.SetParent(transform, false);
                _light = host.AddComponent<Light>();
            }

            var t = _light.transform;

            // Off the surface it is mounted on, along the axis it throws along. A light whose
            // origin sits inside the wall is occluded by that wall and lights nothing in the
            // room - indistinguishable from a light that never switched on.
            t.localPosition = new Vector3(0f, lensForwardOffset, 0f);

            // A Unity spot shines along its own +Z. This project's carried-transform convention
            // is that an item's local +Y is its length and the direction it works along - the
            // cone, the ghost-in-the-field test and the placement's quarter turn all use +Y -
            // so the light is turned to look along +Y. Left at identity it throws sideways out
            // of the device, which lights the wall it is bolted to and nothing else.
            t.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);

            _light.type = LightType.Spot;
            _light.color = dotColor;
            _light.intensity = lightIntensity;
            _light.shadows = LightShadows.None;
            _light.cookie = LoadCookie();
            _light.enabled = false;

            ApplyShape();
        }

        private void ApplyShape()
        {
            if (_light == null)
                return;

            _light.range = Mathf.Max(0.5f, _range);
            _light.spotAngle = Mathf.Clamp(_fullAngle, 5f, 170f);
        }

        /// <summary>
        /// The generated cookie. A null here is an ERROR rather than a shrug: a cookie-less spot
        /// is a plain green blob, which reads as a broken dot pattern rather than as a missing
        /// texture, and those need different fixes.
        /// </summary>
        private Texture LoadCookie()
        {
            var cookie = Resources.Load<Texture2D>(CookieResourcePath);
            if (cookie == null)
            {
                Core.CIYCLog.Error("[CIYC][DOTS][Projection] Resources.Load(\"" +
                                   CookieResourcePath + "\") is NULL, so the spot has no dot " +
                                   "mask and will project a plain green cone. Run " +
                                   "Catch If You Can > 4. SPIELINHALT > Equipment > " +
                                   "DOTS-Cookies erzeugen.");
            }

            return cookie;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// One block, at switch-on, naming every value that can make a lit projector invisible.
        /// </summary>
        private void ReportProjection()
        {
            var t = _light != null ? _light.transform : transform;
            string cookieName = _light != null && _light.cookie != null
                ? _light.cookie.name : "null";
            string cookieSize = _light != null && _light.cookie != null
                ? _light.cookie.width + "x" + _light.cookie.height : "-";

            string block =
                "[CIYC][DOTS][Projection]" +
                " componentAlive=" + (this != null) +
                " projectionObject=" + (_light != null ? _light.gameObject.name : "<none>") +
                " activeInHierarchy=" + (_light != null && _light.gameObject.activeInHierarchy) +
                " lightExists=" + (_light != null) +
                " lightCount=" + GetComponentsInChildren<Light>(true).Length +
                " lightType=" + (_light != null ? _light.type.ToString() : "-") +
                " lightEnabled=" + (_light != null && _light.enabled) +
                " lightIntensity=" + (_light != null ? _light.intensity.ToString("F2") : "-") +
                " lightRange=" + (_light != null ? _light.range.ToString("F2") : "-") +
                " spotAngle=" + (_light != null ? _light.spotAngle.ToString("F1") : "-") +
                " lightColor=" + (_light != null ? _light.color.ToString() : "-") +
                " cookie=" + cookieName +
                " cookieSize=" + cookieSize +
                " cullingMask=" + (_light != null ? _light.cullingMask.ToString() : "-") +
                // renderingLayerMask is deliberately NOT printed: this machine cannot reach
                // the Unity docs to confirm the member exists on Light in 6000.5, and a stub
                // that agrees with a guess is not verification (mistake 9). cullingMask above
                // is the one that has always been there.
                " worldPosition=" + t.position.ToString("F2") +
                " throwDirection=" + t.forward.ToString("F2") +
                " deviceAxis=" + transform.up.ToString("F2") +
                " deviceRotation=" + transform.rotation.eulerAngles.ToString("F1");

            bool broken = _light == null || !_light.enabled || _light.cookie == null;
            if (broken)
            {
                Core.CIYCLog.Error(block + "  <- FAILED PROJECTION STATE");
            }
            else
            {
                // What this block cannot see: whether URP actually gave the light a slot in its
                // additional-lights cookie atlas. A cookie that is assigned and not applied
                // looks like a plain green cone, and no script-side value says so. If the cone
                // is solid, check the URP asset's Light Cookies switch and atlas size.
                Core.CIYCLog.Info(block);
            }
        }
#endif
    }
}
