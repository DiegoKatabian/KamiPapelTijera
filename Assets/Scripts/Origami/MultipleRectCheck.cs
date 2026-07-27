using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultipleRectCheck : MonoBehaviour
{
    //este multiple rectangle check solo nace cuando estas dentro de un sello (por ahora origami)
    //asi corre el update pero solo cuando lo necesitamos
    //aca esta la mecanica de arrastrar el mouse y eso

    Origami desiredOrigami;

    bool invocando = false;
    bool arrastrando = false;

    public MultipleRectCheck SetOrigami(Origami ori)
    {
        desiredOrigami = ori;
        return this;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E)) //toggle: E arranca el minijuego, E de nuevo lo cancela. ya no hay que holdear
        {
            if (!invocando)
            {
                StartOrigami(desiredOrigami);
            }
            else
            {
                //print("invocacion cancelada x apretar E de nuevo");
                EndOrigami(desiredOrigami);
            }
        }

        if (invocando && Input.GetMouseButtonDown(0))
        {
            //chequeo si el mouse esta dentro de la imagen de inicio, y habilito el arranque
            if (RectTransformUtility.RectangleContainsScreenPoint(desiredOrigami.origamiRoutes[desiredOrigami.currentRouteIndex].inicioRectangle, Input.mousePosition))
            {
                arrastrando = true;
                AudioManager.instance.PlayRandom("PaperFold01", "PaperFold02");
                AudioManager.instance.PlayByName("PaperFoldLoop");
                CursorManager.Instance.SetCursor(CursorType.ClosedHand);
            }
            else
            {
                Debug.Log("invocaci?n cancelada x empezar en un lugar incorrecto");
                EndOrigami(desiredOrigami);
            }
        }

        //chequeo si el jugador solto el mouse mientras arrastraba
        if (invocando && arrastrando && Input.GetMouseButtonUp(0))
        {
            CursorManager.Instance.SetCursor(CursorType.OpenHand);

            //chequeo si el jugador solto sobre la meta
            if (RectTransformUtility.RectangleContainsScreenPoint(desiredOrigami.origamiRoutes[desiredOrigami.currentRouteIndex].finalRectangle, Input.mousePosition))
            {
                //Debug.Log("invocaci?n exitosa");
                AudioManager.instance.PlayRandom("PaperFold01", "PaperFold02");
                AudioManager.instance.StopByName("PaperFoldLoop");
                //desiredOrigami.CompleteRoute();

                if (desiredOrigami.CompleteRoute())
                {
                    EndOrigami(desiredOrigami);
                }
            }
            else
            {
                //Debug.Log("invocaci?n cancelada x soltar mal");
                AudioManager.instance.PlayByName("Origami_Fail_Crumble", 1, 0.05f);
                EndOrigami(desiredOrigami);
            }

            arrastrando = false;
        }

        bool encimaDeAlgunRectangulo = false;

        foreach (RectTransform rectTransform in desiredOrigami.origamiRoutes[desiredOrigami.currentRouteIndex].routeRectangles) //chequeo si estoy encima de algun rectangulo
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition))
            {
                if (arrastrando)
                {
                    desiredOrigami.origamiRoutes[desiredOrigami.currentRouteIndex].SetImagePosition(Input.mousePosition);
                }
                encimaDeAlgunRectangulo = true; //si s?, todo bien
                break;
            }
        }

        //if (arrastrando && !encimaDeAlgunRectangulo) //si no, end origami
        //{
        //    //me sal? de la ruta
        //    arrastrando = false;
        //    AudioManager.instance.PlayByName("Origami_Fail_Crumble", 1, 0.05f);
        //    Debug.Log("invocaci?n cancelada x salir de la ruta");
        //    EndOrigami(desiredOrigami);
        //}

    }

    public void StartOrigami(Origami origami)
    {
        if (!origami.wasUsed) //me parece que esto no deberia preguntarse aca
        {
            //print("arranca la invocacion");
            origami.gameObject.SetActive(true);
            invocando = true;
            TooltipManager.Instance.ShowTooltip(origami.tooltipMessage, origami.postItColor);
            AudioManager.instance.PlayRandom("MagicChannelingLoop01", "MagicChannelingLoop02");
            CameraManager.Instance.SetCamera(CameraMode.CloseUp);
            EventManager.Trigger(Evento.OnOrigamiStart);
            //print("rect check: mando a actualizar");
            origami.TriggerPliegueTextUpdater();
            CursorManager.Instance.ShowCursor(true);

        }

    }

    public void EndOrigami(Origami origami)
    {
        invocando = false;
        arrastrando = false;
        origami.FailOrigami();
        origami.gameObject.SetActive(false);
        //print("invocacion cancelada");
        TooltipManager.Instance.HideTooltip();
        AudioManager.instance.StopByName("PaperFoldLoop");
        AudioManager.instance.StopByName("MagicChannelingLoop01", "MagicChannelingLoop02");
        CursorManager.Instance.SetCursor(CursorType.OpenHand);
        EventManager.Trigger(Evento.OnOrigamiEnd);
        CursorManager.Instance.ShowCursor(false);
        CameraManager.Instance.SetCamera(CameraMode.Normal);


    }
}
