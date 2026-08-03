using UnityEngine;

public class PromotionUI : MonoBehaviour
{
    public GameObject panel;

    private Pawn promotingPawn;
    private LogicManager logicManager;
    private MultiplayerGameController multiplayerGameController;

    private void Start()
    {
        logicManager = FindFirstObjectByType<LogicManager>();
        multiplayerGameController =
            FindFirstObjectByType<MultiplayerGameController>();

        Hide();
    }

    public void Show(Pawn pawn)
    {
        promotingPawn = pawn;

        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    public void Hide()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        promotingPawn = null;
    }

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
