using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level 2 page 4: what the police take from Kami and hand back at the evidence pickup. The ONE
/// place that remembers confiscated amounts, so the escape can be retried any number of times.
///
/// Confiscating ADDS to what is already held: if Kami is caught again before reaching the pickup,
/// whatever she gathered in between (typewriter paper) joins the stash, and the pickup always
/// returns everything. Nothing is ever lost to a capture. The paper-plane hat is the exception:
/// it is simply taken away (its paper was already spent folding it).
///
/// It does not decide WHEN to confiscate: PoliceStationPage calls ConfiscateAll on arrival and on
/// every restart after a capture. The pickup calls ReturnAll.
/// </summary>
public class ConfiscatedGear : MonoBehaviour
{
    [SerializeField, Tooltip("Inventory items taken (all of each). The scissors are always taken on top of these.")]
    ResourceType[] _confiscatedResources = { ResourceType.papel, ResourceType.pelusaPainting };

    [SerializeField, Tooltip("The evidence pickup. Shown only while there is something to give back.")]
    ConfiscatedGearPickup _pickup;

    readonly Dictionary<ResourceType, int> _stash = new Dictionary<ResourceType, int>();
    bool _scissorsTaken;

    public bool HasSomethingToReturn
    {
        get
        {
            if (_scissorsTaken)
            {
                return true;
            }

            foreach (int amount in _stash.Values)
            {
                if (amount > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    void Start()
    {
        if (_pickup == null)
        {
            Debug.LogWarning($"[ConfiscatedGear] {gameObject.name}: no _pickup assigned, Kami can never get her things back");
        }
    }

    public void ConfiscateAll()
    {
        LevelManager levelManager = LevelManager.Instance;
        Player player = levelManager != null ? levelManager.player : null;
        if (player == null)
        {
            Debug.LogWarning("[ConfiscatedGear] no LevelManager/Player in the scene, nothing confiscated");
            return;
        }

        foreach (ResourceType resource in _confiscatedResources)
        {
            int held = levelManager.recursosRecolectados[resource];
            if (held <= 0)
            {
                continue;
            }

            _stash.TryGetValue(resource, out int alreadyHeld);
            _stash[resource] = alreadyHeld + held;
            levelManager.AddResource(resource, -held);
        }

        if (player.hasTijera)
        {
            player.LoseTijera();
            _scissorsTaken = true;
        }

        if (player.isPaperPlaneHat)
        {
            player.DestroyPaperPlaneHat();
        }

        Debug.Log($"[ConfiscatedGear] confiscated. Holding: scissors={_scissorsTaken}, {DescribeStash()}");
        RefreshPickup();
    }

    public void ReturnAll()
    {
        LevelManager levelManager = LevelManager.Instance;
        Player player = levelManager != null ? levelManager.player : null;
        if (player == null)
        {
            Debug.LogWarning("[ConfiscatedGear] no LevelManager/Player in the scene, nothing returned");
            return;
        }

        Debug.Log($"[ConfiscatedGear] returning: scissors={_scissorsTaken}, {DescribeStash()}");

        foreach (KeyValuePair<ResourceType, int> entry in _stash)
        {
            if (entry.Value > 0)
            {
                levelManager.AddResource(entry.Key, entry.Value);
            }
        }
        _stash.Clear();

        if (_scissorsTaken)
        {
            _scissorsTaken = false;
            player.GetTijera(); //the same "you got your scissors" reward pose as any scissors pickup
        }

        AudioManager.instance.Play(AudioId.PickupSpecial);
        RefreshPickup();
    }

    void RefreshPickup()
    {
        if (_pickup != null)
        {
            _pickup.gameObject.SetActive(HasSomethingToReturn);
        }
    }

    string DescribeStash()
    {
        if (_stash.Count == 0)
        {
            return "no items";
        }

        List<string> parts = new List<string>();
        foreach (KeyValuePair<ResourceType, int> entry in _stash)
        {
            parts.Add($"{entry.Value}x {entry.Key}");
        }
        return string.Join(", ", parts);
    }
}
