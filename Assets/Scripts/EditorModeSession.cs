#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>保存跨場景的本機沙盒模式，並在棋局場景建立編輯工具。</summary>
public static class EditorModeSession
{
    public static bool Enabled { get; private set; }

    public static void SetEnabled(bool enabled)
    {
        Enabled = enabled;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= AttachToGameScene;
        SceneManager.sceneLoaded += AttachToGameScene;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToInitialScene()
    {
        AttachToGameScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void AttachToGameScene(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "ChessScene")
        {
            SetEnabled(false);
            return;
        }
        if (Object.FindFirstObjectByType<EditorModeController>() != null) return;

        GameObject host = new GameObject("EditorModeController");
        host.AddComponent<EditorModeController>();
    }
}

/// <summary>提供卡庫取卡與棋盤棋子配置的本機執行時沙盒。</summary>
public sealed class EditorModeController : MonoBehaviour
{
    private enum Tool { Place, Remove }

    private readonly string[] pieceNames =
        { "Pawn", "Rook", "Knight", "Bishop", "Queen", "King" };

    private LogicManager logic;
    private Board board;
    private CardHandManager hands;
    private ChessCard cardLibrary;
    private Vector2 cardScroll;
    private Rect windowRect = new Rect(18f, 18f, 390f, 670f);
    private Tool tool = Tool.Place;
    private int pieceIndex;
    private bool whitePiece = true;
    private bool whiteHand = true;
    private bool showCards = true;
    private bool panelOpen;
    private bool lockApplied;
    private string status = "選擇卡片，或選擇棋子後點擊棋盤格。";

    private IEnumerator Start()
    {
        yield return null;
        ResolveReferences();
        SetPanelOpen(EditorModeSession.Enabled);
    }

    private void OnDestroy()
    {
        if (lockApplied && logic != null)
        {
            logic.PopOperationLock("EditorMode");
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
        {
            if (!EditorModeSession.Enabled) EditorModeSession.SetEnabled(true);
            SetPanelOpen(!panelOpen);
        }

        if (!panelOpen || showCards || tool == Tool.Place && board == null) return;
        if (!GameplayPointer.Available || !GameplayPointer.Pressed) return;
        Vector2 pointer = GameplayPointer.Position;
        if (windowRect.Contains(new Vector2(pointer.x, Screen.height - pointer.y))) return;

        TryEditBoard(pointer);
    }

    private void ResolveReferences()
    {
        logic = FindFirstObjectByType<LogicManager>();
        board = FindFirstObjectByType<Board>();
        hands = FindFirstObjectByType<CardHandManager>();
        cardLibrary = hands != null ? hands.cardLibrary : FindFirstObjectByType<ChessCard>();
    }

    private void SetPanelOpen(bool open)
    {
        panelOpen = open;
        if (logic == null) ResolveReferences();

        if (open && !lockApplied && logic != null)
        {
            logic.PushOperationLock("EditorMode");
            lockApplied = true;
        }
        else if (!open && lockApplied && logic != null)
        {
            logic.PopOperationLock("EditorMode");
            lockApplied = false;
        }
    }

    private void OnGUI()
    {
        if (!EditorModeSession.Enabled) return;
        if (!panelOpen)
        {
            GUI.Box(new Rect(18f, 18f, 230f, 42f), "編輯器模式已收合  F8 開啟");
            return;
        }

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Chess RPG 編輯器模式");
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();
        GUILayout.Label("F8 收合面板。面板開啟時會暫停正常棋局操作。");

        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(showCards, "卡庫", "Button")) showCards = true;
        if (GUILayout.Toggle(!showCards, "棋盤配置", "Button")) showCards = false;
        GUILayout.EndHorizontal();

        if (showCards) DrawCardLibrary();
        else DrawBoardTools();

        GUILayout.FlexibleSpace();
        GUILayout.Label(status);
        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
    }

    private void DrawCardLibrary()
    {
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("加入：", GUILayout.Width(48f));
        if (GUILayout.Toggle(whiteHand, "白方手牌", "Button")) whiteHand = true;
        if (GUILayout.Toggle(!whiteHand, "黑方手牌", "Button")) whiteHand = false;
        GUILayout.EndHorizontal();

        cardScroll = GUILayout.BeginScrollView(cardScroll, GUILayout.Height(535f));
        if (cardLibrary == null)
        {
            GUILayout.Label("找不到 ChessCard 卡庫。");
        }
        else
        {
            IReadOnlyList<CardDefinition> cards = cardLibrary.Cards;
            foreach (CardDefinition card in cards)
            {
                if (card == null) continue;
                GUILayout.BeginHorizontal("box");
                GUILayout.Label($"{card.id}  {card.cardName}", GUILayout.Width(245f));
                if (GUILayout.Button("取得", GUILayout.Width(85f))) AddCard(card);
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndScrollView();
    }

    private void AddCard(CardDefinition card)
    {
        if (hands == null)
        {
            status = "找不到 CardHandManager。";
            return;
        }

        hands.AddCardToHandForEditor(card, whiteHand);
        status = $"已將 {card.id} {card.cardName} 加入{(whiteHand ? "白方" : "黑方")}手牌。";
    }

    private void DrawBoardTools()
    {
        GUILayout.Space(8f);
        GUILayout.Label("操作工具");
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(tool == Tool.Place, "放置棋子", "Button")) tool = Tool.Place;
        if (GUILayout.Toggle(tool == Tool.Remove, "移除棋子", "Button")) tool = Tool.Remove;
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("棋子陣營");
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(whitePiece, "白方", "Button")) whitePiece = true;
        if (GUILayout.Toggle(!whitePiece, "黑方", "Button")) whitePiece = false;
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("棋子種類");
        for (int i = 0; i < pieceNames.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 2, pieceNames.Length); j++)
            {
                if (GUILayout.Toggle(pieceIndex == j, GetPieceLabel(pieceNames[j]), "Button"))
                    pieceIndex = j;
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(14f);
        GUILayout.Label(tool == Tool.Place
            ? "點擊棋盤上的格子放置棋子。原格已有棋子時會先取代。"
            : "點擊棋盤上的棋子或格子將棋子移除。");

        if (GUILayout.Button("清空整個棋盤", GUILayout.Height(40f))) ClearBoard();
        if (GUILayout.Button("恢復標準開局", GUILayout.Height(40f))) RestoreStartingPosition();
    }

