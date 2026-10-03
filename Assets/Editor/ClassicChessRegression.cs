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
        SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 150f);
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
                if (logic.IsOperationLocked) return;
                Check(!logic.IsClassicChess && hand.gameObject.activeInHierarchy, "Initial game uses RPG mode");
                VerifyCardPreviews(logic, hand, places);
                VerifyDragGate(logic, hand);
                VerifyPrivateHandState(logic, hand);
                Advance(logic, 1);
                Check(logic.TryPlayFieldCard(hand.cardLibrary.GetCard("F12"), places[0]), "Direct F12 accepted");
            }
            else if (stage == 1 || stage == 3 || stage == 5 || stage == 7 || stage == 9)
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
                else if (stage == 5 || stage == 7)
                {
                    if (stage == 5) VerifyEnPassant(logic);
                    else VerifyCastling(logic);
                    Advance(logic, stage + 1);
                    Object.FindFirstObjectByType<MultiplayerGameController>().RequestRestartGame();
                }
                else
                {
                    VerifyCheckmate(logic);
                    File.AppendAllText(OutputPath, "PASS: all local regression checks completed\n");
                    EditorApplication.isPlaying = false;
                }
            }
            else if (stage == 6 || stage == 8)
            {
                Advance(logic, stage + 1);
                Check(logic.TryPlayFieldCard(hand.cardLibrary.GetCard("F12"), places[0]), "Restart classic rules fixture");
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

    /// <summary>驗證實際手牌序列化、非法牌組的原子拒絕與私有資料套用。</summary>
    private static void VerifyPrivateHandState(LogicManager logic, CardHandManager hand)
    {
        string original = hand.SerializeNetworkCardState(true);
        Check(PrivateCardState.TryDecode(original, out PrivateCardState white), "Encode actual white hand");
        Check(PrivateCardState.TryDecode(hand.SerializeNetworkCardState(false), out PrivateCardState black), "Encode actual black hand");
        Check(white.recipientWhite && !black.recipientWhite, "Private packets target opposite player perspectives");
        Check(!hand.SetDeckForPlayerAsAuthority(true, "J01,INVALID", false), "Invalid submitted deck is rejected");
        Check(hand.SerializeNetworkCardState(true) == original, "Invalid deck leaves current hand unchanged");
        hand.ApplyNetworkCardState(PrivateCardState.Encode(true, "J01,E01", 5), false, true);
        var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var whiteHand = (System.Collections.IList)typeof(CardHandManager).GetField("whiteHand", fields).GetValue(hand);
        var blackHand = (System.Collections.IList)typeof(CardHandManager).GetField("blackHand", fields).GetValue(hand);
        var whiteDeck = (System.Collections.IList)typeof(CardHandManager).GetField("whiteDeck", fields).GetValue(hand);
        var blackDeck = (System.Collections.IList)typeof(CardHandManager).GetField("blackDeck", fields).GetValue(hand);
        Check(whiteHand.Count == 2 && blackHand.Count == 0, "Client stores only its own hand");
        Check(whiteDeck.Count == 0 && blackDeck.Count == 0, "Client stores neither deck order");
        Check(!hand.CanDrawThisTurn, "Client applies authoritative draw permission");
        Check((int)typeof(CardHandManager).GetField("remoteOpponentHandCount", fields).GetValue(hand) == 5,
            "Opponent backs use public count without card definitions");
        hand.ResetHands(logic.isWhiteTurn);
    }

    /// <summary>驗證拒絕拖曳的後續事件不改動 UI，以及拖曳途中鎖定時能還原卡片。</summary>
    private static void VerifyDragGate(LogicManager logic, CardHandManager hand)
    {
        var card = new GameObject("DragGateFixture", typeof(RectTransform), typeof(CanvasGroup), typeof(CardDragHandler));
        var parent = new GameObject("DragGateParent", typeof(RectTransform));
        parent.transform.SetParent(hand.CardGameUiRoot, false);
        card.transform.SetParent(parent.transform, false);
        var rect = card.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(17f, 23f);
        var group = card.GetComponent<CanvasGroup>();
        var drag = card.GetComponent<CardDragHandler>();
        drag.Initialize(hand, hand.cardLibrary.GetCard("J01"));
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left, position = new Vector2(300f, 300f) };
        logic.PushOperationLock("DragGateTest");
        try
        {
            drag.OnBeginDrag(pointer);
            drag.OnDrag(pointer);
            drag.OnEndDrag(pointer);
            Check(card.transform.parent == parent.transform && rect.anchoredPosition == new Vector2(17f, 23f),
                "Rejected drag callbacks leave card in original hand position");
            Check(group.alpha == 1f && group.blocksRaycasts, "Rejected drag preserves card readability and interaction");
            Check(!drag.RecycleFromDropZone(), "Rejected drag cannot recycle through drop-zone callback");
        }
        finally { logic.PopOperationLock("DragGateTest"); }
        try
        {
            drag.OnBeginDrag(pointer);
            Check(card.transform.parent == hand.CardGameUiRoot && group.alpha == 0.5f && !group.blocksRaycasts,
                "Eligible card can begin normal drag");
            logic.isPromotionActive = true;
            drag.OnEndDrag(pointer);
            Check(card.transform.parent == parent.transform && rect.anchoredPosition == new Vector2(17f, 23f) &&
                group.alpha == 1f && group.blocksRaycasts, "New operation restriction restores dragged card");
        }
        finally
        {
            logic.isPromotionActive = false;
            Object.Destroy(card);
            Object.Destroy(parent);
        }
    }

    /// <summary>驗證手牌光暈與拖曳目標使用實際規則，而且查詢不消耗卡牌或改寫棋子。</summary>
    private static void VerifyCardPreviews(LogicManager logic, CardHandManager hand, FieldCardPlace[] places)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var openingField = typeof(CardHandManager).GetField("openingHandSize", flags);
        int originalOpening = (int)openingField.GetValue(hand);
        openingField.SetValue(hand, 4);
        try { Check(hand.SetDeckForPlayerAsAuthority(true, "J01,E01,F01,F12", true), "Create preview hand fixture"); }
        finally { openingField.SetValue(hand, originalOpening); }
        CardDefinition job = hand.cardLibrary.GetCard("J01");
        CardDefinition dismissal = hand.cardLibrary.GetCard("E01");
        CardDefinition field = hand.cardLibrary.GetCard("F01");
        CardDefinition advanced = hand.cardLibrary.GetCard("F12");
        var targets = new System.Collections.Generic.List<Component>();
        hand.CollectCardTargets(job, targets);
        Check(targets.Count == 8 && targets.All(p => p is Pawn pawn && pawn.IsWhite), "Job targets only eligible friendly pawns");
        Check(hand.HasPlayableCardTarget(job), "Playable job enables hand glow");
        Check(hand.GetCardUnavailableReason(job) == null, "Playable card has no unavailable hint");
        hand.CollectCardTargets(dismissal, targets);
        Check(hand.CanPreviewCard(dismissal) && targets.Count == 0 && !hand.HasPlayableCardTarget(dismissal), "Dismissal without equipped pieces has no glow");
        Check(hand.GetCardUnavailableReason(dismissal) == "目前沒有符合這張卡牌條件的棋子", "Unavailable card explains missing eligible pieces");
        CardUnavailableHint hover = hand.GetComponentsInChildren<CardDragHandler>(true)
            .First(d => d.gameObject.activeInHierarchy && (CardDefinition)typeof(CardDragHandler).GetField("cardDefinition", flags).GetValue(d) == dismissal)
            .GetComponent<CardUnavailableHint>();
        var hoverPointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        { position = new Vector2(300, 300) };
        hover.OnPointerEnter(hoverPointer);
        typeof(CardUnavailableHint).GetField("showAt", flags).SetValue(hover, 0f);
        typeof(CardUnavailableHint).GetMethod("Update", flags).Invoke(hover, null);
        var hoverPanel = (RectTransform)typeof(CardUnavailableHint).GetField("panel", flags).GetValue(hover);
        Check(hoverPanel != null && hoverPanel.gameObject.activeSelf &&
            hoverPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(g => !g.raycastTarget),
            "Unavailable hover shows text without blocking input");
        hover.OnPointerExit(hoverPointer);
        Check(!hoverPanel.gameObject.activeSelf, "Pointer exit immediately hides unavailable hint");
        hand.CollectCardTargets(field, targets);
        Check(targets.Count == places.Length, "Field previews both empty slots");
        places[0].SetCard(field);
        try
        {
            hand.CollectCardTargets(field, targets);
            Check(targets.Count == 1 && !targets.Contains(places[0]), "Occupied field slot cannot be highlighted");
            hand.CollectCardTargets(advanced, targets);
            Check(targets.Count == 0, "Advanced field requires two empty slots");
            Check(hand.GetCardUnavailableReason(advanced) == "空的場地欄位不足", "Advanced field explains insufficient space");
        }
        finally { places[0].Clear(); }
        Check(logic.boardMap[0, 1].cardDefinition == null && hand.CanPreviewCard(job), "Preview leaves pieces and hand unchanged");
        CardDragHandler drag = hand.GetComponentsInChildren<CardDragHandler>(true)
            .First(d => d.gameObject.activeInHierarchy && (CardDefinition)typeof(CardDragHandler).GetField("cardDefinition", flags).GetValue(d) == job);
        typeof(CardDragHandler).GetMethod("LateUpdate", flags).Invoke(drag, null);
        Check(drag.GetComponentInChildren<CardGlowGraphic>(true).enabled, "Live card renderer shows playable glow");
        CardGlowGraphic glow = drag.GetComponentInChildren<CardGlowGraphic>(true);
        Check(glow.GetComponent<CanvasRenderer>() != null, "Glow has required CanvasRenderer before clipping");
        Vector4 glowPadding = drag.GetComponent<UnityEngine.UI.RectMask2D>().padding;
        Check(glowPadding.x <= -24f && glowPadding.y <= -24f && glowPadding.z <= -24f && glowPadding.w <= -24f,
            "Card clipping includes the full outer glow");
        glow.SetClipRect(new Rect(-1000, -1000, 2000, 2000), true);
        glow.SetClipRect(default, false);
        Canvas.ForceUpdateCanvases();
        Check(drag.GetComponent<UnityEngine.UI.Image>().canvasRenderer != null && drag.gameObject.activeInHierarchy,
            "Card remains visible after glow clipping and canvas rebuild");
        logic.PushOperationLock("PreviewTest");
        try
        {
            typeof(CardDragHandler).GetMethod("LateUpdate", flags).Invoke(drag, null);
            Check(!drag.GetComponentInChildren<CardGlowGraphic>(true).enabled, "Operation lock immediately hides hand glow");
            hand.CollectCardTargets(job, targets);
            Check(targets.Count == 0, "Operation lock prevents target previews");
            Check(hand.GetCardUnavailableReason(job) == hand.GetCardInteractionBlockReason(), "Hover shares current operation lock reason");
        }
        finally { logic.PopOperationLock("PreviewTest"); }
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left, position = new Vector2(300, 300) };
        drag.OnBeginDrag(pointer);
        CardTargetFeedback feedback = Object.FindFirstObjectByType<CardTargetFeedback>();
        Check(feedback != null && feedback.GetComponentsInChildren<LineRenderer>().Length == 8, "Live drag creates eight legal target rings");
        logic.isPromotionActive = true;
        try
        {
            drag.OnEndDrag(pointer);
            Check(feedback.GetComponentsInChildren<LineRenderer>().Length == 0, "Ending drag immediately hides all world markers");
        }
        finally { logic.isPromotionActive = false; }
        drag.OnBeginDrag(pointer);
        pointer.position = new Vector2(-10000f, -10000f);
        drag.OnEndDrag(pointer);
        Check(typeof(CardDragHandler).GetField("returnRoutine", flags).GetValue(drag) != null &&
            !drag.GetComponent<CanvasGroup>().blocksRaycasts && hand.CanPreviewCard(job),
            "Invalid drop starts return animation without consuming card");
        Check(typeof(CardDragHandler).GetField("returnSlot", flags).GetValue(drag) != null,
            "Returning card reserves its hand layout slot");
        int feedbackCount = Object.FindObjectsByType<CardPlayFeedback>(FindObjectsSortMode.None).Length;
        Check(hand.PlayCardOnPieceAsAuthority(true, job.id, new BoardCoordinate(0, 1)), "Valid authority card play accepted");
        Check(Object.FindObjectsByType<CardPlayFeedback>(FindObjectsSortMode.None).Length == feedbackCount + 1,
            "Confirmed card play creates one success flash");
        Check(!hand.PlayCardOnPieceAsAuthority(true, job.id, new BoardCoordinate(0, 1)) &&
            Object.FindObjectsByType<CardPlayFeedback>(FindObjectsSortMode.None).Length == feedbackCount + 1,
            "Rejected card play does not create success flash");
        hand.ResetHands(logic.isWhiteTurn);
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

    /// <summary>執行真實走法驗證吃過路兵的移除位置及回合切換。</summary>
    private static void VerifyEnPassant(LogicManager logic)
    {
        Check(!logic.TryExecuteNetworkMove(new BoardCoordinate(4, 1), new BoardCoordinate(4, 4), true), "Reject pawn three-square move");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(4, 1), new BoardCoordinate(4, 3), true), "EP e2-e4");
        Check(!logic.TryExecuteNetworkMove(new BoardCoordinate(3, 1), new BoardCoordinate(3, 3), true), "Reject moving wrong side");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(0, 6), new BoardCoordinate(0, 5), false), "EP a7-a6");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(4, 3), new BoardCoordinate(4, 4), true), "EP e4-e5");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(3, 6), new BoardCoordinate(3, 4), false), "EP d7-d5");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(4, 4), new BoardCoordinate(3, 5), true), "EP e5xd6");
        Check(logic.boardMap[3, 4] == null && logic.boardMap[3, 5] is Pawn && !logic.isWhiteTurn,
            "En passant removes adjacent pawn and advances turn");
    }

    /// <summary>清出王翼路徑後驗證易位會同時移動王與車。</summary>
    private static void VerifyCastling(LogicManager logic)
    {
        Check(!logic.TryExecuteNetworkMove(new BoardCoordinate(4, 0), new BoardCoordinate(6, 0), true), "Reject blocked castling");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(4, 1), new BoardCoordinate(4, 3), true), "Castle e2-e4");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(0, 6), new BoardCoordinate(0, 5), false), "Castle a7-a6");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(6, 0), new BoardCoordinate(5, 2), true), "Castle Ng1-f3");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(0, 5), new BoardCoordinate(0, 4), false), "Castle a6-a5");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(5, 0), new BoardCoordinate(4, 1), true), "Castle Bf1-e2");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(1, 6), new BoardCoordinate(1, 5), false), "Castle b7-b6");
        Check(logic.TryExecuteNetworkMove(new BoardCoordinate(4, 0), new BoardCoordinate(6, 0), true), "Kingside castling accepted");
        Check(logic.boardMap[6, 0] is King && logic.boardMap[5, 0] is Rook &&
            logic.boardMap[4, 0] == null && logic.boardMap[7, 0] == null, "Castling relocates king and rook");
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
