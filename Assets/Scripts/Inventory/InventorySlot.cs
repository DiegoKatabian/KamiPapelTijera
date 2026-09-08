using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class InventorySlot : MonoBehaviour
{
    public InventoryItem currentItem;

    [SerializeField] protected Image itemImageComponent;
    [SerializeField] protected Image slotStickerImageComponent;
    [SerializeField] protected TextMeshProUGUI nameTextComponent;
    [SerializeField] protected TextMeshProUGUI amountTextComponent;

    protected Color _originalTextColor, _originalSlotColor, _originalIconColor;
    public float upwardsSpeed = 100;

    bool setNamePromised = false;

    private void Start()
    {
        _originalTextColor = nameTextComponent.color;
        _originalSlotColor = slotStickerImageComponent.color;
        _originalIconColor = itemImageComponent.color;
    }

    private void OnEnable()
    {
        if (setNamePromised)
        {
            StartCoroutine(SetLocalizedText(currentItem.itemName, nameTextComponent));
            setNamePromised = false;
        }
    }

    public void SetTransparency(float alpha)
    {
        //Debug.Log("set transparency");
        nameTextComponent.color = new Color(nameTextComponent.color.r, nameTextComponent.color.g, nameTextComponent.color.b, alpha);
        slotStickerImageComponent.color = new Color(slotStickerImageComponent.color.r, slotStickerImageComponent.color.g, slotStickerImageComponent.color.b, alpha);
        itemImageComponent.color = new Color(itemImageComponent.color.r, itemImageComponent.color.g, itemImageComponent.color.b, alpha);
    }

    public void ResetColor()
    {
        //Debug.Log("reset color");
        nameTextComponent.color = _originalTextColor;
        slotStickerImageComponent.color = _originalSlotColor;
        itemImageComponent.color = _originalIconColor;
    }

    public virtual void SetItem(InventoryItem item)
    {
        currentItem = item;

        itemImageComponent.sprite = currentItem.itemSprite;
        slotStickerImageComponent.color = currentItem.itemColor;

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(SetLocalizedText(currentItem.itemName, nameTextComponent));
        }
        else
        {
            setNamePromised = true;
        }
            
        if (InventoryManager.Instance.itemsAmount[currentItem] < 1)
        {
            ClearSlot();
        }
        else if (InventoryManager.Instance.itemsAmount[currentItem] == 1)
        {
            amountTextComponent.text = "";
        }
        else
        {
            amountTextComponent.text = InventoryManager.Instance.itemsAmount[currentItem].ToString();
        }
    }

    public virtual void ClearSlot()
    {
        currentItem = null;
        itemImageComponent.sprite = InventoryManager.Instance.emptyItemSprite;
        //fuera del registro de LocalizedText: si no, un cambio de device le devuelve el
        //nombre del item que acabamos de sacar del slot
        LocalizedText.Limpiar(nameTextComponent);
        nameTextComponent.text = "";
        amountTextComponent.text = "";
        slotStickerImageComponent.color = Color.white;
    }

    public virtual void BUTTON_OnHover()
    {
        AudioManager.instance.PlayByName("Action_Hover", 1);
    }

    public virtual void BUTTON_OnPress()
    {
        AudioManager.instance.PlayByName("PaperFold01", 3f, 0.05f);
        if (currentItem != null)
        {
            InventoryManager.Instance.ShowcaseItem(currentItem);
        }
    }

    public void StartLerpSequence(float duration)
    {
        slotStickerImageComponent.gameObject.SetActive(true);
        StartCoroutine(AlphaLerpSequence(duration));
    }
    public IEnumerator AlphaLerpSequence(float duration)
    {
        StartCoroutine(AlphaLerpFadeIn(duration));
        yield return new WaitForSeconds(duration * 2);
        StartCoroutine(AlphaLerpFadeOut(duration));
    }
    public IEnumerator AlphaLerpFadeIn(float duration)
    {
        float elapsedTime = 0;
        SetTransparency(0);

        while (elapsedTime < duration)
        {
            SetTransparency(Mathf.Lerp(0, 1, elapsedTime / duration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }
    public IEnumerator AlphaLerpFadeOut(float duration)
    {
        float elapsedTime = 0;
        SetTransparency(1);

        while (elapsedTime < duration)
        {
            SetTransparency(Mathf.Lerp(1, 0, elapsedTime / duration));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        ResourceParticleManager.Instance.isShowingRewardSticker = false;
        slotStickerImageComponent.gameObject.SetActive(false);
    }

    //la corrutina se fue a LocalizedText (era una de siete copias identicas). Esta era una de
    //las que NO pasaban por InputPromptSystem: la descripcion de las botas dice "toca SHIFT
    //para ir mas rapido" y la de la tijera "CLIC para cortar", y con joystick eran mentira.
    protected IEnumerator SetLocalizedText(string fallbackText, TMPro.TextMeshProUGUI textElement)
    {
        return LocalizedText.Escribir(textElement, fallbackText, "ItemTable", new LocalizedText.Opciones
        {
            origen = "InventorySlot"
        });
    }

}
