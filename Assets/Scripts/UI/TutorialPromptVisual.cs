using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial flotante de input (WASD, SPACE, click de origami): muestra la variante de
/// teclado/mouse o la de joystick segun el device activo, y cambia en vivo si el jugador
/// cambia de device a mitad de partida.
///
/// Por que existe (issue #41): estos tutoriales hoy muestran iconos fijos de teclado/mouse.
/// Con un joystick en la mano esos dibujos son informacion falsa. El arte de joystick
/// TODAVIA NO EXISTE (Valentino lo va a pasar), asi que este componente deja el sistema
/// LISTO de antemano: los campos de joystick arrancan vacios a proposito, y mientras sigan
/// vacios el fallback es mostrar SIEMPRE la variante de teclado (nunca dejar el prompt en
/// blanco ni romper por referencia faltante).
///
/// Sirve tanto para tutoriales en el mundo (SpriteRenderer, ej. Tutorial_WASD/Tutorial_Space
/// en FloatingTutorials.prefab) como para UI (Image, ej. el click de origami en
/// "OrigamiRoute 1-Easy.prefab"): resuelve el que haya con GetComponent.
/// </summary>
public class TutorialPromptVisual : MonoBehaviour
{
    [Header("Variante teclado/mouse (la que existe hoy)")]
    [SerializeField, Tooltip("Sprite de reposo de la variante teclado/mouse (WASD, SPACE, click). Es lo que se ve si no hay animacion asignada.")]
    private Sprite _spriteTeclado;

    [SerializeField, Tooltip("Animator Controller (flipbook por sprites) de la variante teclado/mouse. Vacio = se queda quieto en el sprite de reposo de arriba.")]
    private RuntimeAnimatorController _animatorTeclado;

    [Header("Variante joystick (arranca vacia: el arte todavia no existe)")]
    [SerializeField, Tooltip("Sprite de reposo de la variante joystick (stick izquierdo / joystick+A). VACIO a proposito hasta que Valentino entregue el arte: mientras tanto se sigue mostrando la variante de teclado.")]
    private Sprite _spriteJoystick;

    [SerializeField, Tooltip("Animator Controller (flipbook) de la variante joystick. VACIO a proposito hasta que Valentino entregue el arte.")]
    private RuntimeAnimatorController _animatorJoystick;

    SpriteRenderer _spriteRenderer;
    Image _image;
    Animator _animator;
    bool _yaAvisoFaltaDeArte;

    void Awake()
    {
        //el mismo componente sirve para el caso mundo (SpriteRenderer) y el caso UI (Image):
        //cada objeto real solo tiene uno de los dos, asi que null-check y usamos el que aparezca
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _image = GetComponent<Image>();
        _animator = GetComponent<Animator>();

        if (_spriteRenderer == null && _image == null)
        {
            Debug.LogWarning($"[TutorialPromptVisual] '{name}' no tiene SpriteRenderer ni Image: no hay donde dibujar el prompt.");
        }
    }

    void OnEnable()
    {
        //aplicar ya con el device actual: el objeto puede activarse cuando el jugador
        //ya esta jugando con joystick, no solo cuando cambia de device en caliente
        AplicarVariante(InputHub.UltimoDeviceFueJoystick);
        InputHub.OnDeviceCambio += HandleDeviceCambio;
    }

    void OnDisable()
    {
        //OnDeviceCambio es un evento ESTATICO: sin desuscribirse quedan referencias
        //colgadas a objetos ya destruidos (y NullReferenceException en el proximo cambio)
        InputHub.OnDeviceCambio -= HandleDeviceCambio;
    }

    void HandleDeviceCambio()
    {
        AplicarVariante(InputHub.UltimoDeviceFueJoystick);
    }

    void AplicarVariante(bool deviceEsJoystick)
    {
        if (_spriteRenderer == null && _image == null)
        {
            return; //ya se aviso en Awake, no hay donde dibujar nada
        }

        bool hayVarianteJoystick = _spriteJoystick != null || _animatorJoystick != null;
        bool usarJoystick = deviceEsJoystick && hayVarianteJoystick;

        if (deviceEsJoystick && !hayVarianteJoystick)
        {
            //fallback correcto: seguir mostrando teclado en vez de dejar el prompt en blanco
            AvisarFaltaDeArteUnaVez();
        }

        Sprite spriteAMostrar = usarJoystick ? _spriteJoystick : _spriteTeclado;
        RuntimeAnimatorController controladorAMostrar = usarJoystick ? _animatorJoystick : _animatorTeclado;

        //el Animator (si hay) pisa el sprite cuadro a cuadro apenas tiene controller asignado;
        //sin controller, lo unico que se ve es el sprite estatico de abajo
        if (_animator != null)
        {
            _animator.runtimeAnimatorController = controladorAMostrar;
        }

        if (spriteAMostrar != null)
        {
            AsignarSprite(spriteAMostrar);
        }
    }

    void AsignarSprite(Sprite sprite)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = sprite;
        }
        else if (_image != null)
        {
            _image.sprite = sprite;
        }
    }

    void AvisarFaltaDeArteUnaVez()
    {
        if (_yaAvisoFaltaDeArte)
        {
            return;
        }
        _yaAvisoFaltaDeArte = true;
        Debug.LogWarning($"[TutorialPromptVisual] '{name}' no tiene variante de joystick cargada (sprite/animator vacios): sigo mostrando la de teclado como fallback. Falta cargar el arte de joystick en este objeto.");
    }
}
