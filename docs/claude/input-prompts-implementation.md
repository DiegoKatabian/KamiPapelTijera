# Dynamic Input Prompts: Implementation Guide

**Purpose**: Show animated gamepad button icons in UI instead of hardcoded "Press E" text. Supports both keyboard and gamepad simultaneously, with graceful fallback.

**Status**: Reference for `feature/joystick-controls` (issue #41)

---

## Architecture Overview

```
DialogueManager / TooltipManager (displays text)
         ↓
DialogueTextProcessor (processes string before display)
         ↓
InputPromptSystem (detects active device, returns icon or text)
         ↓
TextMesh Pro Rich Text Tags (renders sprite icons inline)
         ↓
Sprite Atlas (gamepad button icons: A, B, X, Y, LB, RB, Start, D-pad, sticks)
```

## Step 1: Input Detection (`InputPromptSystem.cs`)

Singleton that detects which input device is active.

```csharp
public enum InputDevice { Keyboard, Gamepad, Both }

public class InputPromptSystem : MonoBehaviour
{
    public static InputPromptSystem Instance { get; private set; }
    
    private InputDevice activeDevice = InputDevice.Keyboard;
    private float gamepadCheckInterval = 0.1f;
    private float timeSinceLastCheck = 0f;
    
    // Cache for performance
    private Dictionary<string, string> promptCache = new();
    
    void Update()
    {
        timeSinceLastCheck += Time.deltaTime;
        if (timeSinceLastCheck >= gamepadCheckInterval)
        {
            DetectInputDevice();
            timeSinceLastCheck = 0f;
        }
    }
    
    private void DetectInputDevice()
    {
        bool hasGamepad = Gamepad.current != null && Gamepad.current.enabled;
        bool hasKeyboard = Keyboard.current != null && Keyboard.current.enabled;
        
        // Detect most recent active input (check recent axis/button reads)
        // For now: if gamepad exists, prefer it; otherwise keyboard
        activeDevice = hasGamepad ? InputDevice.Gamepad : InputDevice.Keyboard;
    }
    
    /// <summary>
    /// Get prompt text/icon for an input action.
    /// Returns: "E" (keyboard), "<sprite name=button_A>" (gamepad), or "E / [A]" (both)
    /// </summary>
    public string GetPromptText(InputAction action)
    {
        string cacheKey = $"{action}_{activeDevice}";
        if (promptCache.TryGetValue(cacheKey, out var cached))
            return cached;
        
        string result = activeDevice switch
        {
            InputDevice.Keyboard => GetKeyboardPrompt(action),
            InputDevice.Gamepad => GetGamepadPrompt(action),
            InputDevice.Both => GetCoexistencePrompt(action),
            _ => ""
        };
        
        promptCache[cacheKey] = result;
        return result;
    }
    
    private string GetKeyboardPrompt(InputAction action) => action switch
    {
        InputAction.Action => "E",
        InputAction.Jump => "Space",
        InputAction.Attack => "Click",
        InputAction.Sprint => "Shift",
        _ => ""
    };
    
    private string GetGamepadPrompt(InputAction action) => action switch
    {
        InputAction.Action => "<sprite name=button_B>", // B = red (context: attack or interact)
        InputAction.Jump => "<sprite name=button_B>",     // B = green
        InputAction.Attack => "<sprite name=button_X>",   // X = blue
        InputAction.Sprint => "<sprite name=button_LB>",  // L1 = white/gray
        _ => ""
    };
    
    private string GetCoexistencePrompt(InputAction action) => action switch
    {
        InputAction.Action => $"{GetKeyboardPrompt(action)} / {GetGamepadPrompt(action)}",
        InputAction.Jump => $"{GetKeyboardPrompt(action)} / {GetGamepadPrompt(action)}",
        InputAction.Attack => $"{GetKeyboardPrompt(action)} / {GetGamepadPrompt(action)}",
        InputAction.Sprint => $"{GetKeyboardPrompt(action)} / {GetGamepadPrompt(action)}",
        _ => ""
    };
}

public enum InputAction { Interact, Jump, Attack, Sprint }
```

## Step 2: String Processing (`DialogueTextProcessor.cs`)

Takes localized strings with `{INPUT:*}` placeholders and replaces them with icons.

```csharp
public class DialogueTextProcessor : MonoBehaviour
{
    public static string ProcessInputPlaceholders(string rawText)
    {
        // Replace {INPUT:action} → icon/text via InputPromptSystem
        rawText = Regex.Replace(rawText, @"\{INPUT:interact\}", 
            InputPromptSystem.Instance.GetPromptText(InputAction.Action));
        
        rawText = Regex.Replace(rawText, @"\{INPUT:jump\}", 
            InputPromptSystem.Instance.GetPromptText(InputAction.Jump));
        
        rawText = Regex.Replace(rawText, @"\{INPUT:attack\}", 
            InputPromptSystem.Instance.GetPromptText(InputAction.Attack));
        
        rawText = Regex.Replace(rawText, @"\{INPUT:sprint\}", 
            InputPromptSystem.Instance.GetPromptText(InputAction.Sprint));
        
        return rawText;
    }
}
```

## Step 3: Localization Strings (Example)

In `DialogueTable_en.asset` (Unity Localization):

```yaml
# OLD (hardcoded keyboard)
InteractPrompt:
  en: "Press E to talk"

# NEW (input-aware)
InteractPrompt:
  en: "Press {INPUT:action} to talk"

JumpTutorial:
  en: "Jump with {INPUT:jump} to reach high places"

AttackTutorial:
  en: "{INPUT:action} to cut the paper"

CameraHelp:
  en: "Press {INPUT:camera} to cycle camera"
```

At runtime:
- Keyboard active: "Press E to talk" → "Press E to talk"
- Gamepad active: "Press E to talk" → "Press [B icon] to talk"
- Both: "Press E to talk" → "Press E / [B icon] to talk"

## Step 4: TextMesh Pro Rich Text Tags

TMP supports inline sprite rendering:

```
Raw: "Press <sprite name=button_A> to continue"
Rendered: "Press [yellow Y icon] to continue"
```

**Setup**:
1. Create or import gamepad button sprite atlas (32×32 px per button recommended)
2. In TextMesh Pro Material, add sprite atlas as `Sprite Asset`
3. Name sprites consistently: `button_A`, `button_B`, `button_X`, `button_A`, `button_LB`, `button_RB`, `button_Start`, `button_Dpad_Up`, etc.
4. TMP automatically recognizes `<sprite name=button_A>` tags and renders them inline

## Step 5: Sprite Animation (Optional)

Simple pulse/bounce animation on button icons when they first appear.

```csharp
// In DialogueManager.WriteText() or PostIt.Show()
IEnumerator WriteTextWithAnimatedIcons(TMP_Text textComponent, string text)
{
    // Process placeholders first
    string processed = DialogueTextProcessor.ProcessInputPlaceholders(text);
    
    // Set text (TMP renders all sprites)
    textComponent.text = processed;
    
    // Animate all sprites in text (optional)
    foreach (var spriteInfo in textComponent.spriteAnimationID)
    {
        StartCoroutine(PulseSprite(textComponent, spriteInfo));
    }
    
    // Continue with character-by-character reveal if desired
    yield return StartCoroutine(RevealCharacterByCharacter(textComponent));
}

IEnumerator PulseSprite(TMP_Text textComponent, int spriteIndex)
{
    float elapsed = 0f;
    float pulseDuration = 0.6f;
    
    while (elapsed < pulseDuration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / pulseDuration;
        float scale = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.1f; // Pulse 0.9 to 1.1
        
        // Scale the sprite via TMP material property (advanced)
        // Or use simpler visual: just fade in slightly
        
        yield return null;
    }
}
```

**Simpler approach**: Just use TMP's built-in `<scale>` tag:

```csharp
// Manually wrap sprites in animation tags
rawText = rawText.Replace(
    "<sprite name=button_A>",
    "<sprite name=button_A><scale=1.15><anim=1><scale=1.0>" // Pulse effect
);
```

## Step 6: Graceful Fallback

If gamepad icon sprite is missing or not loaded:

```csharp
private string GetGamepadPrompt(InputAction action)
{
    // Check if sprite asset loaded
    if (spriteAsset == null)
    {
        Debug.LogWarning("[InputPromptSystem] Sprite asset not loaded, falling back to text");
        return GetGamepadPromptFallback(action); // Returns "Y", "B", etc.
    }
    
    return action switch
    {
        InputAction.Jump => "<sprite name=button_A>",
        InputAction.Action => "<sprite name=button_B>", // action (attack/interact)
        InputAction.Sprint => "<sprite name=button_LB>",
        InputAction.Camera => "<sprite name=button_L2>",
        // ... etc
    };
}

private string GetGamepadPromptFallback(InputAction action) => action switch
{
    InputAction.Jump => "A",
    InputAction.Action => "B",     // action (attack/interact)
    InputAction.Sprint => "L1",
    InputAction.Camera => "L2",
    _ => ""
};
```

## Step 7: Integration Points

### DialogueManager
```csharp
// Before displaying dialogue text:
public void SetDialogueText(string rawText)
{
    string processed = DialogueTextProcessor.ProcessInputPlaceholders(rawText);
    dialogueTextComponent.text = processed;
}
```

### TooltipManager / PostIt
```csharp
// In PostIt.Show():
public void Show(string tooltipKey, PostItColor color)
{
    string rawText = tooltipTable.GetEntry(tooltipKey).GetLocalizedString();
    string processed = DialogueTextProcessor.ProcessInputPlaceholders(rawText);
    textComponent.text = processed;
    
    // Play fade-in animation
    StartCoroutine(FadeIn());
}
```

### PlayerPageSpawnManager / Tutorial Text
```csharp
// Any on-screen hint/tutorial:
string hint = "Jump with {INPUT:jump} to reach the roof!";
tutorialText.text = DialogueTextProcessor.ProcessInputPlaceholders(hint);
```

## Sprite Atlas Creation (Quick Recipe)

1. **Design**: Create 512×512 px sprite sheet with:
   - Xbox button colors (A=green, B=red, X=blue, Y=yellow)
   - 32×32 px each button (allows 4×4 grid per quadrant)
   - Include: A, B, X, Y, LB, RB, Start, Select, D-Pad (up/down/left/right), L-Stick, R-Stick

2. **Import in Unity**:
   - Set `Texture Type` → Sprite (2D and UI)
   - Set `Sprite Mode` → Multiple
   - Click `Sprite Editor` → Slice → Grid by Cell Size (32×32)
   - Auto-name: `button_A`, `button_B`, etc. (or rename in slicing dialog)

3. **Register in TMP**:
   - Create TMP Sprite Asset (Right-click → Create → Text Mesh Pro → Sprite Asset)
   - Assign your sprite sheet
   - Sprites should auto-populate with names

4. **Verify**:
   ```
   Write in a TMP text: "Try this: <sprite name=button_A>"
   Should render with the green A button icon inline.
   ```

## Testing Checklist

- [ ] Gamepad connected: UI shows "[Y icon]" instead of "E"
- [ ] Gamepad disconnected: UI reverts to "E" without error
- [ ] Both connected: UI shows "E / [Y icon]"
- [ ] Sprites render inline in dialogue and tooltips
- [ ] Sprites animate (pulse/bounce) when first appear
- [ ] Missing sprite gracefully falls back to text ("Y")
- [ ] Localization strings with `{INPUT:*}` work in all 3 languages (es/en/pt)
- [ ] No console spam on input device changes

## Performance Notes

- **Caching**: `InputPromptSystem` caches prompt strings per active device to avoid regex/string work every frame.
- **Update frequency**: Device detection happens every 0.1s (not every frame) to avoid polling overhead.
- **TMP Rich Text**: `<sprite>` tags are fast; TMP meshes these inline. No performance concern unless 1000+ sprites per text (unlikely).

## Future Enhancements

- **Smart coexistence display**: "E / [A]" could be prettier as side-by-side icons without text
- **Controller detection per player**: if multiplayer, detect Gamepad 1 vs. Gamepad 2
- **Vibration prompts**: show "[rumble icon]" for haptic feedback hints
- **Keyboard alternative text**: some players prefer "Press [letter]" over just "[E]" — make configurable per localization table
