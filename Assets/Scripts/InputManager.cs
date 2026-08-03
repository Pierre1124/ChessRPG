using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [SerializeField] private LogicManager logicManager;
    [SerializeField] private CardInfoUI cardInfoUI;
    [SerializeField] private MultiplayerGameController multiplayerGameController;
    [SerializeField, Min(0.1f)] private float pieceInfoHoldSeconds = 1.5f;

    private readonly List<Square> highlightedSquares =
        new List<Square>();

    private Piece selectedPiece;
    private Square currentlyHighlightedSquare;
    private bool isHoldingMouse;
    private bool didShowPieceInfo;
    private float holdStartedAt;
    private Vector2 holdStartScreenPosition;
    private Piece heldPiece;

    public static bool IsPointerOverUiStatic()
    {
        if (EventSystem.current == null || Mouse.current == null)
        {
            return false;
        }

        PointerEventData eventData =
            new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    private void Start()
    {
        if (multiplayerGameController == null)
        {
            multiplayerGameController =
                FindFirstObjectByType<MultiplayerGameController>();
        }

        if (cardInfoUI != null)
        {
            cardInfoUI.Initialize(logicManager);
        }
        else
        {
            Debug.LogError(
                "InputManager requires CardInfoUI to be assigned."
            );
        }
    }

    private void Update()
    {
        if (
            (logicManager != null && logicManager.IsOperationLocked) ||
            multiplayerGameController != null &&
            !multiplayerGameController.CanGameplayOperate
        )
        {
            isHoldingMouse = false;
            heldPiece = null;
            ClearSelection();
            return;
        }

        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            BeginHold(mousePosition);
            return;
        }

        if (
            isHoldingMouse &&
            Mouse.current.leftButton.isPressed &&
            !didShowPieceInfo &&
            heldPiece != null &&
            Time.unscaledTime - holdStartedAt >= pieceInfoHoldSeconds
        )
        {
            didShowPieceInfo = true;
            cardInfoUI?.Show(heldPiece);
            return;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndHold(mousePosition);
        }
    }

    private void BeginHold(Vector2 screenPosition)
    {
        isHoldingMouse = true;
        didShowPieceInfo = false;
        holdStartedAt = Time.unscaledTime;
        holdStartScreenPosition = screenPosition;
        heldPiece = IsPointerOverUi(screenPosition)
            ? null
            : GetPieceAtScreenPosition(screenPosition);
    }

    private void EndHold(Vector2 screenPosition)
    {
        bool shouldHandleClick =
            isHoldingMouse &&
            !didShowPieceInfo &&
            !IsPointerOverUi(screenPosition) &&
            Vector2.Distance(
                holdStartScreenPosition,
                screenPosition
            ) <= 12f;

        isHoldingMouse = false;
        heldPiece = null;

        if (!shouldHandleClick)
        {
            return;
        }

        cardInfoUI?.Hide();
        HandleClick(screenPosition);
    }

    private void HandleClick(Vector2 screenPosition)
    {
        if (
            logicManager == null ||
            logicManager.isPromotionActive ||
            logicManager.IsOperationLocked ||
            (
                multiplayerGameController != null &&
                !multiplayerGameController.CanGameplayOperate
            ) ||
            Camera.main == null
        )
        {
            ShowAlarm("現在不能操作");
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            return;
        }

        Piece hitPiece = hit.transform.GetComponentInParent<Piece>();
        Square hitSquare = hit.transform.GetComponentInParent<Square>();

        if (hitPiece != null)
        {
            if (TryMoveSelectedToPiece(hitPiece))
            {
                return;
            }

            TrySelectPiece(hitPiece);
            return;
        }

        if (hitSquare != null)
        {
            if (TryMoveSelectedToSquare(hitSquare))
            {
                return;
            }

            Piece pieceOnSquare = GetPieceOnSquare(hitSquare);

            if (pieceOnSquare != null)
            {
                TrySelectPiece(pieceOnSquare);
            }
        }
    }

    private bool TrySelectPiece(Piece piece)
    {
        if (
            piece == null ||
            piece.IsWhite != logicManager.isWhiteTurn ||
            (
                multiplayerGameController != null &&
                !multiplayerGameController.CanLocalPlayerAct(
                    logicManager.isWhiteTurn
                )
            )
        )
        {
            if (piece != null && piece.IsWhite != logicManager.isWhiteTurn)
            {
                ShowAlarm(
                    $"現在是{(logicManager.isWhiteTurn ? "白方" : "黑方")}回合"
                );
            }
            else
            {
                ShowAlarm("不是你的回合");
            }

            return false;
        }

        if (selectedPiece == piece)
        {
            ClearSelection();
            return true;
        }

        selectedPiece = piece;
        HighlightSelectedSquare();
        HighlightLegalMoves(selectedPiece.GetLegalMoves());
        return true;
    }

    private bool TryMoveSelectedToPiece(Piece targetPiece)
    {
        if (selectedPiece == null || targetPiece == null)
        {
            return false;
        }

        Vector2 targetCoordinates = targetPiece.GetCoordinates();
        Square targetSquare =
            logicManager.GetSquareAtPosition(targetCoordinates);

        if (
            targetSquare == null ||
            !highlightedSquares.Contains(targetSquare)
        )
        {
            if (targetPiece.IsWhite != selectedPiece.IsWhite)
            {
                ShowAlarm("非法走法");
            }

            return false;
        }

        MoveSelectedPiece(targetCoordinates);
        return true;
    }

    private bool TryMoveSelectedToSquare(Square targetSquare)
    {
        if (
            selectedPiece == null ||
            targetSquare == null ||
            !highlightedSquares.Contains(targetSquare)
        )
        {
            if (selectedPiece != null && targetSquare != null)
            {
                ShowAlarm("非法走法");
            }

            return false;
        }

        Vector2 targetCoordinates =
            new Vector2(
                targetSquare.transform.position.x,
                targetSquare.transform.position.z
            );

        MoveSelectedPiece(targetCoordinates);
        return true;
    }

    private void MoveSelectedPiece(Vector2 targetCoordinates)
    {
        if (
            multiplayerGameController != null &&
            multiplayerGameController.IsOnline
        )
        {
            NetworkGameCommand command =
                multiplayerGameController.CreateMoveCommand(
                    selectedPiece,
                    targetCoordinates
                );
            ClearSelection();
            multiplayerGameController.SubmitCommand(command);
            return;
        }

        Vector2 startPosition = selectedPiece.GetCoordinates();
        bool isCapture =
            logicManager.boardMap[
                (int)targetCoordinates.x,
                (int)targetCoordinates.y
            ] != null;

        bool isEnPassant =
            selectedPiece is Pawn &&
            !isCapture &&
            Mathf.Abs(targetCoordinates.x - startPosition.x) == 1 &&
            Mathf.Abs(targetCoordinates.y - startPosition.y) == 1;

        Piece movedPiece = selectedPiece;
        movedPiece.Move(targetCoordinates);
        OperateLogUI.LogMove(
            movedPiece,
            startPosition,
            movedPiece.GetCoordinates()
        );

        logicManager.lastMovedPiece = movedPiece;
        logicManager.lastMovedPieceStartPosition = startPosition;
        logicManager.lastMovedPieceEndPosition =
            movedPiece.GetCoordinates();

        ClearSelection();
        PlayMoveSound(isCapture, isEnPassant);

        if (!logicManager.isPromotionActive)
        {
            logicManager.UpdateCheckMap();
            logicManager.EndTurn();
        }
    }

    private Piece GetPieceAtScreenPosition(Vector2 screenPosition)
    {
        if (Camera.main == null || logicManager == null)
        {
            return null;
        }

        Ray ray = Camera.main.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            return null;
        }

        Piece piece = hit.transform.GetComponentInParent<Piece>();

        if (piece != null)
        {
            return piece;
        }

        Square square = hit.transform.GetComponentInParent<Square>();
        return square != null ? GetPieceOnSquare(square) : null;
    }

    private Piece GetPieceOnSquare(Square square)
    {
        Vector2 squareCoordinates =
            new Vector2(
                square.transform.position.x,
                square.transform.position.z
            );

        return logicManager.boardMap[
            (int)squareCoordinates.x,
            (int)squareCoordinates.y
        ];
    }

    private void HighlightSelectedSquare()
    {
        UnhighlightSelectedSquare();
        UnhighlightLegalMoves();

        currentlyHighlightedSquare =
            logicManager.GetSquareAtPosition(
                selectedPiece.GetCoordinates()
            );

        if (currentlyHighlightedSquare != null)
        {
            currentlyHighlightedSquare.Highlight(
                new Color(0f, 0.6f, 0.6f)
            );
        }
    }

    private void HighlightLegalMoves(List<Vector2> legalMoves)
    {
        UnhighlightLegalMoves();

        foreach (Vector2 move in legalMoves)
        {
            Square square = logicManager.GetSquareAtPosition(move);

            if (square == null)
            {
                continue;
            }

            Piece pieceOnSquare =
                logicManager.boardMap[(int)move.x, (int)move.y];

            square.Highlight(
                pieceOnSquare != null ? Color.red : Color.cyan
            );
            highlightedSquares.Add(square);
        }
    }

    private void ClearSelection()
    {
        UnhighlightSelectedSquare();
        UnhighlightLegalMoves();
        selectedPiece = null;
    }

    private void UnhighlightSelectedSquare()
    {
        if (currentlyHighlightedSquare != null)
        {
            currentlyHighlightedSquare.Unhighlight();
            currentlyHighlightedSquare = null;
        }
    }

    private void UnhighlightLegalMoves()
    {
        foreach (Square square in highlightedSquares)
        {
            if (square != null)
            {
                square.Unhighlight();
            }
        }

        highlightedSquares.Clear();
    }

    private void PlayMoveSound(bool isCapture, bool isEnPassant)
    {
        if (isCapture || isEnPassant)
        {
            if (logicManager.captureSound != null)
            {
                logicManager.captureSound.Play();
            }
            return;
        }

        if (logicManager.moveSound != null)
        {
            logicManager.moveSound.Play();
        }
    }

    private bool IsPointerOverUi(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData eventData =
            new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0;
    }

    private void ShowAlarm(string message)
    {
        GameFlowUI.Show(message);
    }
}
