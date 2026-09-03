using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.Development
{
    /// <summary>
    /// The on-screen panel a lab reports itself through: live values on the left, buttons for
    /// the things you want to poke.
    ///
    /// <para>
    /// A lab that cannot show you a number is half a lab. "Is the battery draining", "which
    /// reverb profile am I in", "what state is the ghost in", "what is the joystick actually
    /// returning" are all questions the room cannot answer by being looked at, and all of them
    /// are one line here.
    /// </para>
    ///
    /// <para>
    /// IMGUI, on purpose. It needs no canvas, no prefab, no layout pass and no scene authoring,
    /// which is exactly the trade a development overlay should make - and it is what
    /// <see cref="Audio.AudioDebugOverlay"/> already does, so the labs look like the tool the
    /// project already has.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/Development/Lab Readout")]
    public sealed class DevelopmentLabReadout : MonoBehaviour
    {
        private readonly List<Func<string>> _lines = new List<Func<string>>();
        private readonly List<KeyValuePair<string, Action>> _buttons =
            new List<KeyValuePair<string, Action>>();

        private string _title = "Lab";
        private bool _visible = true;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        [Tooltip("Toggles the panel. Something has to be able to get it out of the way of a " +
                 "screenshot.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F10;

        /// <summary>The panel for this lab, created on first use.</summary>
        public static DevelopmentLabReadout Ensure(string title)
        {
            var existing = FindAnyObjectByType<DevelopmentLabReadout>();
            if (existing == null)
            {
                var go = new GameObject("DEV_LabReadout");
                existing = go.AddComponent<DevelopmentLabReadout>();
            }

            existing._title = title;
            return existing;
        }

        /// <summary>
        /// A line, evaluated every frame it is drawn. A closure rather than a string, because
        /// the whole point is that it changes while you watch it.
        /// </summary>
        public DevelopmentLabReadout Line(Func<string> line)
        {
            if (line != null)
                _lines.Add(line);

            return this;
        }

        /// <summary>A button. For the things a lab has to be able to do to itself.</summary>
        public DevelopmentLabReadout Button(string label, Action action)
        {
            if (!string.IsNullOrEmpty(label) && action != null)
                _buttons.Add(new KeyValuePair<string, Action>(label, action));

            return this;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
                _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible)
                return;

            EnsureStyles();

            float height = 46f + _lines.Count * 18f + _buttons.Count * 24f;
            GUILayout.BeginArea(new Rect(12f, 12f, 380f, height), _boxStyle);
            GUILayout.Label(_title + "  (" + toggleKey + " hides)", _labelStyle);

            for (int i = 0; i < _lines.Count; i++)
            {
                string text;
                try
                {
                    text = _lines[i]();
                }
                catch (Exception e)
                {
                    // A readout that throws must not take the lab down with it. Half the
                    // things being watched here are allowed to be null.
                    text = "<error: " + e.GetType().Name + ">";
                }

                GUILayout.Label(text, _labelStyle);
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (GUILayout.Button(_buttons[i].Key, GUILayout.Height(20f)))
                    _buttons[i].Value?.Invoke();
            }

            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null)
                return;

            _boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 8, 8) };
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false };
            _labelStyle.normal.textColor = new Color(0.7f, 1f, 0.78f);
        }
    }
}
