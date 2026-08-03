using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject panel;
    public GameObject confirmationPopup;

    [Header("Audio")]
    public Toggle soundToggle;
    public Slider volumeSlider;

    [Header("Gameplay")]
    public Toggle cameraRotationToggle;

    [Header("Display")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle windowedToggle;

    private LogicManager logicManager;
    private LayoutUI layoutUI;
    private MultiplayerGameController multiplayerGameController;
    private Resolution[] availableResolutions;

    private void Start()
    {
        logicManager = FindFirstObjectByType<LogicManager>();
        layoutUI = FindFirstObjectByType<LayoutUI>();
        multiplayerGameController =
            FindFirstObjectByType<MultiplayerGameController>();

        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(false);
        }

        LoadSettings();
        BindAudioControls();
        BindGameplayControls();
        BindDisplayControls();
    }

    private void BindAudioControls()
    {
        if (soundToggle != null && volumeSlider != null)
        {
            volumeSlider.interactable = soundToggle.isOn;
            soundToggle.onValueChanged.AddListener(isOn =>
            {
                volumeSlider.interactable = isOn;
                SaveSettings();
            });
        }

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(value =>
            {
                if (logicManager != null)
                {
                    logicManager.SetSoundVolume(value);
                }

                SaveSettings();
            });
        }
    }

    private void BindGameplayControls()
    {
        if (cameraRotationToggle == null)
        {
            return;
        }

        cameraRotationToggle.onValueChanged.AddListener(isEnabled =>
        {
            if (logicManager != null)
            {
                logicManager.ToggleCameraRotation(isEnabled);
            }

            SaveSettings();
        });
    }

    private void BindDisplayControls()
    {
        availableResolutions =
            DisplaySettingsController.PopulateResolutionDropdown(
                resolutionDropdown
            );
        DisplaySettingsController.BindWindowedToggle(windowedToggle);
        ApplyDisplaySettings(false);

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.AddListener(index =>
            {
                ApplyDisplaySettings(true);
            });
        }

        if (windowedToggle != null)
        {
            windowedToggle.onValueChanged.AddListener(isWindowed =>
            {
                ApplyDisplaySettings(true);
            });
        }
    }

    private void ApplyDisplaySettings(bool save)
    {
        int index = resolutionDropdown != null
            ? resolutionDropdown.value
            : PlayerPrefs.GetInt(
                DisplaySettingsController.ResolutionIndexKey,
                0
            );
        bool windowed = windowedToggle != null
            ? windowedToggle.isOn
            : DisplaySettingsController.IsWindowed();

        DisplaySettingsController.ApplyResolution(
            index,
            availableResolutions,
            windowed,
            save
        );
    }

    private void SaveSettings()
    {
        if (soundToggle != null)
        {
            PlayerPrefs.SetInt(
                "SoundEnabled",
                soundToggle.isOn ? 1 : 0
            );
        }

        if (volumeSlider != null)
        {
            PlayerPrefs.SetFloat("SoundVolume", volumeSlider.value);
        }

        if (resolutionDropdown != null)
        {
            PlayerPrefs.SetInt(
                DisplaySettingsController.ResolutionIndexKey,
                resolutionDropdown.value
            );
        }

        if (windowedToggle != null)
        {
            PlayerPrefs.SetInt(
                DisplaySettingsController.WindowedKey,
                windowedToggle.isOn ? 1 : 0
            );
        }

        if (cameraRotationToggle != null)
        {
            PlayerPrefs.SetInt(
                "CameraRotationEnabled",
                cameraRotationToggle.isOn ? 1 : 0
            );
        }

        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        if (soundToggle != null)
        {
            soundToggle.isOn = PlayerPrefs.GetInt("SoundEnabled", 1) == 1;
        }

        if (volumeSlider != null)
        {
            volumeSlider.value = PlayerPrefs.GetFloat("SoundVolume", 0.5f);
        }

        if (cameraRotationToggle != null)
        {
            cameraRotationToggle.isOn = PlayerPrefs.GetInt(
                "CameraRotationEnabled",
                1
            ) == 1;
        }
    }

    public void ShowPanel()
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    public void goBack()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (layoutUI != null)
        {
            layoutUI.gameObject.SetActive(true);
        }

        if (logicManager != null)
        {
            logicManager.isPromotionActive = false;
        }
    }

    public void ShowRestartConfirmation()
    {
        MultiplayerGameController controller =
            GetMultiplayerGameController();
        if (
            controller != null &&
            !controller.CanGameplayOperate
        )
        {
            Debug.Log("[NetworkGame] Restart blocked while waiting for player.");
            return;
        }

        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(true);
        }
    }

    public void ConfirmRestart()
    {
        MultiplayerGameController controller =
            GetMultiplayerGameController();
        if (
            controller != null &&
            !controller.CanGameplayOperate
        )
        {
            Debug.Log("[NetworkGame] Restart blocked while waiting for player.");
            return;
        }

        Time.timeScale = 1f;
        if (controller != null)
        {
            controller.RequestRestartGame();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("ChessScene");
        }

        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(false);
        }
    }

    public void ReturnToStartMenu()
    {
        Time.timeScale = 1f;

        MultiplayerGameController controller =
            GetMultiplayerGameController();
        if (controller != null)
        {
            controller.RequestReturnToStart();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("StartScene");
        }
    }

    public void CancelRestart()
    {
        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(false);
        }
    }

    private MultiplayerGameController GetMultiplayerGameController()
    {
        if (multiplayerGameController == null)
        {
            multiplayerGameController =
                FindFirstObjectByType<MultiplayerGameController>();
        }

        return multiplayerGameController;
    }
}
