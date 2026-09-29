using UnityEngine;

/// <summary>
/// Makes a patrolling cop cuttable (Diego, 2026-09-28): the scissors' hitbox finds this ICortable on
/// the officer's trigger collider and knocks him out. Kept as a tiny bridge, like RocosoCortable,
/// so PoliceOfficer itself doesn't have to know about the scissors.
///
/// Any scissors work (normal or upgraded): the cop is not a boss, he is an obstacle Kami can
/// answer when she gets spotted.
/// </summary>
public class PoliceOfficerCortable : MonoBehaviour, ICortable
{
    [SerializeField, Tooltip("The officer this collider belongs to. Found on this GameObject if empty.")]
    PoliceOfficer _officer;

    void Awake()
    {
        if (_officer == null)
        {
            _officer = GetComponent<PoliceOfficer>();
        }

        if (_officer == null)
        {
            Debug.LogWarning($"[PoliceOfficerCortable] {gameObject.name}: no PoliceOfficer found, cutting him will do nothing");
        }
    }

    public void GetCut(float dmg)
    {
        if (_officer == null || _officer.IsKnockedOut)
        {
            return;
        }

        AudioManager.instance.Play(AudioId.TijeraHit);
        _officer.KnockOut();
    }
}
