using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows an Animator-driven flipbook Image in the current language: the clip keys the Spanish
/// frames, and every frame after the Animator has written one this swaps it for the same frame of
/// the selected locale. One clip for every language instead of a clip and a controller per language.
/// Used by the café ticket origami (its receipt has Spanish words printed on it).
/// </summary>
[RequireComponent(typeof(Image))]
public class LocalizedFlipbook : MonoBehaviour
{
    [Tooltip("The flipbook's frames per language. Its first entry must hold the frames the animation clip keys.")]
    [SerializeField] LocalizedSprites _frames;

    Image _image;

    void Awake()
    {
        _image = GetComponent<Image>();
        if (_frames == null)
        {
            Debug.LogWarning($"[LocalizedFlipbook] {gameObject.name}: _frames is not assigned, the flipbook stays in Spanish");
        }
    }

    //LateUpdate: after the Animator wrote this frame's sprite, before the canvas draws it
    void LateUpdate()
    {
        if (_frames == null)
        {
            return;
        }

        Sprite current = _image.sprite;
        Sprite localized = _frames.Localize(current);
        if (localized != current)
        {
            _image.sprite = localized;
        }
    }
}
