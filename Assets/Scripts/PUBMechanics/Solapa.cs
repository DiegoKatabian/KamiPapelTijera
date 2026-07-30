using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Solapa : MonoBehaviour
{
    [Tooltip("Particulas one-shot que se reproducen como feedback al abrir o cerrar la solapa")]
    [SerializeField] ParticleSystem brillitosMagicos;

    protected bool isOn;
    protected Animator anim;

    /// <summary>
    /// Indica si la solapa esta abierta actualmente.
    /// </summary>
    public bool IsOpen
    {
        get { return isOn; }
    }

    private void Awake()
    {
        // Se cachea en Awake (y no en Start) para que CambiarEstado nunca encuentre anim en null
        anim = GetComponent<Animator>();

        // Fallback: si no se wireo la referencia en el prefab, la buscamos en los hijos
        if (brillitosMagicos == null)
        {
            brillitosMagicos = GetComponentInChildren<ParticleSystem>(true);
        }
    }

    public virtual void CambiarEstado()
    {
        isOn = !isOn;
        anim.SetBool("isOpen", isOn);

        if (brillitosMagicos != null)
        {
            brillitosMagicos.Play();
        }
        else
        {
            Debug.LogWarning("[Solapa] No hay ParticleSystem de brillitos asignado, se omite el feedback visual");
        }

        AudioManager.instance.PlayByName("PaperFold01", 1.5f, 0.1f);
    }
}
