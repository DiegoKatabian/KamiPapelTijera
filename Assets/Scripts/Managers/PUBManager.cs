using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PUBManager : Singleton<PUBManager>
{
    //bueno la idea es asi
    //cuando cambia la pagina, nace el objeto
    //los objetos cuando nacen, nacen con su animacion de ABRIRSE  
    //cuando cambia la pagina nuevamente, disparan su animacion de CERRARSE y mueren
    //como sabe el pagescroller que esta animacion terminó?

    //para abrir no hace falta nada, porque nacen y se abren de una. 
    //pero para cerrar, si

    [SerializeField] List<PUB> pubs = new List<PUB>();

    public void OpenPUBs()
    {
        //Debug.Log("pubmanager: open all pubs");
        foreach (PUB pub in pubs)
        {
            pub.OpenPUB();
        }
    }
    public void ClosePUBs()
    {
        //Debug.Log("pubmanager: close all pubs");
        foreach (PUB pub in pubs)
        {
            pub.ClosePUB();
        }
    }
    public void AddPUB(PUB pub)
    {
        if (!pubs.Contains(pub))
        {
            //Debug.Log("pubmanager: added pub to list");
            pubs.Add(pub);
        }
        else
        {
            //Debug.Log("pubmanager: cant add pub " + pub + " because i already contain it");
        }
    }
    public void RemovePUB(PUB pub)
    {
        //Debug.Log("pubmanager: removed pub from list");
        pubs.Remove(pub);
    }
}
