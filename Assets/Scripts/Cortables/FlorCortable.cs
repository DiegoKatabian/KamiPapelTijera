using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlorCortable : PickupCortable
{
    protected override void ApplyCut()
    {
        base.ApplyCut();
        AudioManager.instance.Play(AudioId.MagicSuccess, 2f);
    }
}
