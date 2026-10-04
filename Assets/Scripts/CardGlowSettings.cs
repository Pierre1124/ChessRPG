using UnityEngine;

/// <summary>保存本機手牌光暈偏好，與出牌資格及網路規則分離。</summary>
public static class CardGlowSettings
{
    public const string StyleKey = "CardGlow.Style";
    public const string ColorKey = "CardGlow.Color";
    public const string IntensityKey = "CardGlow.Intensity";
    private static bool loaded;
    private static int style;
    private static int palette;
    private static float intensity;
    public static int Revision { get; private set; }
    public static int Style { get { EnsureLoaded(); return style; } }
    public static int Palette { get { EnsureLoaded(); return palette; } }
    public static float Intensity { get { EnsureLoaded(); return intensity; } }
    public static Color Tint
    {
        get
        {
            EnsureLoaded();
            switch (palette)
            {
                case 1: return new Color(1f, 0.76f, 0.25f);
                case 2: return new Color(0.3f, 0.7f, 1f);
                case 3: return new Color(0.8f, 0.5f, 1f);
                default: return new Color(0.15f, 0.92f, 0.72f);
            }
        }
    }

    /// <summary>即使編輯器停用網域重載，每次啟動仍重新讀取偏好。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() { loaded = false; Revision++; }

    /// <summary>首次使用時讀取並限制存檔數值，避免損壞設定影響渲染。</summary>
    private static void EnsureLoaded()
    {
        if (loaded) return;
        style = Mathf.Clamp(PlayerPrefs.GetInt(StyleKey, 0), 0, 3);
        palette = Mathf.Clamp(PlayerPrefs.GetInt(ColorKey, 0), 0, 3);
        intensity = Mathf.Clamp(PlayerPrefs.GetFloat(IntensityKey, 0.7f), 0.2f, 1f);
        if (float.IsNaN(intensity)) intensity = 0.7f;
        loaded = true;
    }

    /// <summary>立即更新光暈並標記偏好；由設定關閉時統一寫入磁碟。</summary>
    public static void Set(int newStyle, int newPalette, float newIntensity)
    {
        loaded = true;
        style = Mathf.Clamp(newStyle, 0, 3);
        palette = Mathf.Clamp(newPalette, 0, 3);
        intensity = float.IsNaN(newIntensity) ? 0.7f : Mathf.Clamp(newIntensity, 0.2f, 1f);
        PlayerPrefs.SetInt(StyleKey, style);
        PlayerPrefs.SetInt(ColorKey, palette);
        PlayerPrefs.SetFloat(IntensityKey, intensity);
        Revision++;
    }

    /// <summary>只重設光暈外觀，不動牌組、音量與畫面設定。</summary>
    public static void ResetDefaults() { Set(0, 0, 0.7f); }
}
