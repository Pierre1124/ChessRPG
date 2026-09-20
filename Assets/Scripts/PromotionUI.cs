using UnityEngine;

public class PromotionUI : MonoBehaviour
{
    public GameObject panel;

    private Pawn promotingPawn;
    private LogicManager logicManager;
    private MultiplayerGameController multiplayerGameController;

    /// <summary>
    /// 取得對局及多人控制器，並隱藏初始升變面板。
    /// </summary>
    private void Start()
    {
        logicManager = FindFirstObjectByType<LogicManager>();
        multiplayerGameController =
            FindFirstObjectByType<MultiplayerGameController>();

        Hide();
    }

    /// <summary>
    /// 保存待升變的兵並顯示選擇面板。
    /// </summary>
    public void Show(Pawn pawn)
    {
        promotingPawn = pawn;

        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    /// <summary>
    /// 隱藏升變選擇面板並清除待處理棋子。
    /// </summary>
    public void Hide()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        promotingPawn = null;
    }

    /// <summary>
    /// 提交目前選擇的升變棋子種類。
    /// </summary>
    public void PromotePawn(string pieceName)
    {
        if (promotingPawn == null)
        {
            Hide();
            return;
        }

        Pawn selectedPawn = promotingPawn;

        if (logicManager == null)
        {
            logicManager = FindFirstObjectByType<LogicManager>();
        }

        if (multiplayerGameController == null)
        {
            multiplayerGameController =
                FindFirstObjectByType<MultiplayerGameController>();
        }

        if (
            multiplayerGameController != null &&
            multiplayerGameController.IsOnline
        )
        {
            multiplayerGameController.SubmitPromotionChoice(
                selectedPawn,
                pieceName
            );
            Hide();
            return;
        }

        BoardCoordinate coordinate =
            BoardCoordinate.FromVector2(selectedPawn.GetCoordinates());
        bool isWhitePlayer = selectedPawn.IsWhite;

        Hide();
        logicManager.ApplyPromotionChoice(
            coordinate,
            isWhitePlayer,
            pieceName,
            true
        );
    }
}
