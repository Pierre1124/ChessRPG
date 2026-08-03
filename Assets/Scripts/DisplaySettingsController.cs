using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class DisplaySettingsController
{
    public const string ResolutionIndexKey = "ResolutionIndex";
    public const string WindowedKey = "WindowedMode";

    public static Resolution[] BuildResolutionOptions()
    {
        return Screen.resolutions
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
    }

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
        dropdown.value = Mathf.Clamp(
            PlayerPrefs.GetInt(
                ResolutionIndexKey,
                GetCurrentResolutionIndex(resolutions)
            ),
            0,
            Mathf.Max(0, resolutions.Length - 1)
        );
        dropdown.RefreshShownValue();

        return resolutions;
    }

    public static void ApplySavedSettings()
    {
        Resolution[] resolutions = BuildResolutionOptions();
        int index = Mathf.Clamp(
            PlayerPrefs.GetInt(
                ResolutionIndexKey,
                GetCurrentResolutionIndex(resolutions)
            ),
            0,
            Mathf.Max(0, resolutions.Length - 1)
        );

        ApplyResolution(index, resolutions, IsWindowed(), false);
    }

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
            PlayerPrefs.Save();
        }

        Debug.Log(
            $"[DisplaySettings] Resolution={resolution.width}x" +
            $"{resolution.height} | Windowed={windowed}"
        );
    }

    public static void BindWindowedToggle(Toggle toggle)
    {
        if (toggle != null)
        {
            toggle.isOn = IsWindowed();
        }
    }

    public static bool IsWindowed()
    {
        return PlayerPrefs.GetInt(
            WindowedKey,
            Screen.fullScreen ? 0 : 1
        ) == 1;
    }

    private static int GetCurrentResolutionIndex(Resolution[] resolutions)
    {
        if (resolutions == null || resolutions.Length == 0)
        {
            return 0;
        }

        for (int i = 0; i < resolutions.Length; i++)
        {
            if (
                resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height
            )
            {
                return i;
            }
        }

        return 0;
    }
}
