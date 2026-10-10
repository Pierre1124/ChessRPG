using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>套用本機設定、管理設定視窗與需要確認的對局操作。</summary>
public class SettingsUI : MonoBehaviour
{
    public GameObject panel, confirmationPopup;
    public Toggle soundToggle, cameraRotationToggle, windowedToggle;
    public Slider volumeSlider;
    public TMP_Dropdown resolutionDropdown;
    private LogicManager logicManager;
    private LayoutUI layoutUI;
    private MultiplayerGameController multiplayerGameController;
    private Resolution[] availableResolutions;
    private SettingsPanelView view;
    private bool ownsLock, displayPending, pendingWindowed;
    private float displayDeadline;
    private int previousWidth, previousHeight, pendingIndex;
    private FullScreenMode previousMode;

    /// <summary>建立介面，載入偏好並立即套用音效及相機狀態。</summary>
    private void Start()
    {
        logicManager = FindFirstObjectByType<LogicManager>();
        layoutUI = FindFirstObjectByType<LayoutUI>();
        multiplayerGameController = FindFirstObjectByType<MultiplayerGameController>();
        if (panel == null) return;
        view = panel.GetComponent<SettingsPanelView>();
        if (view == null) { Debug.LogError("設定頁尚未烘焙至場景。", this); return; }
        view.Initialize(this);
        soundToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("SoundEnabled", 1) == 1);
        float volume = PlayerPrefs.GetFloat("SoundVolume", 0.5f);
        volumeSlider.SetValueWithoutNotify(float.IsNaN(volume) ? 0.5f : Mathf.Clamp01(volume));
        cameraRotationToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("CameraRotationEnabled", 1) == 1);
        availableResolutions = DisplaySettingsController.PopulateResolutionDropdown(resolutionDropdown);
        windowedToggle.SetIsOnWithoutNotify(DisplaySettingsController.IsWindowed());
        ApplyAudio();
        if (logicManager != null) logicManager.ToggleCameraRotation(cameraRotationToggle.isOn);
        soundToggle.onValueChanged.AddListener(OnSoundChanged);
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        cameraRotationToggle.onValueChanged.AddListener(OnCameraChanged);
        panel.SetActive(false);
    }
    /// <summary>即時靜音並保留玩家的音量值。</summary>
    private void OnSoundChanged(bool value) { ApplyAudio(); SaveSettings(); }
    /// <summary>即時更新音量與百分比，不逐幀寫入磁碟。</summary>
    private void OnVolumeChanged(float value) { ApplyAudio(); SaveSettings(); }
    /// <summary>立即更新回合切換時的相機旋轉。</summary>
    private void OnCameraChanged(bool value)
    {
        if (logicManager != null) logicManager.ToggleCameraRotation(value);
        SaveSettings();
    }
    /// <summary>同步音效開關、音量與控制項。</summary>
    private void ApplyAudio()
    {
        volumeSlider.interactable = soundToggle.isOn;
        if (logicManager != null) { logicManager.ToggleSound(soundToggle.isOn); logicManager.SetSoundVolume(volumeSlider.value); }
        view.UpdateVolume(volumeSlider.value, soundToggle.isOn);
    }
    /// <summary>更新偏好快取，關閉設定時集中寫入磁碟。</summary>
    private void SaveSettings()
    {
        PlayerPrefs.SetInt("SoundEnabled", soundToggle.isOn ? 1 : 0);
        PlayerPrefs.SetFloat("SoundVolume", volumeSlider.value);
        PlayerPrefs.SetInt("CameraRotationEnabled", cameraRotationToggle.isOn ? 1 : 0);
    }
    /// <summary>使用獨立操作鎖開啟設定，不修改棋子升變狀態。</summary>
    public void ShowPanel()
    {
        if (panel == null) return;
        ControlBindings.SettingsOpen = true;
        if (!ownsLock && logicManager != null) { logicManager.PushOperationLock("Settings"); ownsLock = true; }
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }
    /// <summary>撤銷未確認的顯示變更，保存偏好並釋放自己的操作鎖。</summary>
    public void goBack()
    {
        ControlBindings.SettingsOpen = false;
        RevertDisplay(); CancelRestart(); PlayerPrefs.Save();
        if (panel != null) panel.SetActive(false);
        ReleaseLock();
        if (layoutUI != null) layoutUI.gameObject.SetActive(true);
    }
    /// <summary>只釋放設定面板持有的鎖，保留其他流程的鎖定。</summary>
    private void ReleaseLock()
    {
        if (ownsLock && logicManager != null) logicManager.PopOperationLock("Settings");
        ownsLock = false;
    }
    /// <summary>暫時套用顯示選擇，玩家確認後才保存。</summary>
    public void ApplyDisplayChanges()
    {
        if (displayPending || availableResolutions == null || availableResolutions.Length == 0) return;
        previousWidth = Screen.width; previousHeight = Screen.height; previousMode = Screen.fullScreenMode;
        pendingIndex = resolutionDropdown.value; pendingWindowed = windowedToggle.isOn;
        DisplaySettingsController.ApplyResolution(pendingIndex, availableResolutions, pendingWindowed, false);
        displayPending = true; displayDeadline = Time.unscaledTime + 12f;
        view.ShowConfirmation("保留新的顯示設定？", KeepDisplay, RevertDisplay, "保留", "還原");
    }
    /// <summary>確認後保存顯示設定。</summary>
    private void KeepDisplay()
    {
        if (!displayPending) return;
        displayPending = false;
        DisplaySettingsController.ApplyResolution(pendingIndex, availableResolutions, pendingWindowed, true);
        view.HideConfirmation();
    }
    /// <summary>取消或逾時時還原實際畫面及控制項。</summary>
    private void RevertDisplay()
    {
        if (!displayPending) return;
        displayPending = false;
        Screen.SetResolution(previousWidth, previousHeight, previousMode);
        availableResolutions = DisplaySettingsController.PopulateResolutionDropdown(resolutionDropdown);
        windowedToggle.SetIsOnWithoutNotify(DisplaySettingsController.IsWindowed());
        view.HideConfirmation();
    }
    /// <summary>不受暫停影響的倒數，避免顯示設定導致畫面無法操作。</summary>
    private void Update()
    {
        if (ControlBindings.Capturing || ControlBindings.CaptureFinishedFrame == Time.frameCount) return;
        if (panel != null && panel.activeSelf && UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame &&
            (resolutionDropdown == null || !resolutionDropdown.IsExpanded))
        {
            if (displayPending) RevertDisplay();
            else if (view != null && view.IsConfirmationVisible) CancelRestart();
            else goBack();
            return;
        }
        if (!displayPending) return;
        int seconds = Mathf.CeilToInt(displayDeadline - Time.unscaledTime);
        if (seconds <= 0) RevertDisplay();
        else view.SetConfirmationText($"保留新的顯示設定？\n{seconds} 秒後自動還原");
    }
    /// <summary>重新開始前檢查連線狀態並顯示確認。</summary>
    public void ShowRestartConfirmation()
    {
        if (displayPending) return;
        if (multiplayerGameController != null && !multiplayerGameController.CanGameplayOperate)
        { GameFlowUI.Show("請等待玩家加入或對局同步完成"); return; }
        view.ShowConfirmation("重新開始對局？\n目前棋局進度將會清除。", ConfirmRestart, CancelRestart, "重新開始", "取消");
    }
    /// <summary>再次驗證狀態並執行重新開始。</summary>
    public void ConfirmRestart()
    {
        if (multiplayerGameController != null && !multiplayerGameController.CanGameplayOperate)
        { CancelRestart(); GameFlowUI.Show("目前無法重新開始，請稍後再試"); return; }
        PlayerPrefs.Save(); Time.timeScale = 1f; ReleaseLock();
        if (multiplayerGameController != null) multiplayerGameController.RequestRestartGame();
        else UnityEngine.SceneManagement.SceneManager.LoadScene("ChessScene");
    }
    /// <summary>返回主選單前先確認，避免誤觸直接離開。</summary>
    public void ReturnToStartMenu()
    {
        if (!displayPending) view.ShowConfirmation("離開對局並返回主選單？", ConfirmReturn, CancelRestart, "離開對局", "取消");
    }
    /// <summary>保存偏好並依連線狀態離開對局。</summary>
    private void ConfirmReturn()
    {
        PlayerPrefs.Save(); Time.timeScale = 1f; ReleaseLock();
        if (multiplayerGameController != null) multiplayerGameController.RequestReturnToStart();
        else UnityEngine.SceneManagement.SceneManager.LoadScene("StartScene");
    }
    /// <summary>取消確認視窗，保留棋局與設定。</summary>
    public void CancelRestart() { if (view != null) view.HideConfirmation(); }
    /// <summary>停用時撤銷顯示試用並釋放設定鎖。</summary>
    private void OnDisable() { ControlBindings.SettingsOpen = false; RevertDisplay(); ReleaseLock(); PlayerPrefs.Save(); }
    /// <summary>切到背景時保存本機偏好。</summary>
    private void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
}
