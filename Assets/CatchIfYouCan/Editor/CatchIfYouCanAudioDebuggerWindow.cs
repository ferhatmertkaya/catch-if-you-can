using System.Collections.Generic;
using System.Linq;
using CatchIfYouCan.Audio;
using CatchIfYouCan.Ghost;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    public class CatchIfYouCanAudioDebuggerWindow : EditorWindow
    {
        private Vector2 _scroll;
        private string[] _eventIds = System.Array.Empty<string>();
        private int _selectedEventIndex;
        private readonly Dictionary<AudioMixerGroupId, bool> _muteStates = new Dictionary<AudioMixerGroupId, bool>();
        private double _nextRefresh;

        [MenuItem("Catch If You Can/9. ENTWICKLER - DEBUG/Audio Debugger [NUR LESEN]", false, 970)]
        public static void ShowWindow()
        {
            GetWindow<CatchIfYouCanAudioDebuggerWindow>("CIYC Audio Debugger");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Catch If You Can — Audio Debugger", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Play Mode focused. Enter Play to inspect live audio state.", MessageType.Info);

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("Not in Play Mode.");
                if (GUILayout.Button("Refresh Event List (Edit Mode)"))
                    RefreshEventIds(null);
                DrawEventPlaybackSection();
                return;
            }

            if (EditorApplication.timeSinceStartup >= _nextRefresh)
            {
                _nextRefresh = EditorApplication.timeSinceStartup + 0.25;
                Repaint();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSnapshotSection();
            DrawSourceSection();
            DrawGhostSection();
            DrawTensionSection();
            DrawReverbSection();
            DrawOcclusionSection();
            DrawAmbientSection();
            DrawEventPlaybackSection();
            DrawMuteSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSnapshotSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Snapshot", EditorStyles.boldLabel);
            var manager = Object.FindAnyObjectByType<AudioManager>();
            var snapshot = manager?.SnapshotController;
            EditorGUILayout.LabelField("Current", snapshot != null ? snapshot.CurrentSnapshot.ToString() : "—");
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Active Sources", EditorStyles.boldLabel);
            int pooled = AudioEmitterPool.Instance != null ? AudioEmitterPool.Instance.ActiveCount : 0;
            int max = AudioEmitterPool.Instance != null ? AudioEmitterPool.Instance.MaxSimultaneous : 0;
            int sceneSources = Object.FindObjectsByType<AudioSource>()
                .Count(s => s != null && s.isPlaying);
            EditorGUILayout.LabelField("Emitter Pool", $"{pooled} / {max}");
            EditorGUILayout.LabelField("Scene AudioSources (playing)", sceneSources.ToString());
        }

        private void DrawGhostSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Ghost Audio", EditorStyles.boldLabel);
            var ghostAudio = Object.FindAnyObjectByType<GhostAudioController>();
            if (ghostAudio == null)
            {
                EditorGUILayout.LabelField("GhostAudioController", "Not present");
                return;
            }

            var ghost = ghostAudio.GetComponent<GhostController>();
            EditorGUILayout.LabelField("State", ghost != null ? ghost.CurrentState.ToString() : "—");
            EditorGUILayout.LabelField("Hunt Active", ghostAudio.IsHuntActive ? "Yes" : "No");
        }

        private void DrawTensionSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Tension", EditorStyles.boldLabel);
            var tension = Object.FindAnyObjectByType<TensionAudioDirector>();
            EditorGUILayout.LabelField("Score", tension != null ? $"{tension.Tension:F1} / 100" : "—");
        }

        private void DrawReverbSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Room Reverb", EditorStyles.boldLabel);
            var reverb = Object.FindAnyObjectByType<ReverbZoneController>();
            var roomTone = Object.FindAnyObjectByType<RoomToneController>();
            EditorGUILayout.LabelField("Profile", reverb != null ? reverb.CurrentProfileId : "—");
            EditorGUILayout.LabelField("Room Tone", roomTone != null ? roomTone.CurrentToneId : "—");
        }

        private void DrawOcclusionSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Occlusion", EditorStyles.boldLabel);
            var occlusion = Object.FindAnyObjectByType<AudioOcclusionController>();
            if (occlusion == null)
            {
                EditorGUILayout.LabelField("AudioOcclusionController", "Not present");
                return;
            }

            EditorGUILayout.LabelField("Tracked Sources", occlusion.TrackedSourceCount.ToString());
            EditorGUILayout.LabelField("Listener Zone", occlusion.ListenerZoneName ?? "None");
        }

        private void DrawAmbientSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Ambient Events", EditorStyles.boldLabel);
            var zones = Object.FindObjectsByType<AmbientZone>();
            if (zones.Length == 0)
            {
                EditorGUILayout.LabelField("No AmbientZone instances");
                return;
            }

            for (int i = 0; i < zones.Length; i++)
            {
                var z = zones[i];
                if (z == null) continue;
                string status = z.IsActive ? "ACTIVE" : "idle";
                EditorGUILayout.LabelField(z.name, $"{z.AmbientEventId} ({status})");
            }
        }

        private void DrawEventPlaybackSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Play Audio Event", EditorStyles.boldLabel);

            if (_eventIds.Length == 0 || GUILayout.Button("Refresh Event IDs"))
                RefreshEventIds(Object.FindAnyObjectByType<AudioManager>()?.EventLibrary);

            if (_eventIds.Length == 0)
            {
                EditorGUILayout.LabelField("No events found.");
                return;
            }

            _selectedEventIndex = EditorGUILayout.Popup("Event", _selectedEventIndex, _eventIds);
            if (GUILayout.Button("Play Selected Event") && Application.isPlaying)
            {
                string id = _eventIds[_selectedEventIndex];
                Object.FindAnyObjectByType<AudioManager>()?.PlayEvent(id);
            }
        }

        private void DrawMuteSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Mute Mixer Groups (Runtime Bus)", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("Available in Play Mode.");
                return;
            }

            var router = RuntimeAudioBusRouter.Instance;
            if (router == null)
            {
                EditorGUILayout.LabelField("RuntimeAudioBusRouter not found.");
                return;
            }

            foreach (AudioMixerGroupId group in System.Enum.GetValues(typeof(AudioMixerGroupId)))
            {
                if (group == AudioMixerGroupId.Master)
                    continue;

                bool muted = router.IsMuted(group);
                bool next = EditorGUILayout.ToggleLeft(group.ToString(), muted);
                if (next != muted)
                    router.SetMuted(group, next);
            }
        }

        private void RefreshEventIds(AudioEventLibrary library)
        {
            var ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (library != null)
            {
                foreach (var pair in library.Events)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key))
                        ids.Add(pair.Key);
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:AudioEventDefinition", new[] { "Assets/CatchIfYouCan" });
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var def = AssetDatabase.LoadAssetAtPath<AudioEventDefinition>(path);
                if (def != null && !string.IsNullOrWhiteSpace(def.EventId))
                    ids.Add(def.EventId);
            }

            _eventIds = ids.OrderBy(s => s).ToArray();
            _selectedEventIndex = Mathf.Clamp(_selectedEventIndex, 0, Mathf.Max(0, _eventIds.Length - 1));
        }
    }
}
