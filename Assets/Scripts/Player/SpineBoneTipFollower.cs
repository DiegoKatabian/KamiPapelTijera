using Spine;
using Spine.Unity;
using UnityEngine;

//sigue un punto a lo largo de un hueso del skeleton de spine, en coordenadas de mundo.
//lo usamos para que el trail quede pegado a la punta de la tijera de spine (hueso tijera_front3).
//como setea position en world space, no importa de quien sea hijo el gameobject.
public class SpineBoneTipFollower : MonoBehaviour
{
    public SkeletonAnimation skeletonAnimation;
    public string boneName = "tijera_front3";
    [Range(0f, 1f)] public float alongBone = 1f; //0 = base del hueso, 1 = punta (usa el length del hueso)

    Bone _bone;
    bool _warned;

    public void Init(SkeletonAnimation skeleton, string bone)
    {
        skeletonAnimation = skeleton;
        boneName = bone;
        _bone = null;
        _warned = false;
    }

    void LateUpdate() //late para correr despues de que spine actualizo el skeleton
    {
        if (skeletonAnimation == null)
        {
            return;
        }

        if (_bone == null)
        {
            _bone = skeletonAnimation.Skeleton.FindBone(boneName);
            if (_bone == null)
            {
                if (!_warned)
                {
                    Debug.LogWarning($"[SpineBoneTipFollower] no encontre el hueso '{boneName}' en el skeleton", this);
                    _warned = true;
                }
                return;
            }
            Debug.Log($"[SpineBoneTipFollower] '{name}' siguiendo el hueso '{boneName}' (length {_bone.Data.Length})", this);
        }

        //punto local sobre el eje del hueso, transformado a espacio del skeleton y de ahi a mundo.
        //el flip (ScaleX = -1) ya viene incluido en la transformacion del hueso.
        float localX = _bone.Data.Length * alongBone;
        _bone.LocalToWorld(localX, 0f, out float worldX, out float worldY);
        transform.position = skeletonAnimation.transform.TransformPoint(new Vector3(worldX, worldY, 0f));
    }
}
