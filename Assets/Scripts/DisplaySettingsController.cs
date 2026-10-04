using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class DisplaySettingsController
{
    public const string ResolutionIndexKey = "ResolutionIndex";
    public const string WindowedKey = "WindowedMode";
    public const string WidthKey = "DisplayWidth";
    public const string HeightKey = "DisplayHeight";

    /// <summary>
    /// 建立解析度選項與對應的顯示文字。
    /// </summary>
    public static Resolution[] BuildResolutionOptions()
    {
        Resolution[] options = Screen.resolutions
            .GroupBy(resolution => new
            {
                resolution.width,
                resolution.height
            })
            .Select(group => group
                .OrderByDescending(resolution =>
                    resolution.refreshRateRatio.value)
                .First())
            .OrderBy(resolution => resolution.width)
            .ThenBy(resolution => resolution.height)
            .ToArray();
        return options.Length > 0 ? options : new[] { new Resolution { width = Mathf.Max(640, Screen.width), height = Mathf.Max(480, Screen.height) } };
    }

    /// <summary>
    /// 將可用解析度填入下拉選單並選取目前設定。
    /// </summary>
    public static Resolution[] PopulateResolutionDropdown(
        TMP_Dropdown dropdown
    )
    {
        Resolution[] resolutions = BuildResolutionOptions();
        if (dropdown == null)
        {
            return resolutions;
        }

        List<string> options = new List<string>();
        for (int i = 0; i < resolutions.Length; i++)
        {
            options.Add($"{resolutions[i].width}x{resolutions[i].height}");
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(GetSavedResolutionIndex(resolutions));
        dropdown.RefreshShownValue();

        return resolutions;
    }

    /// <summary>
    /// 讀取並套用已儲存的畫面設定。
    /// </summary>
    public static void ApplySavedSettings()
    {
        Resolution[] resolutions = BuildResolutionOptions();
        int index = GetSavedResolutionIndex(resolutions);

        ApplyResolution(index, resolutions, IsWindowed(), false);
    }

    /// <summary>
    /// 套用指定解析度與視窗模式，依參數決定是否保存。
    /// </summary>
    public static void ApplyResolution(
        int index,
        Resolution[] resolutions,
        bool windowed,
        bool save
    )
    {
        if (resolutions == null || resolutions.Length == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, resolutions.Length - 1);
        Resolution resolution = resolutions[index];
        Screen.SetResolution(
            resolution.width,
            resolution.height,
            windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow
        );

        if (save)
        {
            PlayerPrefs.SetInt(ResolutionIndexKey, index);
            PlayerPrefs.SetInt(WindowedKey, windowed ? 1 : 0);
            PlayerPrefs.SetInt(WidthKey, resolution.width);
            PlayerPrefs.SetInt(HeightKey, resolution.height);
            PlayerPrefs.Save();
        }

        Debug.Log(
            $"[DisplaySettings] Resolution={resolution.width}x" +
            $"{resolution.height} | Windowed={windowed}"
        );
    }

    /// <summary>
    /// 綁定視窗模式切換控制項。
    /// </summary>
    public static void BindWindowedToggle(Toggle toggle)
    {
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(IsWindowed());
        }
    }

    /// <summary>
    /// 取得目前設定是否使用視窗模式。
    /// </summary>
    public static bool IsWindowed()
    {
        return PlayerPrefs.GetInt(
            WindowedKey,
            Screen.fullScreen ? 0 : 1
        ) == 1;
    }

    /// <summary>
    /// 找出目前解析度在選項清單中的索引。
    /// </summary>
    private static int GetCurrentResolutionIndex(Resolution[] resolutions)
    {
        if (resolutions == null || resolutions.Length == 0)
        {
            return 0;
        }

        for (int i = 0; i < resolutions.Length; i++)
        {
            if (
                resolutions[i].width == Screen.width &&
                resolutions[i].height == Screen.height
            )
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>優先以尺寸還原設定，避免更換螢幕後相同索引代表不同解析度。</summary>
    public static int GetSavedResolutionIndex(Resolution[] resolutions)
    {
        if (resolutions == null || resolutions.Length == 0) return 0;
        if (PlayerPrefs.HasKey(WidthKey) && PlayerPrefs.HasKey(HeightKey))
        {
            int width = PlayerPrefs.GetInt(WidthKey), height = PlayerPrefs.GetInt(HeightKey);
            for (int i = 0; i < resolutions.Length; i++)
                if (resolutions[i].width == width && resolutions[i].height == height) return i;
            return GetCurrentResolutionIndex(resolutions);
        }
        return Mathf.Clamp(PlayerPrefs.GetInt(ResolutionIndexKey, GetCurrentResolutionIndex(resolutions)), 0, resolutions.Length - 1);
    }
}
