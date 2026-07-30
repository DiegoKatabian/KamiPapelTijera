using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleShooter : MonoBehaviour
{
    //para las que solo se shootean
    public GameObject[] particleSystemGameObject;
    Dictionary<GameObject, ParticleSystem[]> particleSystemsDict = new Dictionary<GameObject, ParticleSystem[]>();

    //para las que se instancian
    //public GameObject particlePrefab;
    public float timeToDestroy = 2;
    public Vector3 offset = Vector3.zero;

    //la 0 es sprint particles
    //la 1 es jump particles
    //la 2 va a ser reward received particles
    //la 3 es splash (pisar sobre agua)
    //la 4 es getaffectedbywind viento particles
    //la 5 es footstep (pasos secos)
    //la 6 es runstop (frenada en seco)

    private void Start()
    {
        //get all the particle systems in each gameobject and create a list for each of them

        foreach (GameObject item in particleSystemGameObject)
        {
            //fill the dictionary with each go and its particle systems components in children
            particleSystemsDict.Add(item, item.GetComponentsInChildren<ParticleSystem>());
        }
    }

    public void Shoot(int index)
    {
        //shoot all the ps of the go in the index
        foreach (ParticleSystem item in particleSystemsDict[particleSystemGameObject[index]])
        {
            item.Play();
        }
    }

    public void Enable(int index, bool value)
    {
        foreach (ParticleSystem item in particleSystemsDict[particleSystemGameObject[index]])
        {
            item.gameObject.SetActive(value);
            //Debug.Log("enableo " + item.gameObject.name);
        }
    }

    public void Create(int index, Transform targetTransform)
    {
        //instancia siguiendo al transform dado, aplicando el offset serializado (comportamiento historico)
        if (targetTransform == null)
        {
            Debug.LogWarning($"[ParticleShooter] Create: targetTransform null para el indice {index}, no instancio nada");
            return;
        }

        CreateInternal(index, targetTransform.position + offset, targetTransform.rotation);
    }

    public void Create(int index, Vector3 worldPosition)
    {
        //instancia exactamente en la posicion world dada, SIN offset: el caller ya manda la posicion final (ej: Player.FeetPosition)
        CreateInternal(index, worldPosition, Quaternion.identity);
    }

    void CreateInternal(int index, Vector3 worldPosition, Quaternion worldRotation)
    {
        //logica compartida de ambos overloads de Create: guards + instanciar detached (parent null) + autodestruir
        if (particleSystemGameObject == null || index < 0 || index >= particleSystemGameObject.Length)
        {
            Debug.LogWarning($"[ParticleShooter] Create: indice {index} fuera de rango (array de {(particleSystemGameObject == null ? 0 : particleSystemGameObject.Length)} elementos)");
            return;
        }

        if (particleSystemGameObject[index] == null)
        {
            Debug.LogWarning($"[ParticleShooter] Create: el elemento {index} del array esta vacio, no instancio nada");
            return;
        }

        GameObject particle = Instantiate(particleSystemGameObject[index], worldPosition, worldRotation, null); //parent null: queda world-space, no sigue a kami

        StartCoroutine(DestroyCoroutine(timeToDestroy, particle));
    }

    public IEnumerator DestroyCoroutine(float time, GameObject go)
    {
        yield return new WaitForSeconds(time);
        Destroy(go);
    }
}
