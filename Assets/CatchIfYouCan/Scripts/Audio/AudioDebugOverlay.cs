#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Linq;
using CatchIfYouCan.Ghost;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class AudioDebugOverlay : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.F9;
        [SerializeField] private bool visible;

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
                visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();

            const float width = 360f;
            const float height = 280f;
            GUILayout.BeginArea(new Rect(12f, 12f, width, height), _boxStyle);
            GUILayout.Label("CIYC Audio Debug (F9)", _labelStyle);

            var manager = Object.FindAnyObjectByType<AudioManager>();
            var snapshot = manager?.SnapshotController;
            GUILayout.Label($"Snapshot: {(snapshot != null ? snapshot.CurrentSnapshot.ToString() : "—")}", _labelStyle);

            int pooled = AudioEmitterPool.Instance != null ? AudioEmitterPool.Instance.ActiveCount : 0;
            int max = AudioEmitterPool.Instance != null ? AudioEmitterPool.Instance.MaxSimultaneous : 0;
            int playing = Object.FindObjectsByType<AudioSource>().Count(s => s != null && s.isPlaying);
            GUILayout.Label($"Sources: pool {pooled}/{max}, scene {playing}", _labelStyle);

            var ghostAudio = Object.FindAnyObjectByType<GhostAudioController>();
            var ghost = ghostAudio != null ? ghostAudio.GetComponent<GhostController>() : null;
            GUILayout.Label($"Ghost: {(ghost != null ? ghost.CurrentState.ToString() : "—")} hunt={(ghostAudio != null && ghostAudio.IsHuntActive)}", _labelStyle);

            var tension = Object.FindAnyObjectByType<TensionAudioDirector>();
            GUILayout.Label($"Tension: {(tension != null ? tension.Tension.ToString("F1") : "—")}", _labelStyle);

            var reverb = Object.FindAnyObjectByType<ReverbZoneController>();
            var roomTone = Object.FindAnyObjectByType<RoomToneController>();
            GUILayout.Label($"Reverb: {(reverb != null ? reverb.CurrentProfileId : "—")}", _labelStyle);
            GUILayout.Label($"Room Tone: {(roomTone != null ? roomTone.CurrentToneId : "—")}", _labelStyle);

            var occlusion = Object.FindAnyObjectByType<AudioOcclusionController>();
            if (occlusion != null)
                GUILayout.Label($"Occlusion: {occlusion.TrackedSourceCount} tracked, zone={occlusion.ListenerZoneName ?? "None"}", _labelStyle);

            var zones = Object.FindObjectsByType<AmbientZone>();
            int activeZones = zones.Count(z => z != null && z.IsActive);
            GUILayout.Label($"Ambient zones active: {activeZones}/{zones.Length}", _labelStyle);

            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null)
                return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft
            };
            _boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.72f));

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
        }

        private static Texture2D MakeTex(int width, int height, Color color)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Object.FindAnyObjectByType<AudioDebugOverlay>() != null)
                return;

            var go = new GameObject("AudioDebugOverlay");
            go.AddComponent<AudioDebugOverlay>();
        }
    }
}
#endif
