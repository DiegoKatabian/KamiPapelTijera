using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hides everything drawn under a GameObject and later shows back exactly what was hidden. For
/// cutscene beats like "the girls get into the car": the objects stay alive and keep their logic
/// (Kami's CharacterController, an NPC's agent), they just stop being drawn.
///
/// Remembers which renderers it turned off instead of turning everything back on, so a renderer
/// that was already off (the paper-plane hat, an inactive trail) stays off.
/// </summary>
public class HiddenRenderers
{
    readonly List<Renderer> _hidden = new List<Renderer>();

    public static HiddenRenderers Hide(GameObject root)
    {
        HiddenRenderers result = new HiddenRenderers();

        if (root == null)
        {
            Debug.LogWarning("[HiddenRenderers] asked to hide a null GameObject, nothing to hide");
            return result;
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.enabled)
            {
                renderer.enabled = false;
                result._hidden.Add(renderer);
            }
        }

        return result;
    }

    public void Restore()
    {
        foreach (Renderer renderer in _hidden)
        {
            //a renderer can be destroyed while hidden (a despawned object): skip it
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        _hidden.Clear();
    }
}
