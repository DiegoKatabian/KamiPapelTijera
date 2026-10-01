using UnityEngine;

// A surface that carries whoever stands on it (a car's roof today). The rider asks for the velocity and
// adds it inside its own single move; it never reparents onto the surface.
public interface IMovingGround
{
    // World-space velocity of the surface, in units/second. Zero when it should not carry anyone.
    // Implementers must answer from cached fields only: the object behind this interface may already
    // be Destroy()ed, and touching `transform` then throws.
    Vector3 GroundVelocity { get; }
}
