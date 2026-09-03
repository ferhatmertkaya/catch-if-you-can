namespace CatchIfYouCan.Ghost
{
    /// <summary>Maps ghost identities and visual profiles to bundled rigged model sources.</summary>
    public static class GhostVisualCatalog
    {
        public static string GetModelAssetPath(string ghostId)
        {
            switch (ghostId)
            {
                case GhostIds.Wanderer: return "Assets/External/Quaternius/Monsters/Orc.gltf";
                case GhostIds.Whisper: return "Assets/External/Quaternius/Monsters/Demon.gltf";
                case GhostIds.Watcher: return "Assets/External/Quaternius/Monsters/BlueDemon.gltf";
                case GhostIds.Mimicer: return "Assets/External/Kenney/MiniDungeon/Models/character-human.fbx";
                case GhostIds.Hollow: return "Assets/External/Quaternius/Monsters/Demon.gltf";
                case GhostIds.Knocker: return "Assets/External/Quaternius/Monsters/Orc.gltf";
                case GhostIds.Shadeborn: return "Assets/External/Quaternius/Monsters/BlueDemon.gltf";
                case GhostIds.Static: return "Assets/External/Kenney/MiniDungeon/Models/character-orc.fbx";
                case GhostIds.Crawler: return "Assets/External/Quaternius/Monsters/CreepCreature.glb";
                case GhostIds.WeepingOne: return "Assets/External/Quaternius/Monsters/CreepCreature.glb";
                default: return GetModelAssetPathForProfile(GhostVisualProfile.HumanSilhouette);
            }
        }

        public static string GetModelAssetPathForProfile(GhostVisualProfile profile)
        {
            switch (profile)
            {
                case GhostVisualProfile.TallShadow:
                case GhostVisualProfile.FacelessFigure:
                    return "Assets/External/Quaternius/Monsters/Demon.gltf";
                case GhostVisualProfile.CrawlingEntity:
                case GhostVisualProfile.ChildShadow:
                    return "Assets/External/Quaternius/Monsters/CreepCreature.glb";
                case GhostVisualProfile.DistortedWoman:
                    return "Assets/External/Quaternius/Monsters/BlueDemon.gltf";
                default:
                    return "Assets/External/Quaternius/Monsters/Orc.gltf";
            }
        }

        /// <summary>Resources subfolder ghost prefabs are looked up in.</summary>
        public const string PrefabResourceFolder = "Ghosts/";

        /// <summary>
        /// Where a ghost's prefab is looked for under Resources.
        ///
        /// <para>
        /// This used to return "CatchIfYouCan/Ghosts/{id}". A Resources path is relative to a
        /// Resources folder, and this project's is Assets/CatchIfYouCan/Resources - so the old
        /// path resolved to Assets/CatchIfYouCan/Resources/CatchIfYouCan/Ghosts, a folder that
        /// has never existed. Every lookup missed, silently, and every ghost in the game was
        /// the primitive capsule fallback. The project name was in the path twice.
        /// </para>
        /// </summary>
        public static string GetPrefabResourcePath(string ghostId)
        {
            return PrefabResourceFolder + ghostId;
        }

        public static string GetPrefabResourcePath(GhostVisualProfile profile)
        {
            return PrefabResourceFolder + "profile_" + profile;
        }

        public static float GetScaleMultiplier(string ghostId)
        {
            switch (ghostId)
            {
                case GhostIds.Crawler: return 0.55f;
                case GhostIds.WeepingOne: return 0.75f;
                case GhostIds.Whisper: return 0.95f;
                case GhostIds.Shadeborn: return 1.15f;
                case GhostIds.Knocker: return 1.05f;
                default: return 1f;
            }
        }

        public static float GetVerticalOffset(string ghostId)
        {
            return ghostId == GhostIds.Crawler ? 0.15f : 0f;
        }
    }
}
