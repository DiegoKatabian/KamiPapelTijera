using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public enum PostItColor
{
    Azul,
    Naranja,
    Rosa,
    Amarillo,
    Verde
}

public class TooltipManager : Singleton<TooltipManager>
{
    //orquestador de post-its: localiza el texto y le pide al PostIt correcto que se muestre/esconda.
    //el ciclo de vida (fades + timer de muerte) vive en cada PostIt: aca NO hay timers.
    //antes habia un unico timer global compartido por todos los colores y cada ShowTooltip
    //lo reseteaba y apagaba TODO junto: el kill time de un color pisaba al de los demas.

    [SerializeField, Tooltip("Fallback en segundos si el killTime del color esta en 0 o negativo")]
    float killTime = 2f;

    [SerializeField, Tooltip("Segundos que vive el post-it Azul antes de esconderse solo")]
    float azulKillTime = 2f;

    [SerializeField, Tooltip("Segundos que vive el post-it Naranja antes de esconderse solo")]
    float naranjaKillTime = 5f;

    [SerializeField, Tooltip("Segundos que vive el post-it Rosa antes de esconderse solo")]
    float rosaKillTime = 2f;

    [SerializeField, Tooltip("Segundos que vive el post-it Amarillo antes de esconderse solo")]
    float amarilloKillTime = 2f;

    [SerializeField, Tooltip("Segundos que vive el post-it Verde antes de esconderse solo")]
    float verdeKillTime = 2f;

    [SerializeField, Tooltip("Un PostIt por color, en el mismo orden que el enum PostItColor")]
    PostIt[] postIts;

    //quien mostro por ultima vez cada color. HAY UN SOLO PostIt POR COLOR, y varios triggers
    //distintos pueden compartirlo (hoy la abuela y las dos esferas de cambio de pagina apuntan
    //todas al Azul): sin dueno, el exit de un trigger escondia el tooltip que OTRO acababa de
    //mostrar. El guard _shownThisEntry de TriggerScript solo protegia ENTRE colores distintos,
    //no dentro del mismo color, asi que el bug se "contagiaba" entre triggers hermanos.
    readonly Dictionary<PostItColor, object> _owners = new Dictionary<PostItColor, object>();

    //muestra el post-it del color pedido con el texto localizado.
    //si ya estaba visible: se actualiza el texto y se reinicia su timer, sin parpadeo de fade
    public void ShowTooltip(string text, PostItColor postIt)
    {
        ShowTooltip(text, postIt, null);
    }

    //owner = quien pide mostrar (los triggers pasan 'this'). Queda registrado como dueno del
    //color para que despues solo EL pueda esconderlo con HideTooltip(color, owner).
    //owner null = show anonimo (origami, sonidos, NPC.Die): no reclama el post-it
    public void ShowTooltip(string text, PostItColor postIt, object owner)
    {
        PostIt target = GetPostIt(postIt);
        if (target == null)
        {
            return; //GetPostIt ya logueo el warning
        }

        _owners[postIt] = owner;

        //los post-its pueden arrancar desactivados a mano en la escena: los prendemos UNA sola
        //vez y de aca en mas quedan siempre activos (el "apagado" es alpha 0 via CanvasGroup).
        //nunca mas SetActive(false): mataria las corrutinas del PostIt (timer + fades)
        if (!target.gameObject.activeSelf)
        {
            target.gameObject.SetActive(true);
        }

        if (target.tmPro == null)
        {
            //Debug.LogWarning($"[TooltipManager] el PostIt {postIt} no tiene tmPro asignado, no puedo escribirle el texto");
        }
        else
        {
            StartCoroutine(SetLocalizedText(text, target.tmPro));
        }

        float colorKillTime = GetKillTimeFor(postIt);
        //Debug.Log($"[TooltipManager] muestro {postIt} por {colorKillTime}s");
        target.Show(colorKillTime);
    }

