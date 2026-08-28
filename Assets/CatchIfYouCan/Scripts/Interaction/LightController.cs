using UnityEngine;

namespace CatchIfYouCan.Interaction
{
    public class LightController : MonoBehaviour
    {
        [SerializeField] private Light[] lights;
        [SerializeField] private Renderer[] emissiveRenderers;
        [SerializeField] private Color emissiveColor = Color.white;
        [SerializeField] private bool startOn = true;

        public bool IsOn { get; private set; }

        private void Awake()
        {
            SetOn(startOn, false);
        }

        public void Toggle()
        {
            SetOn(!IsOn);
        }

        public void SetOn(bool on, bool invokeEvents = true)
        {
            IsOn = on;

            if (lights != null)
            {
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null)
                        lights[i].enabled = on;
                }
            }

            if (emissiveRenderers != null)
            {
                for (int i = 0; i < emissiveRenderers.Length; i++)
                {
                    Renderer r = emissiveRenderers[i];
                    if (r == null)
                        continue;

                    if (on)
                        r.material.EnableKeyword("_EMISSION");
                    else
                        r.material.DisableKeyword("_EMISSION");

                    r.material.SetColor("_EmissionColor", on ? emissiveColor : Color.black);
                }
            }
        }
    }
}
