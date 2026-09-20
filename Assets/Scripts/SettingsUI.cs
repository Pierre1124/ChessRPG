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

    /// <summary>
    /// 取得對局引用、載入設定，並綁定音效、玩法及畫面控制項。
    /// </summary>
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

    /// <summary>
    /// 綁定音效開關與音量控制項。
    /// </summary>
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

    /// <summary>
    /// 綁定遊戲操作與相機相關設定控制項。
    /// </summary>
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

    /// <summary>
    /// 綁定解析度與視窗模式控制項。
    /// </summary>
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

    /// <summary>
    /// 依介面選擇套用畫面設定。
    /// </summary>
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

    /// <summary>
    /// 將目前設定控制項的值保存到 PlayerPrefs。
    /// </summary>
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

    /// <summary>
    /// 讀取已儲存設定並套用到介面及遊戲元件。
    /// </summary>
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

    /// <summary>
    /// 開啟設定面板並更新控制項內容。
    /// </summary>
    public void ShowPanel()
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    /// <summary>
    /// 關閉設定面板並返回原本的遊戲介面。
    /// </summary>
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

    /// <summary>
    /// 顯示重新開始對局的確認介面。
    /// </summary>
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

    /// <summary>
    /// 依目前模式確認並執行重新開始要求。
    /// </summary>
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

    /// <summary>
    /// 依連線狀態離開對局並返回主選單。
    /// </summary>
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

    /// <summary>
    /// 關閉重新開始確認介面。
    /// </summary>
    public void CancelRestart()
    {
        if (confirmationPopup != null)
        {
            confirmationPopup.SetActive(false);
        }
    }

    /// <summary>
    /// 取得並快取目前場景中的多人遊戲控制器。
    /// </summary>
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
