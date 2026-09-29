using UnityEngine;

/// <summary>
/// Draws a PoliceOfficer's vision cone on the floor at runtime (spec 002 FR-006: the danger has to
/// be readable in the game, not only in the editor). The fan is rebuilt every frame from the
/// officer's own angle/range/facing and is cut short by the same walls that block his line of
/// sight, so what the player sees and what the officer can detect never disagree.
///
/// Colour follows his awareness: calm -> alert -> caught.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VisionConeView : MonoBehaviour
{
    [SerializeField, Tooltip("The officer whose cone this draws. Found in the parents if empty.")]
    PoliceOfficer _officer;

    [SerializeField, Tooltip("Rays in the fan. More = smoother edges against walls.")]
    int _segments = 24;

    [SerializeField, Tooltip("Height above the officer's feet where the walls cut the fan, in world units. Around Kami's body height, so the cone stops where she would be hidden.")]
    float _clipHeight = 1f;

    [SerializeField, Tooltip("Height above the floor the fan is drawn at, to avoid z-fighting with it.")]
    float _floorOffset = 0.08f;

    [SerializeField, Tooltip("Colour while he hasn't noticed anything.")]
    Color _calmColor = new Color(1f, 1f, 0.85f, 0.25f);

    [SerializeField, Tooltip("Colour the moment he is on alert.")]
    Color _alertColor = new Color(1f, 0.8f, 0.1f, 0.4f);

    [SerializeField, Tooltip("Colour when his awareness is full (Kami is caught).")]
    Color _caughtColor = new Color(1f, 0.15f, 0.1f, 0.55f);

    [SerializeField, Tooltip("Optional material. Empty = a plain transparent one made at runtime.")]
    Material _material;

    Mesh _mesh;
    Vector3[] _vertices;
    Color[] _colors;
    int[] _triangles;

    void Awake()
    {
        if (_officer == null)
        {
            _officer = GetComponentInParent<PoliceOfficer>();
        }

        if (_officer == null)
        {
            Debug.LogWarning($"[VisionConeView] {gameObject.name}: no PoliceOfficer found, nothing to draw");
            enabled = false;
            return;
        }

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (_material == null)
        {
            //Sprites/Default: unlit, vertex coloured, transparent, and always included in builds
            _material = new Material(Shader.Find("Sprites/Default"));
        }
        meshRenderer.sharedMaterial = _material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        _segments = Mathf.Max(2, _segments);
        _mesh = new Mesh { name = "VisionCone" };
        _mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = _mesh;

        _vertices = new Vector3[_segments + 2];
        _colors = new Color[_segments + 2];
        _triangles = new int[_segments * 3];
        for (int i = 0; i < _segments; i++)
        {
            _triangles[i * 3] = 0;
            _triangles[i * 3 + 1] = i + 1;
            _triangles[i * 3 + 2] = i + 2;
        }
    }

    //the fan is worked out in world space and converted to this object's local space at the end,
    //so wherever this child sits under the officer never distorts it
    void LateUpdate()
    {
        //a knocked-out officer sees nothing, so his cone disappears
        if (_officer.IsKnockedOut)
        {
            _mesh.Clear();
            return;
        }

        Vector3 feet = _officer.FeetPosition;
        Vector3 origin = feet + Vector3.up * _floorOffset;
        Vector3 rayOrigin = feet + Vector3.up * _clipHeight;
        float halfAngle = _officer.VisionAngle * 0.5f;
        float range = _officer.VisionRange;

        Color color = _officer.IsAlerted
            ? Color.Lerp(_alertColor, _caughtColor, _officer.Awareness)
            : Color.Lerp(_calmColor, _alertColor, _officer.Awareness);

        _vertices[0] = origin;
        _colors[0] = color;

        for (int i = 0; i <= _segments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)_segments);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * _officer.Facing;
            float distance = range;

            if (Physics.Raycast(rayOrigin, direction, out RaycastHit hit, range, _officer.SightBlockers, QueryTriggerInteraction.Ignore))
            {
                distance = hit.distance;
            }

            _vertices[i + 1] = origin + direction * distance;
            Color edge = color;
            edge.a *= 0.35f; //fades toward the edge of his sight
            _colors[i + 1] = edge;
        }

        for (int i = 0; i < _vertices.Length; i++)
        {
            _vertices[i] = transform.InverseTransformPoint(_vertices[i]);
        }

        _mesh.Clear();
        _mesh.vertices = _vertices;
        _mesh.colors = _colors;
        _mesh.triangles = _triangles;
        _mesh.RecalculateBounds();
    }
}
