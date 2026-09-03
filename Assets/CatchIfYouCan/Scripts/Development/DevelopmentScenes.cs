namespace CatchIfYouCan.Development
{
    /// <summary>Which lab a development scene is.</summary>
    public enum DevelopmentLab
    {
        Equipment,
        Character,
        Interaction,
        Ghost,
        Audio,
        Lighting,
        Environment,
        UIInput,
        Network
    }

    /// <summary>
    /// The development labs, kept deliberately apart from
    /// <see cref="CatchIfYouCan.Core.CiycScenes"/>.
    ///
    /// <para>
    /// The separation is the safety mechanism, not a filing preference. The production
    /// scene list is what the build tooling turns into a shipped build; if labs lived in
    /// the same list, shipping one would be a matter of forgetting a filter rather than
    /// deliberately adding a path. Nothing here is ever a production scene, so the build
    /// path can assert that instead of trusting a checkbox in the editor.
    /// </para>
    /// </summary>
    public static class DevelopmentScenes
    {
        public const string Folder = "Assets/CatchIfYouCan/Scenes/Development";

        public const string Equipment = "DEV_EquipmentLab";
        public const string Character = "DEV_CharacterLab";
        public const string Interaction = "DEV_InteractionLab";
        public const string Ghost = "DEV_GhostLab";
        public const string Audio = "DEV_AudioLab";
        public const string Lighting = "DEV_LightingLab";
        public const string Environment = "DEV_EnvironmentLab";
        public const string UIInput = "DEV_UIInputLab";
        public const string Network = "DEV_NetworkLab";

        public static readonly DevelopmentLab[] All =
        {
            DevelopmentLab.Equipment,
            DevelopmentLab.Character,
            DevelopmentLab.Interaction,
            DevelopmentLab.Ghost,
            DevelopmentLab.Audio,
            DevelopmentLab.Lighting,
            DevelopmentLab.Environment,
            DevelopmentLab.UIInput,
            DevelopmentLab.Network
        };

        public static string NameOf(DevelopmentLab lab)
        {
            switch (lab)
            {
                case DevelopmentLab.Equipment: return Equipment;
                case DevelopmentLab.Character: return Character;
                case DevelopmentLab.Interaction: return Interaction;
                case DevelopmentLab.Ghost: return Ghost;
                case DevelopmentLab.Audio: return Audio;
                case DevelopmentLab.Lighting: return Lighting;
                case DevelopmentLab.Environment: return Environment;
                case DevelopmentLab.UIInput: return UIInput;
                case DevelopmentLab.Network: return Network;
                default: return null;
            }
        }

        public static string PathOf(DevelopmentLab lab) => Folder + "/" + NameOf(lab) + ".unity";

        public static string[] AllNames()
        {
            var names = new string[All.Length];
            for (int i = 0; i < All.Length; i++)
                names[i] = NameOf(All[i]);
            return names;
        }

        public static string[] AllPaths()
        {
            var paths = new string[All.Length];
            for (int i = 0; i < All.Length; i++)
                paths[i] = PathOf(All[i]);
            return paths;
        }

        /// <summary>
        /// Whether a scene path is a development scene.
        ///
        /// Matches on the folder rather than on the DEV_ prefix, because the folder is what
        /// a person moving a file can see and the prefix is what a person renaming one
        /// forgets. Both are checked, so neither alone can smuggle a lab into a build.
        /// </summary>
        public static bool IsDevelopmentScenePath(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                return false;

            var normalised = scenePath.Replace('\\', '/');
            if (normalised.StartsWith(Folder + "/", System.StringComparison.OrdinalIgnoreCase))
                return true;

            int slash = normalised.LastIndexOf('/');
            var file = slash >= 0 ? normalised.Substring(slash + 1) : normalised;
            return file.StartsWith("DEV_", System.StringComparison.Ordinal);
        }
    }
}
