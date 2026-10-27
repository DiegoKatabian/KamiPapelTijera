using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class ImageFadeIn : MonoBehaviour
{
    //esta clase esta mal nombrada. solo sirve para la placa negra.
    //en realidad deberia llamarse "placa negra fade out manager" o algo asi xd

    [SerializeField]
    float fadeSpeed; // velocidad a la que se hace el fade in
    [SerializeField]
    float holdTime; //hold es un tiempito inicial antes de arrancar

    Image image; // la imagen que hace fade in (en este caso, yo mismo)
    bool _isHolding; 
    bool _isOn; //true es TODO NEGRO

    /// <summary>The image has finished fading away (the main menu waits for the kimmiarts logo's, spec 024 2.B).</summary>
    public bool IsFadedOut => !_isOn;
    private void Start()
    {
        image = GetComponent<Image>();

        //empieza vainilla
        image.color = new Color(image.color.r, image.color.g, image.color.b, 1);

        //holdeo un poquito antes de arrancar
        _isHolding = true;
        _isOn = true;
        StartCoroutine(InitialHoldCoroutine());
    }

    private void Update()
    {
        //lo primero es desvanecer una vez para que empiece el juego xd
        if (!_isHolding && _isOn)
        {
            PlacaNegraOff();
            _isHolding = true; 
            //listo, la placa negra se fue y no disparo mas este metodo
        }

    }

    public IEnumerator InitialHoldCoroutine() //al final del hold permito que me cambien el color
    {
        yield return new WaitForSeconds(holdTime);
        _isHolding = false;
    }

    public void PlacaNegraOff() //tengo q hacerle un metodo dedicado para q el Button lo encuentre. sisi el editor de videojuegos mas caro del mundo
    {
        StopAllCoroutines();
        StartCoroutine(BlackOffCoroutine());
    }

    public void PlacaNegraOn()
    {
        StopAllCoroutines();
        StartCoroutine(BlackOnCoroutine());
    }

    public IEnumerator BlackOffCoroutine()
    {
        //print("black off coroutine");
        while (image.color.a > 0)
        {
            image.color = new Color(image.color.r, image.color.g, image.color.b, image.color.a - (fadeSpeed * Time.deltaTime));
            yield return new WaitForEndOfFrame();
        }
        _isOn = false;
    }

    public IEnumerator BlackOnCoroutine()
    {
        //print("black on coroutine");
        while (image.color.a < 1)
        {
            //print("oscurezco...");
            image.color = new Color(image.color.r, image.color.g, image.color.b, image.color.a + (fadeSpeed * Time.deltaTime));
            yield return new WaitForEndOfFrame();
        }
        _isOn = true;
    }
}
