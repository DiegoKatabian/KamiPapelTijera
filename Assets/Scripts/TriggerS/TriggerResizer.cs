using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerResizer : TriggerScript
{
    [SerializeField] Vector3 originalScale = Vector3.one;
    [SerializeField] Vector3 targetScale = Vector3.one;
    [SerializeField] float lerpDuration = 1f;
    [SerializeField] Transform targetTransform;
    public override void OnEnterBehaviour(Collider other)
    {
        base.OnEnterBehaviour(other);
        StartCoroutine(Resize(targetTransform.localScale, targetScale));
    }

    public override void OnExitBehaviour()
    {
        base.OnExitBehaviour();

        //the exit also runs from OnDisable (its page switching off), where a coroutine can't start
        if (!isActiveAndEnabled)
        {
            targetTransform.localScale = originalScale;
            return;
        }
        StartCoroutine(Resize(targetTransform.localScale, originalScale));
    }

    IEnumerator Resize(Vector3 currentScale, Vector3 finalScale)
    {
        float t = 0f;
        while (t < lerpDuration)
        {
            t += Time.deltaTime;
            targetTransform.localScale = Vector3.Lerp(currentScale, finalScale, t/lerpDuration);
            yield return null;
        }
    }
}
