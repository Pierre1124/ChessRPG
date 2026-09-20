using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 從編輯器選單執行 F12 本機回歸測試；只修改 Play Mode 場景並輸出驗證紀錄。
/// </summary>
[InitializeOnLoad]
public static class ClassicChessRegression
{
    private const string Prefix = "ChessRPG.ClassicRegression.";
    private const string OutputPath = "output/classic-chess-regression.txt";

    /// <summary>
    /// 在腳本重載後恢復測試監看，未啟動測試時不操作場景。
    /// </summary>
    static ClassicChessRegression()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary>
    /// 保存原本的 Play Mode 起始場景，以 ChessScene 啟動隔離的本機驗證。
    /// </summary>
    [MenuItem("Tools/Chess/Run Classic Chess Regression")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run the regression from idle Edit Mode.");
        if (Photon.Pun.PhotonNetwork.IsConnected)
            throw new InvalidOperationException("The local regression requires Photon to be disconnected.");

        SessionState.SetString(Prefix + "PreviousScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Prefix + "Running", true);
        SessionState.SetInt(Prefix + "Stage", 0);
        SessionState.SetInt(Prefix + "OldLogic", 0);
        SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 90f);
        Directory.CreateDirectory("output");
        File.WriteAllText(OutputPath, "F12 local Play Mode regression\n");
        LogicManager.SetNextGameMode(false);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/ChessScene.unity");
        EditorApplication.isPlaying = true;
    }

