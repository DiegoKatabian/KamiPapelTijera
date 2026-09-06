using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Asocia un indice de pagina con un NavMeshData bakeado a mano para esa pagina.
/// El bake clasico de Unity (sin el paquete NavMeshSurface) solo puede incluir
/// geometria activa al momento de bakear, y las paginas del libro son carpetas de
/// GameObjects que se prenden/apagan (ver PageScrollerManager.TogglePages) — por eso
/// cada pagina con enemigos que navegan necesita su PROPIO NavMeshData bakeado por
/// separado, guardado como asset y asignado aca en el Inspector.
/// </summary>
[System.Serializable]
public struct PageNavMeshEntry
{
    public int pageIndex;
    public NavMeshData navMeshData;
}

public class PageNavMeshManager : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Que NavMesh (ya bakeado y guardado como asset separado) corresponde a cada indice de pagina. Paginas sin entrada acá quedan sin NavMesh activo.")]
    private PageNavMeshEntry[] _paginasConNavMesh;

    private NavMeshDataInstance _instanciaActiva;
    private bool _hayInstanciaActiva;

    private void Awake()
    {
        // El bake estatico de la escena auto-carga SU navmesh (el de la ultima pagina bakeada) al cargar
        // la escena, antes de este Awake. Lo sacamos para tener control total via este manager: sin esto,
        // convivirian dos NavMesh superpuestos (el auto-cargado + el que agreguemos nosotros).
        NavMesh.RemoveAllNavMeshData();
    }

    private void Start()
    {
        AplicarParaPagina(PageScrollerManager.Instance.activePageIndex);
        EventManager.Subscribe(Evento.OnNewPageOpen, OnNewPageOpen);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe(Evento.OnNewPageOpen, OnNewPageOpen);
    }

    private void OnNewPageOpen(params object[] parameters)
    {
        // Ignoramos parameters[0]: OnNewPageOpen lo manda con un +1 (convencion de otros sistemas,
        // ver PageScrollerManager). Leemos el indice real directo del singleton en vez de confiar
        // en ese offset.
        AplicarParaPagina(PageScrollerManager.Instance.activePageIndex);
    }

    private void AplicarParaPagina(int pageIndex)
    {
        if (_hayInstanciaActiva)
        {
            NavMesh.RemoveNavMeshData(_instanciaActiva);
            _hayInstanciaActiva = false;
        }

        foreach (PageNavMeshEntry entrada in _paginasConNavMesh)
        {
            if (entrada.pageIndex != pageIndex)
            {
                continue;
            }

            if (entrada.navMeshData == null)
            {
                Debug.LogWarning($"[PageNavMeshManager] La pagina {pageIndex} tiene una entrada configurada pero sin NavMeshData asignado.");
                return;
            }

            _instanciaActiva = NavMesh.AddNavMeshData(entrada.navMeshData);
            _hayInstanciaActiva = true;
            Debug.Log($"[PageNavMeshManager] NavMesh activado para pagina {pageIndex}.");
            return;
        }

        Debug.Log($"[PageNavMeshManager] Pagina {pageIndex} no tiene NavMesh asignado, ningun NavMeshAgent va a poder navegar en esta pagina.");
    }
}
