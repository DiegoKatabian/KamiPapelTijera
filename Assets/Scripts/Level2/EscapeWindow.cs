using UnityEngine;

/// <summary>
/// Trigger just past page 4's escape window, beyond the drapes: Kami entering it has escaped the
/// police station. PoliceStationPage decides what that means.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EscapeWindow : MonoBehaviour
{
    [SerializeField, Tooltip("The page that owns the escape.")]
    PoliceStationPage _page;

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != 3) //layer 3 is the player
        {
            return;
        }

        if (_page == null)
        {
            Debug.LogWarning($"[EscapeWindow] {gameObject.name}: no _page assigned, the escape does nothing");
            return;
        }

        _page.OnKamiCrossedWindow();
    }
}
