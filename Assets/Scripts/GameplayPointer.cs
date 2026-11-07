using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One shared pointer snapshot per frame. A second finger cancels the gesture.</summary>
public static class GameplayPointer
{
    private static int frame = -1;
    private static bool suppressTouch;
    private static bool available, touch, pressed, held, released, canceled;
    private static Vector2 position;
    public static bool Available { get { Read(); return available; } }
    public static bool IsTouch { get { Read(); return touch; } }
    public static Vector2 Position { get { Read(); return position; } }
    public static bool Pressed { get { Read(); return pressed; } }
    public static bool Held { get { Read(); return held; } }
    public static bool Released { get { Read(); return released; } }
    public static bool Canceled { get { Read(); return canceled; } }
    public static float TapSlop => IsTouch ? Mathf.Max(20f, Screen.dpi > 0 ? Screen.dpi * 0.12f : 32f) : 12f;
    public static bool CancelPressed => (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
        (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { frame = -1; suppressTouch = false; }

    private static void Read()
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        pressed = held = released = canceled = false;
        var screen = Touchscreen.current;
        var primary = screen != null ? screen.primaryTouch : null;
        touch = primary != null && (Application.isMobilePlatform || primary.press.isPressed ||
            primary.press.wasPressedThisFrame || primary.press.wasReleasedThisFrame || suppressTouch);
        available = touch || Mouse.current != null;
        if (touch)
        {
            position = primary.position.ReadValue();
            int fingers = 0;
            foreach (var finger in screen.touches) if (finger.press.isPressed) fingers++;
            if (fingers > 1 || primary.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
                suppressTouch = true;
            if (suppressTouch)
            {
                canceled = true;
                if (fingers == 0) suppressTouch = false;
                return;
            }
            pressed = primary.press.wasPressedThisFrame;
            held = primary.press.isPressed;
            released = primary.press.wasReleasedThisFrame;
        }
        else if (Mouse.current != null)
        {
            position = Mouse.current.position.ReadValue();
            pressed = Mouse.current.leftButton.wasPressedThisFrame;
            held = Mouse.current.leftButton.isPressed;
            released = Mouse.current.leftButton.wasReleasedThisFrame;
        }
    }
}
