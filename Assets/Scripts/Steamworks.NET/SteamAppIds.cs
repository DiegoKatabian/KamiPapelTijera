/// <summary>
/// Which Steam app this build runs as. The full game and its demo are two separate Steam apps (a demo has its
/// own App ID, depots and builds, linked to the full game on the partner site), and the id has to match the
/// app the build is uploaded to: SteamManager passes it to RestartAppIfNecessary.
///
/// The demo build is made by the Kami > Build > Steam Demo (Level 1) menu item, which defines
/// KAMI_STEAM_DEMO for that one build. Any other build (and the Editor) is "the full game", which
/// falls back to the demo id until the full game's own app exists.
/// </summary>
public static class SteamAppIds
{
    /// <summary>0 = the full game's own Steam app has not been created yet.</summary>
    public const uint FullGame = 0;

    /// <summary>The Level 1 demo app (same id as steam_appid.txt at the repo root).</summary>
    public const uint Demo = 3284350;

    public static uint Current
    {
        get
        {
#if KAMI_STEAM_DEMO
            return Demo;
#else
            return FullGame != 0 ? FullGame : Demo;
#endif
        }
    }
}