    private static string GetPieceLabel(string type)
    {
        switch (type)
        {
            case "Pawn": return "兵";
            case "Rook": return "城堡";
            case "Knight": return "騎士";
            case "Bishop": return "主教";
            case "Queen": return "皇后";
            case "King": return "國王";
            default: return type;
        }
    }

    private void TryEditBoard(Vector2 screenPosition)
    {
        if (Camera.main == null || board == null || logic == null) return;
        if (!Physics.Raycast(Camera.main.ScreenPointToRay(screenPosition), out RaycastHit hit)) return;

        Vector2Int cell;
        Piece hitPiece = hit.transform.GetComponentInParent<Piece>();
        if (hitPiece != null)
        {
            Vector2 coordinates = hitPiece.GetCoordinates();
            cell = new Vector2Int(Mathf.RoundToInt(coordinates.x), Mathf.RoundToInt(coordinates.y));
        }
        else
        {
            Square square = hit.transform.GetComponentInParent<Square>();
            if (square == null) return;
            cell = new Vector2Int(Mathf.RoundToInt(square.transform.position.x), Mathf.RoundToInt(square.transform.position.z));
        }

        if (cell.x < 0 || cell.x >= board.Width || cell.y < 0 || cell.y >= board.Height) return;
        if (tool == Tool.Remove) RemoveAt(cell);
        else PlaceAt(cell);
    }

    private void PlaceAt(Vector2Int cell)
    {
        RemoveAt(cell, false);
        int prefabIndex = pieceIndex * 2 + (whitePiece ? 0 : 1);
        if (board.PiecePrefabs == null || prefabIndex >= board.PiecePrefabs.Length)
        {
            status = "棋子 Prefab 設定不完整。";
            return;
        }

        float height = pieceNames[pieceIndex] == "Pawn" ? 0.05f : 0.12f;
        Piece piece = board.InstantiatePiece(
            board.PiecePrefabs[prefabIndex],
            new Vector3(cell.x, height, cell.y),
            board.PieceMaterials[whitePiece ? 0 : 1],
            pieceNames[pieceIndex],
            whitePiece
        );
        if (piece != null) piece.UpdateBoardMap();
        RefreshBoardState();
        status = $"已在 ({cell.x}, {cell.y}) 放置{(whitePiece ? "白方" : "黑方")}{GetPieceLabel(pieceNames[pieceIndex])}。";
    }

    private void RemoveAt(Vector2Int cell, bool report = true)
    {
        Piece piece = logic.boardMap[cell.x, cell.y];
        if (piece == null)
        {
            if (report) status = $"({cell.x}, {cell.y}) 沒有棋子。";
            return;
        }

        logic.boardMap[cell.x, cell.y] = null;
        piece.ApplyCard(null);
        logic.OnPieceRemovedFromBoard(piece);
        Destroy(piece.gameObject);
        RefreshBoardState();
        if (report) status = $"已移除 ({cell.x}, {cell.y}) 的棋子。";
    }

    private void ClearBoard()
    {
        if (logic == null) return;
        List<Piece> pieces = new List<Piece>(FindObjectsByType<Piece>(FindObjectsSortMode.None));
        foreach (Piece piece in pieces)
        {
            if (piece == null) continue;
            Vector2 position = piece.GetCoordinates();
            int x = Mathf.RoundToInt(position.x);
            int y = Mathf.RoundToInt(position.y);
            if (x >= 0 && x < 8 && y >= 0 && y < 8) logic.boardMap[x, y] = null;
            piece.ApplyCard(null);
            Destroy(piece.gameObject);
        }
        RefreshBoardState();
        status = "棋盤已清空。";
    }

    private void RestoreStartingPosition()
    {
        ClearBoard();
        board.PlaceStartingPosition();
        RefreshBoardState();
        status = "已恢復標準開局。";
    }

    private void RefreshBoardState()
    {
        if (logic == null) return;
        logic.RefreshBoardFieldEffects();
        logic.UpdateCheckMap();
    }
}
#endif
