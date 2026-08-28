#if DEVELOPMENT_BUILD || UNITY_EDITOR
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class DebugMenuUI : MonoBehaviour
    {
        [SerializeField] private Component fpsText;
        [SerializeField] private Component ghostStateText;
        [SerializeField] private Button forceEventButton;
        [SerializeField] private Button forceHuntButton;
        [SerializeField] private Button teleportButton;
        [SerializeField] private Button giveEquipmentButton;
        [SerializeField] private Toggle invincibilityToggle;

        private float _fpsSmoothed;
        private bool _visible;

        public void BindRuntime(
            Component fpsText,
            Component ghostStateText,
            Button forceEventButton,
            Button forceHuntButton,
            Button teleportButton,
            Button giveEquipmentButton,
            Toggle invincibilityToggle)
        {
            this.fpsText = fpsText;
            this.ghostStateText = ghostStateText;
            this.forceEventButton = forceEventButton;
            this.forceHuntButton = forceHuntButton;
            this.teleportButton = teleportButton;
            this.giveEquipmentButton = giveEquipmentButton;
            this.invincibilityToggle = invincibilityToggle;
            WireButtons();
        }

        private void Start() => WireButtons();

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
                ToggleVisible();

            if (!_visible) return;

            _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);
            UITheme.SetText(fpsText, $"FPS: {_fpsSmoothed:0}");

            var ghost = FindFirstObjectByType<GhostController>();
            if (ghost != null)
            {
                Vector3 pos = ghost.transform.position;
                UITheme.SetText(ghostStateText,
                    $"Ghost: {ghost.CurrentState}\nPos: {pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}");
            }
            else
            {
                UITheme.SetText(ghostStateText, "Ghost: none");
            }
        }

        private void WireButtons()
        {
            if (forceEventButton != null)
            {
                forceEventButton.onClick.RemoveAllListeners();
                forceEventButton.onClick.AddListener(() =>
                {
                    var ghost = FindFirstObjectByType<GhostController>();
                    ghost?.RequestInteraction(HorrorEventType.LightFlicker);
                    ghost?.RequestManifestation(2f, true);
                });
            }

            if (forceHuntButton != null)
            {
                forceHuntButton.onClick.RemoveAllListeners();
                forceHuntButton.onClick.AddListener(() =>
                {
                    var ghost = FindFirstObjectByType<GhostController>();
                    ghost?.GetComponent<HuntController>()?.ForceStartHunt();
                });
            }

            if (teleportButton != null)
            {
                teleportButton.onClick.RemoveAllListeners();
                teleportButton.onClick.AddListener(TeleportPlayerToGhost);
            }

            if (giveEquipmentButton != null)
            {
                giveEquipmentButton.onClick.RemoveAllListeners();
                giveEquipmentButton.onClick.AddListener(GiveDefaultEquipment);
            }

            if (invincibilityToggle != null)
            {
                invincibilityToggle.onValueChanged.RemoveAllListeners();
                invincibilityToggle.onValueChanged.AddListener(on =>
                {
                    if (GameManager.Instance != null)
                        GameManager.Instance.Invincible = on;
                });
            }
        }

        private void ToggleVisible()
        {
            _visible = !_visible;
            gameObject.SetActive(_visible);
            if (UIManager.Instance != null && _visible)
                UIManager.Instance.Show(UIScreen.Debug, false);
        }

        private void TeleportPlayerToGhost()
        {
            var player = FindFirstObjectByType<PlayerController>();
            var ghost = FindFirstObjectByType<GhostController>();
            if (player == null || ghost == null) return;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = ghost.transform.position + Vector3.forward * 2f;
            if (cc != null) cc.enabled = true;
        }

        private void GiveDefaultEquipment()
        {
            var mgr = EquipmentManager.Instance;
            if (mgr == null) return;
            mgr.GiveStarterLoadout();
        }
    }
}
#endif
