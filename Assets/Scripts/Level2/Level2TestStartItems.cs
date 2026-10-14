using UnityEngine;

/// <summary>
/// Test starts only (#187): Diego tests a Level 2 page by starting the scene on it
/// (PageScrollerManager.startingPage), which skips the pages before it and the items they give.
/// Without them page 5's Grace never accepts the evidence. Each page owner's test start calls
/// <see cref="GrantItemsBefore"/>; the table lives here once instead of in three Inspectors.
/// Page 5 (MuseumPage) keeps its own setup.
/// </summary>
public static class Level2TestStartItems
{
    //index 0 = what page 1 gives, index 1 = page 2, ...
    static readonly ResourceType[][] ItemsGivenByPage =
    {
        new[] { ResourceType.cafeTicket },                                                       //page 1: the café fold
        new[] { ResourceType.lostGlove, ResourceType.caughtBelonging, ResourceType.brokenWatch }, //page 2: the clues
        new[] { ResourceType.pelusaPainting },                                                   //page 3: the gift box
    };

    /// <summary>
    /// Quietly gives Kami what pages 1 to <paramref name="page"/> - 1 would have (no reward sticker),
    /// skipping what she already holds.
    /// </summary>
    public static void GrantItemsBefore(int page)
    {
        LevelManager levelManager = LevelManager.Instance;
        if (levelManager == null)
        {
            Debug.LogWarning($"[Level2TestStartItems] no LevelManager, page {page}'s test start gets no items");
            return;
        }

        int lastPage = Mathf.Min(page - 1, ItemsGivenByPage.Length);
        for (int i = 0; i < lastPage; i++)
        {
            foreach (ResourceType item in ItemsGivenByPage[i])
            {
                if (levelManager.recursosRecolectados.TryGetValue(item, out int held) && held > 0)
                {
                    continue;
                }

                levelManager.AddResource(item, 1, ownedAtLevelStart: true);
                Debug.Log($"[Level2TestStartItems] page {page} test start: gave {item} (page {i + 1}'s item)");
            }
        }
    }
}
