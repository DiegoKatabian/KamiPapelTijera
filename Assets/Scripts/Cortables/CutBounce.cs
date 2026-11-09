using UnityEngine;

/// <summary>
/// What the normal scissors do against something only the sharpened pair cuts (spec 024 3.A, the Rocoso and the
/// cuttable stones): sparks on the surface on Kami's side and her take-hit, no damage. EVA playtest: players didn't
/// understand why the cut did nothing, so the bounce has to be seen.
/// </summary>
public static class CutBounce
{
    public static void Play(Component what, ParticleSystem sparksPrefab, float sparksLifetime, bool kamiReacts)
    {
        Player kami = LevelManager.Instance != null ? LevelManager.Instance.player : null;
        if (sparksPrefab != null)
        {
            Vector3 at = what.transform.position;
            Collider own = what.GetComponent<Collider>();
            if (own != null && kami != null)
            {
                //on its surface, on Kami's side, at her chest's height
                at = own.ClosestPoint(kami.transform.position + Vector3.up * 2f);
            }
            ParticleSystem sparks = Object.Instantiate(sparksPrefab, at, Quaternion.identity);
            sparks.Play();
            Object.Destroy(sparks.gameObject, sparksLifetime);
        }
        else
        {
            Debug.LogWarning($"[CutBounce] {what.name}: no sparks prefab assigned, the bounce shows no sparks");
        }

        if (kamiReacts && kami != null)
        {
            kami.PlayCutBounceReaction();
        }
    }
}
