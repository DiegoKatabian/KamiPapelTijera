using UnityEngine;

/// <summary>
/// A bench Kami can sit on (Atlas 15's Idle to Sit / Sit Tijera / Sit to Idle). Put this trigger box around
/// the bench: Interact inside it sits her down, Interact again (or moving, or jumping) stands her up.
/// Prefab: Prefabs/Interactables/BenchSeat.prefab.
/// </summary>
public class TriggerBench : TriggerScript
{
    public enum Facing
    {
        KeepCurrent,
        Right,
        Left
    }

    public override bool EsInteractuable => true;

    [SerializeField, Tooltip("Where she sits: X/Z = where she stands, Y = the height her feet would be at for the sit pose to land on the bench (her drawn body is raised or lowered to it). With 'Fit To Bench' on it is placed at Awake from Seat Offset, so moving it by hand does nothing then. Empty = she sits where she stands.")]
    Transform _seat;

    [SerializeField, Tooltip("Which way she faces once seated.")]
    Facing _facing = Facing.KeepCurrent;

    [Header("Fit to the bench (spec 024 3.E)")]
    [SerializeField, Tooltip("On when this BenchSeat is a child of a bench model (Kami_Bench instances, whose scale and rotation vary): at Awake the trigger box is fitted around the parent's meshes plus the padding, and the seat goes to their middle. Off = the box and Seat as authored.")]
    bool _fitToBench = false;

    [SerializeField, Tooltip("World units added around the bench's meshes on each side (x, along the bench's own axes) and above it (y).")]
    Vector3 _fitPadding = new Vector3(1.5f, 2.5f, 1.5f);

    [SerializeField, Tooltip("Fit To Bench only: the Seat relative to the middle of the bench's top, in world units. Y is where her feet go for the sit pose: about -1.8 puts her bottom on Kami_Bench's slab. Tune it here, once, for every bench.")]
    Vector3 _seatOffset = new Vector3(0f, -1.8f, 0f);

    void Awake()
    {
        if (_fitToBench)
        {
            FitToBench();
        }
    }

    //the box in this object's own frame (the bench's, so it follows the bench's rotation), from the bench's mesh bounds
    void FitToBench()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null || transform.parent == null)
        {
            Debug.LogWarning($"[TriggerBench] {gameObject.name}: fit to bench needs a BoxCollider and a bench parent, kept as authored");
            return;
        }

        bool any = false;
        Vector3 min = Vector3.zero, max = Vector3.zero;
        foreach (MeshFilter mf in transform.parent.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.transform.IsChildOf(transform))
            {
                continue;
            }
            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!any)
                {
                    min = max = local;
                    any = true;
                }
                else
                {
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
        }
        if (!any)
        {
            Debug.LogWarning($"[TriggerBench] {gameObject.name}: no bench mesh under '{transform.parent.name}', kept as authored");
            return;
        }

        //padding is in world units: divide by this object's scale on each axis
        Vector3 scale = transform.lossyScale;
        Vector3 pad = new Vector3(_fitPadding.x / Mathf.Max(Mathf.Abs(scale.x), 1e-5f),
                                  _fitPadding.y / Mathf.Max(Mathf.Abs(scale.y), 1e-5f),
                                  _fitPadding.z / Mathf.Max(Mathf.Abs(scale.z), 1e-5f));
        Vector3 lo = new Vector3(min.x - pad.x, min.y, min.z - pad.z);
        Vector3 hi = new Vector3(max.x + pad.x, max.y + pad.y, max.z + pad.z);
        box.center = 0.5f * (lo + hi);
        box.size = hi - lo;

        if (_seat != null)
        {
            Vector3 top = transform.TransformPoint(new Vector3(0.5f * (min.x + max.x), max.y, 0.5f * (min.z + max.z)));
            _seat.position = top + _seatOffset;
        }
        Debug.Log($"[TriggerBench] {gameObject.name}: fitted to '{transform.parent.name}' (seat at {(_seat != null ? _seat.position.ToString() : "-")})");
    }

    public override void Interact(params object[] parameter)
    {
        if (!triggerBool)
        {
            return;
        }

        LevelManager levelManager = LevelManager.Instance;
        if (levelManager == null || levelManager.inDialogue || levelManager.inCutscene)
        {
            return; //the press that advances a line must not also sit her down
        }

        Player kami = levelManager.player;
        if (kami == null)
        {
            Debug.LogWarning($"[TriggerBench] {gameObject.name}: LevelManager has no player, can't sit");
            return;
        }

        if (kami.IsSitting)
        {
            kami.StandUp();
            return;
        }

        bool? faceRight = _facing == Facing.KeepCurrent ? (bool?)null : _facing == Facing.Right;
        if (kami.SitDown(_seat, faceRight))
        {
            Debug.Log($"[TriggerBench] {gameObject.name}: Kami sits down");
        }
    }
}
