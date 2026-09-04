namespace CatchIfYouCan.Content
{
    /// <summary>
    /// Canonical import paths for third-party assets shipped with the project.
    /// </summary>
    /// <remarks>
    /// The Kenney Furniture Kit and the house half of the Mini Dungeon kit were removed: the
    /// house interior is being replaced by a purchased modular pack that is not integrated yet.
    /// Only the two humanoid meshes the ghosts use survive, and they are named for what they
    /// are used for rather than for the kit they came from. Nothing here may name a folder that
    /// does not exist - a path that resolves nowhere is this project's most repeated mistake.
    /// </remarks>
    public static class ExternalAssetPaths
    {
        public const string GhostCharacterModels = "Assets/External/Kenney/MiniDungeon/Models";
        public const string QuaterniusMonsters = "Assets/External/Quaternius/Monsters";

        public const string GhostPrefabsRoot = "Assets/CatchIfYouCan/Prefabs/Ghost/Rigged";
        public const string AllMonsterPrefabsRoot = "Assets/CatchIfYouCan/Prefabs/Ghost/AllMonsters";
        public const string PropDefinitionsRoot = "Assets/CatchIfYouCan/ScriptableObjects/Props";
        public const string ContentCatalogResources = "Assets/CatchIfYouCan/Resources/CatchIfYouCan/InvestigationContentCatalog.asset";
        public const string ContentCatalogAsset = "Assets/CatchIfYouCan/ScriptableObjects/Content/InvestigationContentCatalog.asset";
        public const string GhostDefinitionsRoot = "Assets/CatchIfYouCan/ScriptableObjects/Ghosts";
    }
}
