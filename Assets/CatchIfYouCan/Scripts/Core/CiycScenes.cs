using UnityEngine;

namespace CatchIfYouCan.Core
{
    /// <summary>The production scenes, as an identity rather than as a spelling.</summary>
    public enum CiycScene
    {
        Boot,
        MainMenu,
        Lobby,
        Training,
        Investigation
    }

    /// <summary>
    /// The one place that knows what the production scenes are called and where they live.
    ///
    /// <para>
    /// These four names used to be typed out in eight separate files - the loader, the
    /// per-scene setup switch, the boot hand-off, the pause menu, and four editor tools.
    /// Adding a fifth scene meant finding all eight, and missing one of them failed
    /// silently: a scene the setup switch does not recognise simply receives no camera, no
    /// event system and no UI, with nothing in the console to say so.
    /// </para>
    ///
    /// <para>
    /// Names and paths both live here because both are load-bearing. The runtime loads by
    /// name; the editor tooling opens, validates and registers by path, and a path that
    /// disagrees with a name is exactly the mismatch that leaves a scene out of the build
    /// and working fine in the editor.
    /// </para>
    /// </summary>
    public static class CiycScenes
    {
        public const string SceneFolder = "Assets/CatchIfYouCan/Scenes";

        public const string Boot = "00_Boot";
        public const string MainMenu = "01_MainMenu";
        public const string Lobby = "02_Lobby";
        public const string Training = "02_Training";
        public const string Investigation = "03_Investigation";

        /// <summary>
        /// Build order. Boot must stay first: it is the scene the player starts in.
        ///
        /// Lobby shares the "02" prefix with Training on purpose. The prefix is a reading
        /// aid, not an identifier, and renumbering Training and Investigation would move
        /// two scene files that the build settings, four editor tools and the saved build
        /// list all name explicitly - risk with no functional return.
        /// </summary>
        public static readonly CiycScene[] ProductionOrder =
        {
            CiycScene.Boot,
            CiycScene.MainMenu,
            CiycScene.Lobby,
            CiycScene.Training,
            CiycScene.Investigation
        };

        public static string NameOf(CiycScene scene)
        {
            switch (scene)
            {
                case CiycScene.Boot: return Boot;
                case CiycScene.MainMenu: return MainMenu;
                case CiycScene.Lobby: return Lobby;
                case CiycScene.Training: return Training;
                case CiycScene.Investigation: return Investigation;
                default: return null;
            }
        }

        public static string PathOf(CiycScene scene) => PathOf(NameOf(scene));

        public static string PathOf(string sceneName) =>
            string.IsNullOrEmpty(sceneName) ? null : SceneFolder + "/" + sceneName + ".unity";

        public static bool TryParse(string sceneName, out CiycScene scene)
        {
            for (int i = 0; i < ProductionOrder.Length; i++)
            {
                if (string.Equals(NameOf(ProductionOrder[i]), sceneName, System.StringComparison.Ordinal))
                {
                    scene = ProductionOrder[i];
                    return true;
                }
            }

            scene = default;
            return false;
        }

        public static string[] ProductionNames()
        {
            var names = new string[ProductionOrder.Length];
            for (int i = 0; i < ProductionOrder.Length; i++)
                names[i] = NameOf(ProductionOrder[i]);
            return names;
        }

        public static string[] ProductionPaths()
        {
            var paths = new string[ProductionOrder.Length];
            for (int i = 0; i < ProductionOrder.Length; i++)
                paths[i] = PathOf(ProductionOrder[i]);
            return paths;
        }

        /// <summary>
        /// Whether the running player can actually load this scene.
        ///
        /// A scene missing from the build list still opens in the editor, so the gap only
        /// shows up on device, as a failed load with no explanation. Asking first turns
        /// that into one sentence naming the scene and the fix.
        /// </summary>
        public static bool IsRegisteredInBuild(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }
}
