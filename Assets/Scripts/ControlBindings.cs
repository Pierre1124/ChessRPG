using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameControl { InspectPiece, RotateCamera, ResetCamera }

/// <summary>本機操作設定；左鍵及 Esc 保留給棋盤、UI 與取消，避免互相搶輸入。</summary>
public static class ControlBindings
{
    private static readonly int[] Defaults = { (int)Key.I, -1, (int)Key.Space };
    public static bool SettingsOpen;
    public static bool Capturing;
    public static int CaptureFinishedFrame = -1;
    public static bool SuppressGameplay => SettingsOpen || Capturing || CaptureFinishedFrame == Time.frameCount;
    public static bool LongPressEnabled => PlayerPrefs.GetInt("Controls.LongPress", 0) == 1;

    /// <summary>讀取有效按鍵，損壞或舊版非法設定回到預設。</summary>
    public static int Get(GameControl action)
    {
        int value = PlayerPrefs.GetInt("Controls." + action, Defaults[(int)action]);
        return IsAllowed(value) ? value : Defaults[(int)action];
    }

    /// <summary>允許鍵盤與右／中／側鍵，保留 Escape 及左鍵。</summary>
    public static bool IsAllowed(int code) => (code >= -4 && code <= -1) ||
        (code > 0 && Enum.IsDefined(typeof(Key), code) && code != (int)Key.Escape);

    /// <summary>拒絕重複綁定並即時持久化。</summary>
    public static bool TrySet(GameControl action, int code, out string reason)
    {
        reason = "";
        if (!IsAllowed(code)) { reason = "左鍵及 Esc 為保留操作，請選其他按鍵"; return false; }
        foreach (GameControl other in Enum.GetValues(typeof(GameControl)))
            if (other != action && Get(other) == code) { reason = "此按鍵已用於「" + ActionName(other) + "」"; return false; }
        PlayerPrefs.SetInt("Controls." + action, code); PlayerPrefs.Save(); return true;
    }

    /// <summary>顯示功能名稱。</summary>
    public static string ActionName(GameControl action) => action == GameControl.InspectPiece ? "查看棋子資訊" :
        action == GameControl.RotateCamera ? "按住旋轉視角" : "重設視角";

    /// <summary>顯示設定中的按鍵名稱。</summary>
    public static string Label(GameControl action)
    {
        int code = Get(action);
        return code == -1 ? "滑鼠右鍵" : code == -2 ? "滑鼠中鍵" : code == -3 ? "滑鼠側鍵 1" :
            code == -4 ? "滑鼠側鍵 2" : ((Key)code).ToString();
    }

    /// <summary>讀取按鍵狀態，同時支援按下瞬間及持續按住。</summary>
    public static bool Read(GameControl action, bool held = false)
    {
        if (SuppressGameplay) return false;
        int code = Get(action);
        UnityEngine.InputSystem.Controls.ButtonControl button = null;
        if (code > 0 && Keyboard.current != null) button = Keyboard.current[(Key)code];
        else if (Mouse.current != null)
            button = code == -1 ? Mouse.current.rightButton : code == -2 ? Mouse.current.middleButton :
                code == -3 ? Mouse.current.backButton : Mouse.current.forwardButton;
        return button != null && (held ? button.isPressed : button.wasPressedThisFrame);
    }

    /// <summary>設定長按備用操作。</summary>
    public static void SetLongPress(bool enabled)
    { PlayerPrefs.SetInt("Controls.LongPress", enabled ? 1 : 0); PlayerPrefs.Save(); }

    /// <summary>僅還原操作設定，不改動音量、光暈與顯示。</summary>
    public static void ResetDefaults()
    {
        foreach (GameControl action in Enum.GetValues(typeof(GameControl))) PlayerPrefs.DeleteKey("Controls." + action);
        PlayerPrefs.DeleteKey("Controls.LongPress"); PlayerPrefs.Save();
    }
}
