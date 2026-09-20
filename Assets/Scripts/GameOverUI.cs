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
            Debug.Log("[NetworkGame] Restart blocked while waiting for player.");
            return;
        }

        Time.timeScale = 1f;
        MultiplayerGameController flow = GetMultiplayerGameController();
        if (flow != null)
        {
            flow.RequestRestartGame();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("ChessScene");
        }
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
