using System.Collections;
using UnityEngine;

/// <summary>
/// Gives Kami a resource ONCE, pops up, shrinks and destroys itself. Two ways to be collected:
///
/// - PlayerTouches: walk into it (the page 2 hat and watch clues). Deliberately not an
///   A/E interaction: Natalia follows Kami closely and her own dialogue trigger would compete
///   for the same button press.
/// - Revealed: collected the moment it is activated. This is what goes inside a trash can,
///   as TriggerSolapa's objetoParaMostrar: opening the can reveals it, and revealing IS
///   collecting, so the can and its contents never fight over the same button press either.
///
/// It destroys itself after collecting, so TriggerSolapa's toggle finds nothing on later
/// opens: a trash can is emptied once, not farmed.
/// </summary>
public class GrantResourcePickup : MonoBehaviour
{
    public enum CollectMode
    {
        PlayerTouches,
        Revealed
    }

    [SerializeField, Tooltip("PlayerTouches = collected by walking into its trigger. Revealed = collected the moment it gets activated (use inside a trash can).")]
    CollectMode _collectMode = CollectMode.PlayerTouches;

    [SerializeField, Tooltip("What Kami gets.")]
    ResourceType _resource = ResourceType.papel;

    [SerializeField, Tooltip("How many she gets.")]
    int _amount = 1;

    [SerializeField, Tooltip("How far up the visual rises while it disappears, in world units.")]
    float _popHeight = 1.5f;

    [SerializeField, Tooltip("Seconds the pop-and-shrink takes before the object is destroyed.")]
    float _popSeconds = 0.6f;

    bool _collected;

    void OnEnable()
    {
        if (_collectMode == CollectMode.Revealed)
        {
            Collect();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_collectMode != CollectMode.PlayerTouches)
        {
            return;
        }

        if (other.gameObject.layer == 3) //layer 3 is the player
        {
            Collect();
        }
    }

    void Collect()
    {
        if (_collected)
        {
            //a trash can closed mid-pop deactivates us and kills the coroutine; the next open
            //re-enables us, so finish disappearing instead of lingering (and never grant twice)
            StartCoroutine(PopAndDestroy());
            return;
        }

        if (LevelManager.Instance == null)
        {
            Debug.LogWarning($"[GrantResourcePickup] {gameObject.name}: there is no LevelManager in the scene, cannot grant {_amount}x {_resource}.");
            return;
        }

        _collected = true;
        Debug.Log($"[GrantResourcePickup] {gameObject.name}: granting {_amount}x {_resource}");

        //same order as PickupCortable: position the "you got this" sticker first, then add
        EventManager.Trigger(Evento.OnObjectWasCut, transform.position);
        LevelManager.Instance.AddResource(_resource, _amount);
        AudioManager.instance.Play(AudioId.PickupSFX);

        StartCoroutine(PopAndDestroy());
    }

    IEnumerator PopAndDestroy()
    {
        Vector3 startPosition = transform.localPosition;
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < _popSeconds)
        {
            float t = elapsed / Mathf.Max(0.01f, _popSeconds);
            transform.localPosition = startPosition + Vector3.up * (_popHeight * Mathf.Sin(t * Mathf.PI * 0.5f));
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
