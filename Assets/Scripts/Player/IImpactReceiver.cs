using UnityEngine;

// What a hazard (today: a car) hands to whatever it hits. The hazard fills this from its own Inspector
// fields; the receiver decides what to do with it. Neither side knows the other's concrete type.
public struct ImpactInfo
{
    public float damage;
    public Vector3 pushDirection;   // unit vector, XZ plane
    public float pushDistance;      // total displacement of the knockback, in units
    public float pushDuration;      // seconds the knockback lasts
    public float immunitySeconds;   // no further impacts for this long
    public DeathCause cause;
}

public interface IImpactReceiver
{
    void ReceiveImpact(ImpactInfo impact);
}
