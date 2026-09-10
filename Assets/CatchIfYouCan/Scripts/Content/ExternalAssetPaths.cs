namespace CatchIfYouCan.Content
{
    /// <summary>
    /// Canonical import paths for third-party assets shipped with the project.
    /// </summary>
    /// <remarks>
    /// The Kenney kits are gone in full - the Furniture Kit with the house interior it built,
    /// and the last two Mini Dungeon character meshes with the ghosts that used them, which are
    /// Quaternius monsters now. <c>GhostCharacterModels</c> went with them rather than being
    /// left pointing at an empty folder: it was the GATE on the whole integration run, so a
    /// constant naming a folder that no longer exists would not have failed loudly, it would
    /// have made Setup Project skip building every ghost prefab and say so in one line nobody
    /// reads. Nothing here may name a folder that does not exist - a path that resolves nowhere
    /// is this project's most repeated mistake.
    /// </remarks>
    public static class ExternalAssetPaths
    {
        /// <summary>Every rigged ghost mesh in the project. Also the gate on the integration run.</summary>
        public const string QuaterniusMonsters = "Assets/External/Quaternius/Monsters";

        public const string GhostPrefabsRoot = "Assets/CatchIfYouCan/Prefabs/Ghost/Rigged";
        public const string AllMonsterPrefabsRoot = "Assets/CatchIfYouCan/Prefabs/Ghost/AllMonsters";
        public const string PropDefinitionsRoot = "Assets/CatchIfYouCan/ScriptableObjects/Props";
        public const string ContentCatalogResources = "Assets/CatchIfYouCan/Resources/CatchIfYouCan/InvestigationContentCatalog.asset";
        public const string ContentCatalogAsset = "Assets/CatchIfYouCan/ScriptableObjects/Content/InvestigationContentCatalog.asset";
        public const string GhostDefinitionsRoot = "Assets/CatchIfYouCan/ScriptableObjects/Ghosts";
    }
}
