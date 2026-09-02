using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Art
{
    /// <summary>
    /// Makes the moon the room's light while the room is up, and puts everything back when it
    /// is not.
    ///
    /// <para>
    /// A shaft of moonlight through a window is a shadow effect, not a light effect: what you
    /// actually see is the window opening carved out of a wall that stops everything else. URP
    /// only casts shadows from the <em>main</em> light — additional light shadows are off in this
    /// project's pipeline asset — so a moonbeam with a window cross in it has to be the main
    /// light or it is just a blue wash.
    /// </para>
    ///
    /// <para>
    /// The scene's Sun Source is the Main Menu's own directional, and rewriting that asset field
    /// would change how the menu is lit. So the swap happens here, at runtime, on enable: this
    /// component lives under the lobby, which is inactive until the handover, so the
    /// menu is never touched and the previous sun is restored the moment the room goes away.
    /// </para>
    ///
    /// <para>
    /// The other directionals are switched off for the same duration, and not for tidiness. Once
    /// this light becomes the main one they demote to additional lights, and this pipeline allows
    /// four of those per object — a count the room already spends entirely on the lamp, the fill,
    /// the window glow and the candle. Two more would push one of them off whichever surface
    /// happened to lose the sort, which reads as lights flickering on and off as you walk.
    /// </para>
    ///
    /// <para>
    /// That leaves nothing holding the room off pure black, so ambient does it instead — which is
    /// what ambient is actually for. A dim gradient lifts every surface the way bounced skylight
    /// would, costs no light slot, casts nothing and cannot pop. The scene's own ambient is black
    /// (its mode is Skybox with no skybox assigned) because the menu was composed that way, so
    /// this is applied and restored alongside the sun rather than written into the scene.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Moon Shaft Light")]
    public sealed class MoonShaftLight : MonoBehaviour
    {
        [Tooltip("The moonlight itself. Falls back to a Light on this object.")]
        [SerializeField] private Light shaft;

        [Tooltip("Become the scene's sun while the room is up. Without this the shaft is the " +
                 "wrong kind of light to cast anything.")]
        [SerializeField] private bool takeOverMainLight = true;

        [Tooltip("Switch other directionals off for the duration, so they do not spend the " +
                 "per-object additional-light budget the room's four lamps already use.")]
        [SerializeField] private bool suppressOtherDirectionals = true;

        [Header("Ambient fill")]
        [Tooltip("Lift the room off pure black while it is up. Everything outside the moonbeam " +
                 "is in shadow by design, and with no bounce lighting in this project that would " +
                 "otherwise be absolute black rather than dark.")]
        [SerializeField] private bool overrideAmbient = true;

        [Tooltip("Cool, from above: the sky through the window.")]
        [SerializeField] private Color ambientSky = new Color(0.055f, 0.072f, 0.108f, 1f);

        [Tooltip("The band at eye level, where most of the walls are.")]
        [SerializeField] private Color ambientEquator = new Color(0.042f, 0.047f, 0.060f, 1f);

        [Tooltip("Warmer and darker, off the boards.")]
        [SerializeField] private Color ambientGround = new Color(0.032f, 0.028f, 0.024f, 1f);

        private UnityEngine.Rendering.AmbientMode _previousAmbientMode;
        private Color _previousSky, _previousEquator, _previousGround;
        private float _previousAmbientIntensity;
        private bool _ambientSwapped;

        private Light _previousSun;
        private bool _swapped;
        private readonly List<Light> _suppressed = new List<Light>();

        private void OnEnable()
        {
            if (shaft == null)
                shaft = GetComponent<Light>();

            if (shaft == null || !takeOverMainLight)
                return;

            _previousSun = RenderSettings.sun;
            RenderSettings.sun = shaft;
            _swapped = true;

            ApplyAmbient();

            if (!suppressOtherDirectionals)
                return;

            // Once, on entry. Not a per-frame search.
            var lights = Object.FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null || l == shaft) continue;
                if (l.type != LightType.Directional) continue;
                if (!l.enabled) continue;

                l.enabled = false;
                _suppressed.Add(l);
            }
        }

        private void ApplyAmbient()
        {
            if (!overrideAmbient)
                return;

            _previousAmbientMode = RenderSettings.ambientMode;
            _previousSky = RenderSettings.ambientSkyColor;
            _previousEquator = RenderSettings.ambientEquatorColor;
            _previousGround = RenderSettings.ambientGroundColor;
            _previousAmbientIntensity = RenderSettings.ambientIntensity;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = 1f;
            _ambientSwapped = true;
        }

        private void RestoreAmbient()
        {
            if (!_ambientSwapped)
                return;

            RenderSettings.ambientMode = _previousAmbientMode;
            RenderSettings.ambientSkyColor = _previousSky;
            RenderSettings.ambientEquatorColor = _previousEquator;
            RenderSettings.ambientGroundColor = _previousGround;
            RenderSettings.ambientIntensity = _previousAmbientIntensity;
            _ambientSwapped = false;
        }

        private void OnDisable()
        {
            RestoreAmbient();

            for (int i = 0; i < _suppressed.Count; i++)
                if (_suppressed[i] != null)
                    _suppressed[i].enabled = true;
            _suppressed.Clear();

            // Only put the sun back if it is still ours to put back; something else may have
            // claimed it since, and stamping over that would be worse than leaving it.
            if (_swapped)
            {
                if (RenderSettings.sun == shaft)
                    RenderSettings.sun = _previousSun;
                _swapped = false;
            }
        }
    }
}
