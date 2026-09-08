using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PaginaActualTextUpdater : TextUpdater
{
    [SerializeField] float fadeDuration = 0.35f;
    [SerializeField] float holdAfterTurn = 2.5f;
    [SerializeField] SpriteSheetAnimator brillitosAnimator;
    [SerializeField] float typewriterCharsPerSecond = 15f;

    Coroutine _fadeRoutine;
    Coroutine _hideRoutine;
    Coroutine _typewriterRoutine;
    int _currentPage = 1;

    protected override void Awake()
    {
        base.Awake();
        EventManager.Subscribe(Evento.OnPageZoneEnter, OnZoneEnter);
        EventManager.Subscribe(Evento.OnPageZoneExit, OnZoneExit);
        EventManager.Subscribe(Evento.OnPageTurnStart, OnTurnStart);
        myText.alpha = 0f;
        if (brillitosAnimator)
        {
            var brillitosImage = brillitosAnimator.GetComponent<Image>();
            if (brillitosImage)
            {
                Color col = brillitosImage.color;
                col.a = 0f;
                brillitosImage.color = col;
            }
        }
    }

    private void Start()
    {
        _currentPage = PageScrollerManager.Instance ? PageScrollerManager.Instance.startingPage : 1;
        StartCoroutine(SetLocalizedText(textoInicial, _currentPage.ToString()));
    }

    void OnZoneEnter(params object[] p)
    {
        CancelHide();
        Fade(1f);
    }

    void OnZoneExit(params object[] p) => Fade(0f);

    void OnTurnStart(params object[] p)
    {
        CancelHide();
        if (brillitosAnimator) brillitosAnimator.Play();
        Fade(0f);
    }

    protected override void UpdateText(params object[] parameter)
    {
        base.UpdateText(parameter);
        if (parameter[0] is int page)
        {
            _currentPage = page;
            if (brillitosAnimator) brillitosAnimator.Play();
            Fade(1f);
            CancelHide();
            StartCoroutine(ShowNewPageWithTypewriter(textoInicial, page.ToString()));
        }
    }

    IEnumerator ShowNewPageWithTypewriter(string key, string pageNumber)
    {
        //esta era una OCTAVA copia de SetLocalizedText, inline. Ahora reusa la de la base:
        //deja el texto localizado, con los prompts del device resueltos y registrado en
        //LocalizedText. La maquina de escribir corre sobre ESE resultado, nunca sobre el crudo.
        yield return StartCoroutine(SetLocalizedText(key, pageNumber));

        myText.maxVisibleCharacters = 0;

        int totalChars = myText.text.Length;
        for (int i = 0; i < totalChars; i++)
        {
            myText.maxVisibleCharacters = i + 1;
            yield return new WaitForSeconds(1f / typewriterCharsPerSecond);
        }

        _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(holdAfterTurn);
        Fade(0f);
    }

    void CancelHide()
    {
        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
    }

    void Fade(float target)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        float start = myText.alpha;
        Image brillitosImage = brillitosAnimator ? brillitosAnimator.GetComponent<Image>() : null;
        Color brillitosStartColor = brillitosImage ? brillitosImage.color : Color.white;

        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            float alpha = Mathf.Lerp(start, target, t / fadeDuration);
            myText.alpha = alpha;

            if (brillitosImage)
            {
                Color col = brillitosImage.color;
                col.a = alpha;
                brillitosImage.color = col;
            }
            yield return null;
        }

        myText.alpha = target;
        if (brillitosImage)
        {
            Color col = brillitosImage.color;
            col.a = target;
            brillitosImage.color = col;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (!gameObject.scene.isLoaded)
        {
            EventManager.Unsubscribe(Evento.OnPageZoneEnter, OnZoneEnter);
            EventManager.Unsubscribe(Evento.OnPageZoneExit, OnZoneExit);
            EventManager.Unsubscribe(Evento.OnPageTurnStart, OnTurnStart);
        }
    }
}
