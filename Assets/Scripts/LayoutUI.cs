using UnityEngine;

public class LayoutUI : MonoBehaviour
{
    private SettingsUI settingsUI;
    private LogicManager logicManager;

    void Start()
    {
        settingsUI = FindFirstObjectByType<SettingsUI>();
        logicManager = FindFirstObjectByType<LogicManager>();
    }

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