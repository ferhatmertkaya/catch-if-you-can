namespace CatchIfYouCan.Ghost
{
    /// <summary>Maps ghost identities and visual profiles to bundled rigged model sources.</summary>
    public static class GhostVisualCatalog
    {
        public static string GetModelAssetPath(string ghostId)
        {
            switch (ghostId)
            {
                case "the_wanderer": return "Assets/External/Quaternius/Monsters/Orc.gltf";
                case "the_whisper": return "Assets/External/Quaternius/Monsters/Demon.gltf";
                case "the_watcher": return "Assets/External/Quaternius/Monsters/BlueDemon.gltf";
                case "the_mimicer": return "Assets/External/Kenney/MiniDungeon/Models/character-human.fbx";
                case "the_hollow": return "Assets/External/Quaternius/Monsters/Demon.gltf";
                case "the_knocker": return "Assets/External/Quaternius/Monsters/Orc.gltf";
                case "the_shadeborn": return "Assets/External/Quaternius/Monsters/BlueDemon.gltf";
                case "the_static": return "Assets/External/Kenney/MiniDungeon/Models/character-orc.fbx";
                case "the_crawler": return "Assets/External/Quaternius/Monsters/CreepCreature.glb";
                case "the_weeping_one": return "Assets/External/Quaternius/Monsters/CreepCreature.glb";
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

        public static string GetPrefabResourcePath(string ghostId)
        {
            return $"CatchIfYouCan/Ghosts/{ghostId}";
        }

        public static string GetPrefabResourcePath(GhostVisualProfile profile)
        {
            return $"CatchIfYouCan/Ghosts/profile_{profile}";
        }

        public static float GetScaleMultiplier(string ghostId)
        {
            switch (ghostId)
            {
                case "the_crawler": return 0.55f;
                case "the_weeping_one": return 0.75f;
                case "the_whisper": return 0.95f;
                case "the_shadeborn": return 1.15f;
                case "the_knocker": return 1.05f;
                default: return 1f;
            }
        }

        public static float GetVerticalOffset(string ghostId)
        {
            return ghostId == "the_crawler" ? 0.15f : 0f;
        }
    }
}
