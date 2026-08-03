using TMPro;
using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI winnerText;

    private LogicManager logicManager;
    private MultiplayerGameController multiplayerGameController;

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

    public void HideGameOver()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

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

    private bool IsRestartBlocked()
    {
        multiplayerGameController = GetMultiplayerGameController();

        return multiplayerGameController != null &&
            !multiplayerGameController.CanGameplayOperate;
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
