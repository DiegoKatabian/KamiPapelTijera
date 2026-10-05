using UnityEngine;
using UnityFx.Outline;

// Kami's outline, drawn through walls like the 3D-era one (#36). Lives on the Spine skeleton
// object. Moving that object onto the "Kami Outline" layer would NOT work: the layer-mask path of
// the OutlineFeature draws with an override material that drops the atlas texture, and Spine
// draws every body part as a textured quad, so the mask would be a pile of rectangles. Registering
// the object in an OutlineLayerCollection takes the package's per-renderer path instead, which
// (with EnableAlphaTesting on the layer) binds each submesh's own atlas page and clips by its
// alpha: the mask is her real silhouette. Without EnableDepthTesting it ignores scene depth, so it
// also shows behind walls and page geometry.
// Color, width and render flags live in the layer's OutlineSettings asset, not here.
public class KamiOutline : MonoBehaviour
{
    [SerializeField, Tooltip("The collection the URP OutlineFeature renders (Assets/URPSettings/OutlineLayerCollection). " +
        "Only ONE OutlineFeature may reference it, or Kami is outlined once per feature.")]
    OutlineLayerCollection _layers;

    [SerializeField, Tooltip("Index of the layer in that collection (0 = \"Player\").")]
    int _layerIndex = 0;

    [SerializeField, Tooltip("Off = no outline. Read when the object is enabled.")]
    bool _showOutline = true;

    OutlineLayer _layer;

    void OnEnable()
    {
        if (!_showOutline)
        {
            Debug.Log("[KamiOutline] Outline switched off in the Inspector");
            return;
        }

        if (_layers == null)
        {
            Debug.LogWarning($"[KamiOutline] '{name}': no OutlineLayerCollection assigned, no outline");
            return;
        }

        if (_layerIndex < 0 || _layerIndex >= _layers.Count)
        {
            Debug.LogWarning($"[KamiOutline] '{name}': layer {_layerIndex} doesn't exist in '{_layers.name}' ({_layers.Count} layers), no outline");
            return;
        }

        _layer = _layers[_layerIndex];
        _layer.Add(gameObject);
        Debug.Log($"[KamiOutline] '{name}' added to outline layer '{_layer.Name}' (mode {_layer.OutlineRenderMode})");
    }

    // The collection is a shared asset that outlives the scene: always take her back out of it.
    void OnDisable()
    {
        if (_layer == null)
        {
            return;
        }

        _layer.Remove(gameObject);
        _layer = null;
    }
}
