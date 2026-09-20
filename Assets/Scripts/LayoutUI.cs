using UnityEngine;

public class LayoutUI : MonoBehaviour
{
    private SettingsUI settingsUI;
    private LogicManager logicManager;

    /// <summary>
    /// 取得設定介面與對局控制器引用。
    /// </summary>
    void Start()
    {
        settingsUI = FindFirstObjectByType<SettingsUI>();
        logicManager = FindFirstObjectByType<LogicManager>();
    }

    /// <summary>
    /// 開啟設定介面。
    /// </summary>
    public void ShowSettings()
    {
        if (settingsUI != null && logicManager != null)
        {
            settingsUI.ShowPanel();
            logicManager.isPromotionActive = true; //disable pieces selecting
            gameObject.SetActive(false);
        }
    }
}