//la causa de muerte decide la animacion (PlayerView), el texto del defeat overlay (key de localizacion)
//y el modo de respawn (OverlayManager). agregar aca futuras causas (lava, pinches, etc).
public enum DeathCause
{
	Generic,   // fallback: cualquier daño sin causa especifica
	Drowning,  // rio: anim Drowning + respawn en lugar seguro
	Rocoso     // headbutt del rocoso: anim Death + respawn normal
}