    /// <summary>
    /// 結束 Play Mode 後還原編輯器原本的起始場景設定。
    /// </summary>
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Prefix + "Running", false)) return;
        string previous = SessionState.GetString(Prefix + "PreviousScene", string.Empty);
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous)
            ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        SessionState.SetBool(Prefix + "Running", false);
    }

    /// <summary>
    /// 等待每次場景重载完成後依序檢查 F12、吃子、升變與 RPG 恢復。
    /// </summary>
    private static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Running", false) || !EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Prefix + "Deadline", 0f))
                throw new TimeoutException("Scene transition did not complete in time.");
            LogicManager logic = Object.FindFirstObjectByType<LogicManager>();
            if (logic == null || logic.GetInstanceID() == SessionState.GetInt(Prefix + "OldLogic", 0)) return;
            logic.UpdatePiecesOnBoard();
            if (logic.piecesOnBoard.Count != 32) return;

            int stage = SessionState.GetInt(Prefix + "Stage", 0);
            CardHandManager hand = Object.FindFirstObjectByType<CardHandManager>(FindObjectsInactive.Include);
            FieldCardPlace[] places = Object.FindObjectsByType<FieldCardPlace>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (stage == 0)
            {
                Check(!logic.IsClassicChess && hand.gameObject.activeInHierarchy, "Initial game uses RPG mode");
                Advance(logic, 1);
                Check(logic.TryPlayFieldCard(hand.cardLibrary.GetCard("F12"), places[0]), "Direct F12 accepted");
            }
            else if (stage == 1 || stage == 3 || stage == 5)
            {
                VerifyClassicGame(logic, hand, places);
                if (stage == 1)
                {
                    VerifyCaptureAndPromotion(logic, hand);
                    Advance(logic, 2);
                    Object.FindFirstObjectByType<MultiplayerGameController>().RequestRestartGame();
                }
                else if (stage == 3)
                {
                    Advance(logic, 4);
                    Object.FindFirstObjectByType<MultiplayerGameController>().RequestRestartGame();
                }
                else
                {
                    VerifyCheckmate(logic);
                    File.AppendAllText(OutputPath, "PASS: all local regression checks completed\n");
                    EditorApplication.isPlaying = false;
                }
            }
            else if (stage == 2)
            {
                Check(!logic.IsClassicChess && hand.gameObject.activeInHierarchy && hand.CanDrawThisTurn, "New game restores RPG hand and draw controls");
                Check(places.All(p => p.gameObject.activeInHierarchy), "New game restores field slots");
                Check(Object.FindFirstObjectByType<DamageCalculationVisualizer>() != null, "New game restores damage visualizer");
                Advance(logic, 3);
                // 讀取目前規則中的 F12 合成配方，透過實際場地入口驗證。
                string[] recipe = FindF12Recipe(logic);
                Check(logic.TryPlayFieldCard(hand.cardLibrary.GetCard(recipe[0]), places[0]), "First fusion field accepted");
                Check(logic.TryPlayFieldCard(hand.cardLibrary.GetCard(recipe[1]), places[1]), "Second fusion field accepted");
            }
            else if (stage == 4)
            {
                Check(!logic.IsClassicChess && hand.gameObject.activeInHierarchy, "RPG restored before deferred animation test");
                CardDefinition card = hand.cardLibrary.GetCard("F12");
                logic.DeferFullGameRestartUntilUsingCard();
                Check(logic.TryPlayFieldCard(card, places[0]), "Deferred F12 accepted");
                Check(logic.HasDeferredFullGameRestart && !logic.IsClassicChess, "F12 waits for card animation completion");
                Advance(logic, 5);
                hand.PlayUsingCardAnimation(card, null);
            }
        }
        catch (Exception exception)
        {
            File.AppendAllText(OutputPath, "FAIL: " + exception + "\n");
            Debug.LogException(exception);
            EditorApplication.isPlaying = false;
        }
    }

    /// <summary>
    /// 從既有合成規則取得 F12 配方，避免測試另行複製卡號規則。
    /// </summary>
    private static string[] FindF12Recipe(LogicManager logic)
    {
        var method = typeof(LogicManager).GetMethod("GetFieldFusionId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        for (int a = 1; a <= 12; a++)
            for (int b = a; b <= 12; b++)
            {
                string first = "F" + a.ToString("00"), second = "F" + b.ToString("00");
                if ((string)method.Invoke(logic, new object[] { first, second }) == "F12") return new[] { first, second };
            }
        throw new InvalidOperationException("No F12 fusion recipe found.");
    }

    /// <summary>
    /// 檢查新局的規則旗標、棋子資料與 RPG 介面均已停用。
    /// </summary>
    private static void VerifyClassicGame(LogicManager logic, CardHandManager hand, FieldCardPlace[] places)
    {
        Check(logic.IsClassicChess && logic.isWhiteTurn, "F12 reload starts a classic game with white to move");
        Check(!hand.gameObject.activeInHierarchy && !hand.CanDrawThisTurn, "Card UI and drawing disabled");
        Check(hand.SerializeNetworkCardState() == string.Empty, "No RPG card state is serialized");
        Check(places.All(p => !p.gameObject.activeInHierarchy && p.ActiveCard == null), "Field slots hidden and empty");
        Check(Object.FindObjectsByType<DamageCalculationVisualizer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .All(v => !v.gameObject.activeInHierarchy), "Damage UI disabled");
        Check(logic.piecesOnBoard.All(p => p.cardDefinition == null && p.CardRuntime == null && p.Statuses.Count == 0), "All pieces have no RPG equipment or statuses");
        var board = Object.FindFirstObjectByType<Board>();
        Check(!board.transform.Find("WhiteHandCard").gameObject.activeInHierarchy &&
            !board.transform.Find("BlackHandCard").gameObject.activeInHierarchy, "Both opponent card-back containers hidden");
        Check(!hand.SetDeckForPlayerAsAuthority(true, "J01,J02", false), "Late deck submission rejected");
        Check(!hand.PlayCardOnPieceAsAuthority(true, "J01", new BoardCoordinate(0, 1), false), "Remote card replay rejected");
        Check(!hand.PlayFieldCardAsAuthority(true, "F01", places[0].name, false), "Remote field replay rejected");
        bool callbackInvoked = false;
        logic.PlayDamageCalculation(new DamageCalculationSequence(), () => callbackInvoked = true, () => callbackInvoked = true);
        logic.PlayRemoteDamageCalculationBatch("{}");
        Check(!callbackInvoked && !logic.IsDamageCalculationBusy, "Damage callbacks and remote batches stay disabled");
        logic.DamagePlayer(true, 1000);
        logic.whiteHealth = 0;
        Check(!logic.CheckHealthGameOver() && Time.timeScale == 1f, "HP cannot end the classic game");
        logic.whiteHealth = LogicManager.MaxHealth;
    }

    /// <summary>
    /// 執行合法開局吃子，再建立升變測試位置，檢查回合、血量與升變功能。
    /// </summary>
    private static void VerifyCaptureAndPromotion(LogicManager logic, CardHandManager hand)
    {
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(4, 1), new BoardCoordinate(4, 3), true), "Classic e2-e4");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(3, 6), new BoardCoordinate(3, 4), false), "Classic d7-d5");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(4, 3), new BoardCoordinate(3, 4), true), "Classic e4xd5");
        logic.UpdatePiecesOnBoard();
        Check(logic.piecesOnBoard.Count == 31 && !logic.isWhiteTurn, "Capture removes one piece and advances turn");
        Check(logic.whiteHealth == LogicManager.MaxHealth && logic.blackHealth == LogicManager.MaxHealth &&
            !logic.IsDamageCalculationBusy, "Capture has no HP damage or damage wait");

        Pawn pawn = (Pawn)logic.boardMap[0, 1];
        for (int y = 6; y <= 7; y++)
        {
            Object.Destroy(logic.boardMap[0, y].gameObject);
            logic.boardMap[0, y] = null;
        }
        pawn.Move(new Vector2(0, 6));
        logic.isWhiteTurn = true;
        logic.UpdateCheckMap();
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(0, 6), new BoardCoordinate(0, 7), true), "Classic pawn reaches promotion rank");
        Check(logic.isPromotionActive, "Promotion choice remains available");
        Check(logic.ApplyPromotionChoice(new BoardCoordinate(0, 7), true, "Queen", true), "Classic promotion accepted");
        Check(logic.boardMap[0, 7] is Queen && !logic.isPromotionActive && !logic.isWhiteTurn, "Promotion finishes without RPG steps");
    }

    /// <summary>
    /// 執行愚人將死，確認普通棋局仍能以將死結束而不依賴血量。
    /// </summary>
    private static void VerifyCheckmate(LogicManager logic)
    {
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(5, 1), new BoardCoordinate(5, 2), true), "Mate setup f2-f3");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(4, 6), new BoardCoordinate(4, 4), false), "Mate setup e7-e5");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(6, 1), new BoardCoordinate(6, 3), true), "Mate setup g2-g4");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(3, 7), new BoardCoordinate(7, 3), false), "Mate move Qd8-h4");
        Check(Time.timeScale == 0f && Object.FindFirstObjectByType<GameOverUI>().panel.activeInHierarchy,
            "Checkmate ends the classic game with result UI");
        Check(logic.whiteHealth == LogicManager.MaxHealth && logic.blackHealth == LogicManager.MaxHealth,
            "Checkmate does not consume player HP");
    }

    /// <summary>
    /// 記錄場景切換前的實例，讓下一階段等待新的對局物件完成初始化。
    /// </summary>
    private static void Advance(LogicManager logic, int stage)
    {
        SessionState.SetInt(Prefix + "OldLogic", logic.GetInstanceID());
        SessionState.SetInt(Prefix + "Stage", stage);
    }

    /// <summary>
    /// 檢查測試條件並立即寫入結果，失敗時中止測試。
    /// </summary>
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        File.AppendAllText(OutputPath, "PASS: " + description + "\n");
    }
}
