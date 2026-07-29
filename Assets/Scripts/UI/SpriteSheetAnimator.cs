using UnityEngine;
using UnityEngine.UI;

public class SpriteSheetAnimator : MonoBehaviour
{
    [SerializeField] Sprite[] spriteFrames;
    [SerializeField] float framesPerSecond = 10f;
    [SerializeField] bool loopAnimation = false;

    Image _image;
    int _currentFrame = 0;
    float _frameTimer = 0f;
    bool _isPlaying = false;

    void Awake()
    {
        _image = GetComponent<Image>();
        if (spriteFrames.Length > 0)
            _image.sprite = spriteFrames[0];

        Play();
    }

    void Update()
    {
        if (!_isPlaying || spriteFrames.Length == 0) return;

        _frameTimer += Time.deltaTime;
        float frameDuration = 1f / framesPerSecond;

        if (_frameTimer >= frameDuration)
        {
            _frameTimer -= frameDuration;
            _currentFrame++;

            if (_currentFrame >= spriteFrames.Length)
            {
                if (loopAnimation)
                    _currentFrame = 0;
                else
                    Stop();
            }

            if (_isPlaying)
                _image.sprite = spriteFrames[_currentFrame];
        }
    }

    public void Play()
    {
        if (spriteFrames.Length == 0) return;
        _isPlaying = true;
        _currentFrame = 0;
        _frameTimer = 0f;
        _image.sprite = spriteFrames[0];
    }

    public void Stop()
    {
        _isPlaying = false;
    }
}
