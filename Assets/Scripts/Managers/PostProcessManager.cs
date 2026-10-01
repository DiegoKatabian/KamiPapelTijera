using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostProcessManager : Singleton<PostProcessManager>
{
    [SerializeField] Volume bloomVolume;

    [Header("Bloom Intensity on Page Change")]
    [SerializeField] float bloomLerpTime = 3;
    [SerializeField] float targetBloomIntensity = 10f;
    float baseBloom;

    ColorAdjustments colorAdjustment;
    Bloom bloom;

    private void Start()
    {
        ColorAdjustments colorAdj;
        Bloom bloomAdj;

        if (bloomVolume.profile.TryGet<ColorAdjustments>(out colorAdj))
        {
            //Debug.Log("got color adjustment");
            colorAdjustment = colorAdj;
        }

        if (bloomVolume.profile.TryGet<Bloom>(out bloomAdj))
        {
            //Debug.Log("got bloom");
            bloom = bloomAdj;
            baseBloom = bloom.intensity.value;
        }
    }

    public void SetBrightnessValue(float value)
    {
        //colorGrading.postExposure.value = value;
        colorAdjustment.postExposure.value = value; 
    }

    public void SetContrastValue(float value)
    {
        //colorGrading.contrast.value = value;
        colorAdjustment.contrast.value = value;
    }

    public IEnumerator LerpBloomIntensity()
    {
        float t = 0;
        while (t < bloomLerpTime)
        {
            bloom.intensity.value = Mathf.Lerp(baseBloom, targetBloomIntensity, t);
            t += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
        t = 0;
        while (t < bloomLerpTime)
        {
            bloom.intensity.value = Mathf.Lerp(targetBloomIntensity, baseBloom, t);
            t += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
    }

}
