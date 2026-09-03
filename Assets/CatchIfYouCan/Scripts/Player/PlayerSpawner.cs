using CatchIfYouCan.Core;
using UnityEngine;

namespace CatchIfYouCan.Player
{
    /// <summary>
    /// Puts the local player into whatever scene is asking, and owns the two things every
    /// caller got wrong differently: when input is live and when the HUD is visible.
    ///
    /// <para>
    /// There were two spawn call sites and they disagreed. The menu controller built a
    /// player, switched its input off, snapped the look and switched input back on after a
    /// fade; the investigation bootstrap built one and left the defaults alone. Neither was
    /// wrong for its own scene, but the sequence lived inside a menu component, so any
    /// third scene - the lobby, once it is its own scene, or a development scene - had to
    /// copy it or do without.
    /// </para>
    ///
    /// <para>
    /// <see cref="PlayerFactory"/> still builds the player. This does not replace it; it
    /// decides where, when and in what state, which is the part that is scene business.
    /// </para>
    /// </summary>
    public static class PlayerSpawner
    {
        /// <summary>The live player, or null.</summary>
        public static PlayerBuildResult Current { get; private set; }

        public static bool HasPlayer => Current != null && Current.Root != null;

        /// <summary>
        /// On desktop, whether enabling input also captures the cursor. Off for scenes that
        /// want the pointer back - the menu sets this from its own serialized flag.
        /// </summary>
        public static bool LockCursorWhenEnabled { get; set; } = true;

        /// <summary>
        /// Creates the player at a scene-provided marker.
        ///
        /// <paramref name="enableInput"/> defaults to true because that is what a scene
        /// entered directly wants. A scene that fades in passes false and calls
        /// <see cref="SetInputEnabled"/> when the screen is visible, so the tap that
        /// started the transition cannot carry through as a look delta.
        /// </summary>
        public static PlayerBuildResult Spawn(Transform spawnPoint, bool enableInput = true,
                                              GameObject prefabOverride = null,
                                              string contextForDiagnostics = null)
        {
            // A missing marker is not a survivable default, it only looks like one. World
            // origin is outside every room this game has, so the player lands under the
            // floor or in the void and it reads on screen as "the level did not load" -
            // which sends the reader after the wrong bug entirely.
            if (spawnPoint == null)
            {
                CIYCLog.Error("No spawn point given to PlayerSpawner" +
                              (string.IsNullOrEmpty(contextForDiagnostics)
                                  ? ""
                                  : " by " + contextForDiagnostics) +
                              ". The player will be created at the world origin, which is " +
                              "almost certainly outside the playable area.");
                return Spawn(Vector3.zero, Quaternion.identity, enableInput, prefabOverride);
            }

            return Spawn(spawnPoint.position, spawnPoint.rotation, enableInput, prefabOverride);
        }

        public static PlayerBuildResult Spawn(Vector3 position, Quaternion rotation,
                                              bool enableInput = true,
                                              GameObject prefabOverride = null)
        {
            if (HasPlayer)
            {
                CIYCLog.Warn("A player already exists; PlayerSpawner is returning the " +
                             "existing one rather than creating a second.");
                return Current;
            }

            Current = prefabOverride != null
                ? BuildFromPrefab(prefabOverride, position, rotation)
                : PlayerFactory.Create(position, rotation);

            if (Current == null || Current.Root == null)
            {
                CIYCLog.Error("PlayerSpawner produced no player.");
                return Current;
            }

            Current.Root.tag = "Player";

            // Placed at construction, so there is no live CharacterController to teleport
            // and no accumulated fall velocity to clear.
            SetInputEnabled(enableInput);

            var look = Current.CameraRoot != null ? Current.CameraRoot.GetComponent<PlayerLook>() : null;
            if (look != null)
                look.SnapTo(rotation, 0f);

            return Current;
        }

        /// <summary>
        /// Movement, look and - on desktop - the cursor. Reversible on purpose: a scene
        /// that hands control back should not leave the pointer captured.
        /// </summary>
        public static void SetInputEnabled(bool enabled)
        {
            if (!HasPlayer)
                return;

            var controller = Current.Root.GetComponent<PlayerController>();
            if (controller != null)
                controller.MovementEnabled = enabled;

            var look = Current.CameraRoot != null ? Current.CameraRoot.GetComponent<PlayerLook>() : null;
            if (look != null)
                look.AllowLook = enabled;

            if (LockCursorWhenEnabled && !Application.isMobilePlatform)
            {
                Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !enabled;
            }
        }

        /// <summary>
        /// The touch HUD is created switched off by the factory, because it exists a whole
        /// fade before anyone should see it.
        /// </summary>
        public static void SetHudVisible(bool visible)
        {
            if (Current != null && Current.TouchHud != null)
                Current.TouchHud.SetActive(visible);
        }

        /// <summary>
        /// Removes the player and tells everything that was following it. Not used by the
        /// production flow yet - scenes unload the player with themselves - but the
        /// registration in LocalPlayerService is process-wide, so leaving a stale entry
        /// behind would outlive the object it names.
        /// </summary>
        public static void Despawn()
        {
            if (Current == null)
                return;

            var root = Current.Root;
            LocalPlayerService.Unregister(root);

            if (Current.TouchHud != null)
                Object.Destroy(Current.TouchHud);

            if (root != null)
                Object.Destroy(root);

            Current = null;
        }

        /// <summary>
        /// The prefab path the investigation bootstrap already supported. Kept verbatim,
        /// including the anchor fallbacks, because a prefab authored against it would
        /// otherwise silently lose its hand anchor.
        /// </summary>
        private static PlayerBuildResult BuildFromPrefab(GameObject prefab, Vector3 position,
                                                         Quaternion rotation)
        {
            var instance = Object.Instantiate(prefab, position, rotation);
            var camera = instance.GetComponentInChildren<Camera>();

            var result = new PlayerBuildResult
            {
                Root = instance,
                HandAnchor = instance.transform.Find("HandAnchor")
                             ?? instance.transform.Find("CameraRoot")
                             ?? instance.transform,
                CameraRoot = instance.transform.Find("CameraRoot"),
                ViewCamera = camera
            };

            // The factory registers what it builds; a prefab has to be announced here, or
            // every consumer of LocalPlayerService silently follows nothing.
            LocalPlayerService.Register(instance, camera,
                                        instance.GetComponentInChildren<AudioListener>());

            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Current = null;
            LockCursorWhenEnabled = true;
        }
    }
}
