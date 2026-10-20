using UnityEngine;

/// <summary>
/// The magnifying glass floating over a page 2 clue, and over one paper-only trash can as a decoy.
/// It goes away for good when the trash can under it is opened or the pickup it marks is collected.
/// The hover, pop and sprite swap are its Animator's (ClueLupa.controller), not code.
/// </summary>
public class ClueMarker : MonoBehaviour
{
    [Tooltip("Trash can this marker floats over: opening it removes the marker. Empty = none.")]
    [SerializeField] Solapa _hideWhenOpened;

    [Tooltip("Clue pickup this marker belongs to: collecting it removes the marker. Empty = none.")]
    [SerializeField] GrantResourcePickup _hideWhenCollected;

    void Awake()
    {
        if (_hideWhenOpened == null && _hideWhenCollected == null)
        {
            Debug.LogWarning($"[ClueMarker] {gameObject.name}: neither a trash can nor a pickup is assigned, this marker never goes away");
        }
    }

    void Update()
    {
        bool opened = _hideWhenOpened != null && _hideWhenOpened.IsOpen;
        bool collected = _hideWhenCollected != null && _hideWhenCollected.IsCollected;
        if (!opened && !collected)
        {
            return;
        }

        Debug.Log($"[ClueMarker] {gameObject.name}: {(opened ? "its trash can was opened" : "its clue was collected")}, hiding");
        gameObject.SetActive(false);
    }
}
