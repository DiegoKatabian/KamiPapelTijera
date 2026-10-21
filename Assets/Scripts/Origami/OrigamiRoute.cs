using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OrigamiRoute : MonoBehaviour
{
    public RectTransform startingPoint;
    public RectTransform inicioRectangle; //flechaverde
    public List<RectTransform> routeRectangles; //el camino
    public RectTransform finalRectangle; //flecharoja

    [HideInInspector] public bool wasCompleted;
    float interpolation = 0;
    Vector3 distanciaMouseInicio;
    Vector3 distanciaTotal;
    Vector3 posicionInicioOriginal;
    bool originalPositionWasSet = false;

    [SerializeField] Animator _flipbookAnimator;

    Graphic _sheetGraphic;

    /// <summary>The flipbook image of this route's sheet: what flashes red when the fold fails. Null (with a warning) if the route has no flipbook.</summary>
    public Graphic SheetGraphic
    {
        get
        {
            if (_sheetGraphic == null && _flipbookAnimator != null)
            {
                _sheetGraphic = _flipbookAnimator.GetComponent<Graphic>();
            }
            return _sheetGraphic;
        }
    }

    /// <summary>Centre of the sheet in world space; the failure message floats up from here. Falls back to the route itself.</summary>
    public Vector3 SheetCenterWorld => _flipbookAnimator != null ? _flipbookAnimator.transform.position : transform.position;

    private void Start()
    {
        //print("agregue los rectangulos inicio y fin a la lista");
        //routeRectangles.Add(inicioRectangle);
        routeRectangles.Add(finalRectangle);

        posicionInicioOriginal = startingPoint.position;
        originalPositionWasSet = true;
        distanciaTotal = finalRectangle.position - startingPoint.position;

    }

    public void SetOrigamiSliderValue(float value)
    {
        _flipbookAnimator.SetFloat("_OrigamiSlider", value); //seteo el valor del slider del material
    }

    public void SetImagePosition(Vector3 mousePosition)
    {
        //Debug.Log("SET IMAGE POSITION calculo posicion de la imagen inicio");
        //tiene el bug de que no me sirve para rutas no rectas
        distanciaMouseInicio = mousePosition - posicionInicioOriginal;

        interpolation = distanciaMouseInicio.magnitude / distanciaTotal.magnitude;

        inicioRectangle.position = Vector3.Lerp(posicionInicioOriginal, finalRectangle.position, interpolation);
        SetOrigamiSliderValue(interpolation);
    }

    public void ResetImagePosition()
    {
        if (originalPositionWasSet)
        {
            //Debug.Log("RESET IMAGE POSITION");
            inicioRectangle.position = posicionInicioOriginal;
        }
    }

    /// <summary>
    /// Starts this fold over after a failure, with the origami still open: the green arrow goes back to
    /// its start and the sheet goes back to its first frame. ResetImagePosition alone is not enough here:
    /// the flipbook keeps the slider value of the failed drag, and (unlike the old close-and-reopen flow)
    /// this route's object is never switched off, so nothing else would reset it.
    /// </summary>
    public void RestartFold()
    {
        ResetImagePosition();
        interpolation = 0f;

        if (_flipbookAnimator != null)
        {
            SetOrigamiSliderValue(0f);
        }
    }
}
