#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Builds the Nathan character assets if they are not there yet.
    ///
    /// <para>
    /// The character is assembled by <see cref="NathanCharacterSetup"/> rather than checked in,
    /// because the prefab and the Animator Controller reference objects inside the imported FBX
    /// whose file IDs only Unity's importer can mint. That was always going to need one manual
    /// menu click, and the manual click is exactly what went missing: the player spawned with no
    /// body at all, because <c>Resources/Characters/Player_CharacterVisual.prefab</c> did not
    /// exist and the factory's load returned null.
    /// </para>
    ///
    /// <para>
    /// So the click is no longer required. This runs once per editor session, does nothing at all
    /// when the prefab is already present, and skips batch builds where generating assets would be
    /// a surprise. It is a safety net, not a build step — running the menu item by hand still works
    /// and is still the way to rebuild after changing the model.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class NathanAutoSetup
    {
        private const string ResourcePrefabPath =
            "Assets/CatchIfYouCan/Resources/Characters/Player_CharacterVisual.prefab";

        private const string FbxPath =
            "Assets/CatchIfYouCan/Art/Characters/Nathan/Models/rp_nathan_animated_003_walking.fbx";

        // Survives domain reloads within a session, so a build that fails does not retry on every
        // recompile and fill the console.
        private const string AttemptedKey = "CIYC.NathanAutoSetup.Attempted";

        static NathanAutoSetup()
        {
            // Deferred: on a fresh open the asset database is still importing when static
            // constructors run, and asking for the model now would report it missing.
            EditorApplication.delayCall += TryBuild;
        }

        private static void TryBuild()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (SessionState.GetBool(AttemptedKey, false))
                return;

            // The sky and the flame material are cheap to check and independent of the
            // character, so they are brought up to date whether or not the character needs
            // building.
            if (!InteractiveRoomSkySetup.IsBuilt())
                InteractiveRoomSkySetup.Build();

            if (!CandleFlameSetup.IsBuilt())
                CandleFlameSetup.BuildMenuItem();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ResourcePrefabPath) != null)
                return;   // already built, nothing to do and nothing to say

            if (AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) == null)
            {
                // The model itself is missing, which is a different problem and not one this
                // should paper over by building an empty character.
                Debug.LogWarning("[CIYC] Nathan model not found at " + FbxPath +
                                 ". The player will spawn without a body until it is imported.");
                SessionState.SetBool(AttemptedKey, true);
                return;
            }

            SessionState.SetBool(AttemptedKey, true);

            Debug.Log("[CIYC] " + ResourcePrefabPath + " is missing, so the character is being " +
                      "built now. This runs once; use Catch If You Can > Characters > Build " +
                      "Nathan Player Visual to rebuild it by hand.");

            NathanCharacterSetup.Build();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ResourcePrefabPath) == null)
                Debug.LogError("[CIYC] The character build ran but produced no prefab at " +
                               ResourcePrefabPath + ". The player will have no visible body.");
        }

        /// <summary>Lets the safety net run again without restarting the editor.</summary>
        [MenuItem("Catch If You Can/Assets bauen/Nathan neu bauen, falls fehlend [SCHREIBT ASSET]", false, 1011)]
        private static void ForceRetry()
        {
            SessionState.SetBool(AttemptedKey, false);
            TryBuild();
        }
    }
}
#endif
