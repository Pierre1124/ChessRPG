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

    /// <summary>
    /// 以目前滑鼠位置執行 UI 射線檢查。
    /// </summary>
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

    /// <summary>
    /// 補齊多人控制器並初始化棋子資訊介面。
    /// </summary>
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

    /// <summary>
    /// 依操作鎖狀態處理滑鼠按下、放開與長按顯示資訊。
    /// </summary>
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
            (logicManager == null || !logicManager.IsClassicChess) &&
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

    /// <summary>
    /// 記錄滑鼠按下的位置、時間與棋子，準備點擊或長按操作。
    /// </summary>
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

    /// <summary>
    /// 結束按壓並依位移與長按狀態決定是否執行點擊。
    /// </summary>
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

    /// <summary>
    /// 解析棋盤點擊位置並處理選取或移動操作。
    /// </summary>
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

    /// <summary>
    /// 檢查操作權限後選取棋子並標示合法走法。
    /// </summary>
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

    /// <summary>
    /// 嘗試將已選棋子移往被點擊棋子的位置。
    /// </summary>
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

    /// <summary>
    /// 嘗試將已選棋子移往指定棋盤格。
    /// </summary>
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

    /// <summary>
    /// 執行選取棋子的移動；連線時提交移動命令。
    /// </summary>
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

    /// <summary>
    /// 從螢幕座標投射射線並取得命中的棋子。
    /// </summary>
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

    /// <summary>
    /// 取得指定棋盤格上的棋子。
    /// </summary>
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

    /// <summary>
    /// 標示目前選取棋子的所在格。
    /// </summary>
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

    /// <summary>
    /// 標示目前選取棋子的合法移動格。
    /// </summary>
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

    /// <summary>
    /// 清除棋子選取與棋盤標示。
    /// </summary>
    private void ClearSelection()
    {
        UnhighlightSelectedSquare();
        UnhighlightLegalMoves();
        selectedPiece = null;
    }

    /// <summary>
    /// 移除選取格的高亮顯示。
    /// </summary>
    private void UnhighlightSelectedSquare()
    {
        if (currentlyHighlightedSquare != null)
        {
            currentlyHighlightedSquare.Unhighlight();
            currentlyHighlightedSquare = null;
        }
    }

    /// <summary>
    /// 移除所有合法走法的高亮顯示。
    /// </summary>
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

    /// <summary>
    /// 依音效設定播放移動音效。
    /// </summary>
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

    /// <summary>
    /// 判斷指定螢幕位置是否命中 UI。
    /// </summary>
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

    /// <summary>
    /// 顯示操作或對局提示訊息。
    /// </summary>
    private void ShowAlarm(string message)
    {
        GameFlowUI.Show(message);
    }
}
