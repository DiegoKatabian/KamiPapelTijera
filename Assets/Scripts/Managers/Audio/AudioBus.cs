/// <summary>
/// Mixer routing groups. Each SoundEntry declares one; AudioBank maps each to an
/// AudioMixerGroup. A bus with no group assigned yet falls back to code-side volume, so the
/// mixer can be created after all the code lands (see the design doc, section 8).
/// </summary>
public enum AudioBus
{
    Music,
    SFX,
    UI,
    Ambience
}
