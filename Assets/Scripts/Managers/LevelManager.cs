using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ResourceType
{
    hongos,
    flores,
    papel,
    botasAgua,
    botasRapidas,
    tijera,
    tijeraMejorada,
    abuela,
    caughtBelonging,   // Level 2: the robber's hat, snagged on a manhole cover (page 2 clue)
    brokenWatch,       // Level 2: broken wristwatch, evidence of the robbery time
    unusedArielScarfCap, // UNUSED (retired 2026-09-28: Ariel's evidence is the hat + glove). Kept so the values below keep their serialized ints
    pelusaPainting,    // Level 2: the stolen Pelusa painting itself
    lostGlove,         // Level 2: a single glove found in a page 2 trash can (appended last so the serialized ints above keep their meaning)
    cafeTicket,        // Level 2: the café ticket Kami folds on page 1 (OrigamiItemGiver); unfolded on page 5 as proof of innocence
    Count
}

//por ahi los resourcetype deberian estar en el resourcemanager
//que deberia estar conectado con el inventorymanager

public class LevelManager : Singleton<LevelManager>
{
    public bool agency;
    public bool inDialogue;

    //a cutscene owns Kami: no movement, jump or attack, but Interact still advances its dialogue.
    //Separate from inDialogue because DialogueManager clears that flag at the end of EVERY dialogue,
    //and a cutscene strings several dialogues together (see CutsceneDirector)
    [HideInInspector] public bool inCutscene;

    //el dictionario capo con cada tipo de recurso y valor
    public Dictionary<ResourceType, int> recursosRecolectados = new Dictionary<ResourceType, int>();

    public Player player;

    public bool enableCheats = false;
    public bool enableEditorCheats = false;

    protected override void Awake()
    {
        if (Instance != this && Instance != null)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }

        for (int i = 0; i < (int)ResourceType.Count; i++) //creo el dict con uno de cada pickuptype y 0
        {
            recursosRecolectados.Add((ResourceType)i, 0);
        }
    }

    private void Start()
    {
        if (gameObject.scene.name == SceneCatalog.NameOf(GameScene.Level1))
        {
            AudioManager.instance.StopById(AudioId.IntroStoryboardLoop);

            AudioManager.instance.Play(AudioId.MemoFloraMainLoop01);
            AudioManager.instance.Play(AudioId.ForestAtDay);
        }

        if (gameObject.scene.name == SceneCatalog.NameOf(GameScene.Level2))
        {
            AudioManager.instance.StopById(AudioId.IntroStoryboardLoop);
            AudioManager.instance.StopById(AudioId.MemoFloraMainLoop01);
            AudioManager.instance.StopById(AudioId.MemoFloraPostBattle01);
            AudioManager.instance.StopById(AudioId.MemoFloraBattleLoop01);

            AudioManager.instance.Play(AudioId.BohrenDestroyingAngels);
            //AudioManager.instance.PlayByName("ForestAtDay");
        }
    }

    public void Update()
    {

        if (!enableCheats)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.P) && enableEditorCheats)
        {
            AllItemsCheat();
        }

        if (Input.GetKey(KeyCode.LeftControl) && player != null)
        {
            
            if (Input.GetKeyDown(KeyCode.F12))
            {
                GoToScene(GameScene.MainMenu);
            }

            if (Input.GetKeyDown(KeyCode.F1))
            {
                GoToScene(GameScene.Level1);
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                GoToScene(GameScene.Level2);
            }
        }
    }

    public void GiveSprintBoots()
    {
        //Debug.Log("el player se gano las botas sprint x haber completado la quest");
        player.hasSprintBoots = true;
        AddResource(ResourceType.botasRapidas, 1);
    }
    public void GiveWaterBoots()
    {
        //Debug.Log("el player se gano las botas water x haber completado la quest");
        player.hasWaterBoots = true;
        //no visual code here: adding the resource equips Gear_RainBoots on Kami (spec 011, Player.EquipGainedGear)
        AddResource(ResourceType.botasAgua, 1);
    }
    public void GiveTijeraMejorada()
    {
        //Debug.Log("el player se gano la tijera mejorada x haber completado la quest");
        player.hasTijera = true;
        player.GetTijeraMejorada();
        AddResource(ResourceType.tijeraMejorada, 1);
    }
    /// <summary>
    /// The only way to change scenes. Takes an identity, not a name: the name comes from the
    /// SceneCatalog asset, so a renamed scene keeps working and a missing one errors loudly
    /// instead of loading nothing.
    /// </summary>
    public void GoToScene(GameScene scene)
    {
        string sceneName = SceneCatalog.NameOf(scene);
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[LevelManager] cannot load '{scene}': not in the catalog.");
            return;
        }

        Debug.Log($"[LevelManager] loading scene '{sceneName}' ({scene})");
        SceneManager.LoadScene(sceneName);
    }

    public void AllItemsCheat()
    {
        AddResource(ResourceType.hongos, 100);
        AddResource(ResourceType.papel, 100);
        AddResource(ResourceType.flores, 100);
        GiveWaterBoots();
        //no sprint boots (Diego, 2026-10-05): nothing gives them any more, the Lightfall winged boots replace them later
        GiveTijeraMejorada();
    }

    public void AddResource(ResourceType pickupType, int valueToAdd)
    {
        //agrega la cantidad valuetoadd al total. si quiero restar, valuetoadd deberia ser negativo
        bool isAdding = false;
        recursosRecolectados[pickupType] += valueToAdd;

        if (valueToAdd >= 1)
        {
            isAdding = true;
        }

        EventManager.Trigger(Evento.OnResourceUpdated, pickupType, recursosRecolectados[pickupType], isAdding);
    }
    public void AddHealth(int curacion)
    {
        player.GetCured(curacion);
    }
    
    public void GameObjectActivator(List<GameObject> gameObjectsToActivate, List<GameObject> gameObjectsToDeactivate)
    {
        foreach (GameObject go in gameObjectsToActivate)
        {
            go.SetActive(true);
        }
        foreach (GameObject go in gameObjectsToDeactivate)
        {
            go.SetActive(false);
        }
    }
}
