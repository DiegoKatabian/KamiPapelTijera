/// <summary>
/// Every scene the game can load, by identity rather than by name. The mapping from a value here
/// to an actual .unity file lives in one place only: the SceneCatalog asset in Resources.
///
/// Adding a level: add the value here, then add its row in the catalog asset and enable the scene
/// in Build Settings. The validator (Kami/Validate Scene Catalog) checks all three.
/// </summary>
public enum GameScene
{
    MainMenu,
    Level1,
    Level1EndCutscene,
    Level2
}