    protected IEnumerator SetLocalizedText(string fallbackText, TMPro.TextMeshProUGUI textElement)
    {
        if (!string.IsNullOrEmpty(fallbackText))
        {
            // Obtenemos la tabla de localizacion
            var tableOperation = LocalizationSettings.StringDatabase.GetTableAsync("TooltipTable");
            yield return tableOperation;

            StringTable stringTable = tableOperation.Result;
            if (stringTable != null)
            {
                // Verificamos si la clave existe en la tabla
                var entry = stringTable.GetEntry(fallbackText);
                if (entry != null && !string.IsNullOrEmpty(entry.GetLocalizedString()))
                {
                    //InputPromptSystem: el texto de la tabla nombra teclas ("Toca E"), asi que
                    //si el jugador esta con joystick hay que decirle el boton que existe de verdad
                    textElement.text = InputPromptSystem.Procesar(entry.GetLocalizedString());
                }
                else
                {
                    textElement.text = InputPromptSystem.Procesar(fallbackText);
                }
            }
            else
            {
                //si la tabla no cargo, igual escribimos ALGO: un post-it visible con texto
                //viejo o vacio es mucho mas dificil de diagnosticar que este warning
                Debug.LogWarning("[TooltipManager] no pude cargar TooltipTable, uso el texto sin localizar");
                textElement.text = InputPromptSystem.Procesar(fallbackText);
            }
        }
        else
        {
            textElement.text = fallbackText;
        }
    }

    //esconde TODOS los post-its (con fade). Hide sobre uno ya escondido es un no-op,
    //asi que barrer todos los colores es barato y no genera parpadeos
    public void HideTooltip()
    {
        if (postIts == null)
        {
            Debug.LogWarning("[TooltipManager] el array de postIts no esta asignado, no puedo esconder nada");
            return;
        }

        for (int i = 0; i < postIts.Length; i++)
        {
            if (postIts[i] == null)
            {
                Debug.LogWarning($"[TooltipManager] el PostIt en el indice {i} es null, lo salteo");
                continue;
            }

            postIts[i].Hide();
        }

        //un hide global (muerte del player, fin de origami) le saca el post-it a cualquier dueno
        _owners.Clear();
    }

    //esconde SOLO el post-it de ese color: es lo que usan los triggers al salir,
    //para no pisar post-its de otros colores que siguen vivos
    public void HideTooltip(PostItColor postIt)
    {
        HideTooltip(postIt, null);
    }

    //owner = quien pide esconder. Si el color lo esta usando OTRO dueno (porque ese otro lo
    //mostro despues), no lo escondemos: el tooltip que se ve en pantalla no es el nuestro.
    //owner null = hide incondicional (camino historico, para los que nunca reclamaron el color)
    public void HideTooltip(PostItColor postIt, object owner)
    {
        PostIt target = GetPostIt(postIt);
        if (target == null)
        {
            return; //GetPostIt ya logueo el warning
        }

        object currentOwner;
        if (owner != null
            && _owners.TryGetValue(postIt, out currentOwner)
            && currentOwner != null
            && !ReferenceEquals(currentOwner, owner))
        {
            Debug.Log($"[TooltipManager] no escondo {postIt}: lo mostro otro despues ({currentOwner}), el pedido vino de {owner}");
            return;
        }

        _owners.Remove(postIt);
        target.Hide();
    }

    PostIt GetPostIt(PostItColor color)
    {
        int index = (int)color;

        if (postIts == null || index < 0 || index >= postIts.Length)
        {
            Debug.LogWarning($"[TooltipManager] no hay PostIt para el color {color}: revisa el array postIts en el inspector");
            return null;
        }

        if (postIts[index] == null)
        {
            Debug.LogWarning($"[TooltipManager] el PostIt del color {color} es null en el array: revisa el inspector");
            return null;
        }

        return postIts[index];
    }

    float GetKillTimeFor(PostItColor color)
    {
        float result;

        switch (color)
        {
            case PostItColor.Azul:
                result = azulKillTime;
                break;
            case PostItColor.Naranja:
                result = naranjaKillTime;
                break;
            case PostItColor.Rosa:
                result = rosaKillTime;
                break;
            case PostItColor.Amarillo:
                result = amarilloKillTime;
                break;
            case PostItColor.Verde:
                result = verdeKillTime;
                break;
            default:
                result = killTime;
                break;
        }

        //el killTime global viejo quedo como FALLBACK: si un color esta mal configurado
        //(0 o negativo), usamos el global asi el post-it nunca queda pegado para siempre
        if (result <= 0f)
        {
            Debug.LogWarning($"[TooltipManager] el killTime de {color} es {result}, uso el fallback global de {killTime}s");
            result = killTime;
        }

        return result;
    }
}
