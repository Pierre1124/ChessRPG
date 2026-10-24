using TMPro;
using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI winnerText;

    private LogicManager logicManager;
    private MultiplayerGameController multiplayerGameController;

    /// <summary>
    /// 取得對局及多人控制器，並隱藏初始結束畫面。
    /// </summary>
    private void Start()
    {
        logicManager = FindFirstObjectByType<LogicManager>();
        multiplayerGameController =
            FindFirstObjectByType<MultiplayerGameController>();

        EnsureReturnButton();
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    /// <summary>
    /// 顯示遊戲結束結果並套用相關介面狀態。
    /// </summary>
    public void ShowGameOver(string result)
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }

        if (winnerText != null)
        {
            winnerText.text = result;
        }
    }

    /// <summary>
    /// 隱藏遊戲結束介面。
    /// </summary>
    public void HideGameOver()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    /// <summary>
    /// 依本機或連線模式提出重新開始要求。
    /// </summary>
    public void RestartGame()
    {
        if (IsRestartBlocked())
        {
            GameFlowUI.Show("對手尚未就緒或已離開，請返回主選單");
            return;
        }

        MultiplayerGameController flow = GetMultiplayerGameController();
        if (flow != null)
        {
            flow.RequestRestartGame();
        }
        else
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("ChessScene");
        }
    }

    private void EnsureReturnButton()
    {
        if (panel == null || panel.transform.Find("ReturnToMenu") != null) return;
        var restart = panel.GetComponentInChildren<UnityEngine.UI.Button>(true);
        if (restart == null) return;
        var rect = (RectTransform)restart.transform;
        // The original restart button uses stretched anchors, negative sizeDelta and scale 3.
        // Give both actions stable dimensions instead of copying that geometry.
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(360, 64);
        rect.anchoredPosition = new Vector2(0, -100);
        var restartLabel = restart.GetComponentInChildren<TMP_Text>(true);
        if (restartLabel != null)
        {
            restartLabel.text = "重新開始";
            restartLabel.fontSize = 26;
            restartLabel.rectTransform.localScale = Vector3.one;
            restartLabel.rectTransform.anchorMin = Vector2.zero;
            restartLabel.rectTransform.anchorMax = Vector2.one;
            restartLabel.rectTransform.offsetMin = new Vector2(8, 4);
            restartLabel.rectTransform.offsetMax = new Vector2(-8, -4);
        }
        var button = GameFlowUI.CreateActionButton(rect.parent, "ReturnToMenu", "返回主選單",
            new Vector2(0, -184), new Vector2(360, 64),
            winnerText != null ? winnerText.font : TMP_Settings.defaultFontAsset, ReturnToStartMenu);
        var added = (RectTransform)button.transform;
        added.anchorMin = rect.anchorMin; added.anchorMax = rect.anchorMax; added.pivot = rect.pivot;
    }

    public void ReturnToStartMenu()
    {
        MultiplayerGameController flow = GetMultiplayerGameController();
        if (flow != null) flow.RequestReturnToStart();
        else { Time.timeScale = 1f; UnityEngine.SceneManagement.SceneManager.LoadScene("StartScene"); }
    }

    /// <summary>
    /// 判斷目前對局狀態是否禁止重新開始。
    /// </summary>
    private bool IsRestartBlocked()
    {
        multiplayerGameController = GetMultiplayerGameController();

        return multiplayerGameController != null &&
            !multiplayerGameController.CanGameplayOperate;
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
