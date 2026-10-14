/// <summary>
/// Which Steam app this build runs as. The full game and its demo are two separate Steam apps (a demo has its
/// own App ID, depots and builds, linked to the full game on the partner site), and the id has to match the
/// app the build is uploaded to: SteamManager passes it to RestartAppIfNecessary.
///
/// The demo build is made by the Kami > Build > Steam Demo (Level 1) menu item, which defines
/// KAMI_STEAM_DEMO for that one build. Any other build (and the Editor) is the full game.
/// </summary>
public static class SteamAppIds
{
    public const uint FullGame = 3284350;

    /// <summary>0 until the demo app exists on the partner site: the demo build refuses to start without it.</summary>
    public const uint Demo = 0;

    public static uint Current
    {
        get
        {
#if KAMI_STEAM_DEMO
            return Demo;
#else
            return FullGame;
#endif
        }
    }
}
