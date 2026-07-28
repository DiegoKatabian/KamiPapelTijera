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
    Count 
}

//por ahi los resourcetype deberian estar en el resourcemanager
//que deberia estar conectado con el inventorymanager

public class LevelManager : Singleton<LevelManager>
{
    public string Level1SceneName = "Nivel1_LaRural";
    public bool agency;
    public bool inDialogue;

    //el dictionario capo con cada tipo de recurso y valor
    public Dictionary<ResourceType, int> recursosRecolectados = new Dictionary<ResourceType, int>();

    public Player player;

    public bool enablePCheat = false;



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
        if (gameObject.scene.name == Level1SceneName)
        {
            AudioManager.instance.StopByName("IntroStoryboardLoop");
            AudioManager.instance.PlayByName("MemoFloraMainLoop01");
            AudioManager.instance.PlayByName("ForestAtDay");
        }
    }

    public void Update()
    {

        if (enablePCheat)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                AllItemsCheat();
            }
        }

        if (Input.GetKey(KeyCode.LeftControl) && player != null)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                AllItemsCheat();
            }

            if (Input.GetKeyDown(KeyCode.F12))
            {
                GoToScene("MainMenu");
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
        //TODO: el swap de material era del modelo 3d viejo (ya borrado). cuando la animadora arme
        //un skin/feedback de botas de agua en el skeleton de spine, aplicarlo aca.
        AddResource(ResourceType.botasAgua, 1);
    }
    public void GiveTijeraMejorada()
    {
        //Debug.Log("el player se gano la tijera mejorada x haber completado la quest");
        player.hasTijera = true;
        player.GetTijeraMejorada();
        AddResource(ResourceType.tijeraMejorada, 1);
    }
    public void GoToScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void AllItemsCheat()
    {
        AddResource(ResourceType.hongos, 100);
        AddResource(ResourceType.papel, 100);
        AddResource(ResourceType.flores, 100);
        GiveWaterBoots();
        GiveSprintBoots();
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
