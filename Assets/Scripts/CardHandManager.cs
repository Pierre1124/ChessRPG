using System.Collections.Generic;
using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CardRecycleEvent
{
    public CardDefinition card;
    public bool isWhitePlayer;
    public int frame;

    /// <summary>
    /// 建立包含回收卡牌、玩家陣營與影格的事件資料。
    /// </summary>
    public CardRecycleEvent(
        CardDefinition recycledCard,
        bool recycledByWhitePlayer,
        int recycledFrame
    )
    {
        card = recycledCard;
        isWhitePlayer = recycledByWhitePlayer;
        frame = recycledFrame;
    }
}

public sealed class CardDrawEvent
{
    public bool isWhitePlayer;
    public int cardsDrawn;
    public int frame;

    /// <summary>
    /// 建立包含抽牌玩家、實際張數與影格的事件資料。
    /// </summary>
    public CardDrawEvent(
        bool drawnByWhitePlayer,
        int drawnCount,
        int drawnFrame
    )
    {
        isWhitePlayer = drawnByWhitePlayer;
        cardsDrawn = drawnCount;
        frame = drawnFrame;
    }
}

public class CardHandManager : MonoBehaviour
{
    public event System.Action<CardRecycleEvent> CardRecycled;
    public event System.Action<CardDrawEvent> CardsDrawn;

    private sealed class PendingCard
    {
        public CardDefinition card;
        public bool isWhitePlayer;
        public int ownerTurnsRemaining;
    }

    [Header("Cards")]
    [SerializeField] public ChessCard cardLibrary;
    private readonly List<CardDefinition> startingDeck =
        new List<CardDefinition>();
    [SerializeField] private GameObject cardPrefab;
    [SerializeField, Min(0.01f)] private float cardScale = 1f;
    [SerializeField] private int cardsDrawnEachTurn = 1;
    [SerializeField] private int openingHandSize = 1;

    [Header("CardGameUI Children")]
    [SerializeField] private Transform cardGameUiRoot;
    [SerializeField] private RectTransform handRoot;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private Button drawButton;
    [SerializeField] private Slider whiteHealthSlider;
    [SerializeField] private Slider blackHealthSlider;
    [SerializeField] private TMP_Text whiteHealthText;
    [SerializeField] private TMP_Text blackHealthText;
    [SerializeField] private RectTransform recycleCardRoot;
    [SerializeField] private GameObject usingCardRoot;
    [SerializeField, Min(0.01f)] private float playedCardShrinkSeconds = 0.35f;
    [SerializeField] private string usingCardShowStateName = "UsingCard_2";
    [SerializeField] private string usingCardShrinkStateName = "UsingCard_2";
    [SerializeField] private Vector2 usingCardTargetStartOffset =
        new Vector2(0f, 160f);
    [SerializeField] private bool usingCardFaceCameraInWorldSpace = true;
    [SerializeField] private bool usingCardInvertCameraFacing;
    [SerializeField] private int usingCardSortingOrder = 32000;
    [SerializeField] private Vector2 usingCardWorldFallbackSize =
        new Vector2(768f, 1024f);
    [SerializeField] private Vector3 usingCardWorldFallbackScale =
        new Vector3(0.002f, 0.002f, 0.002f);
    [FormerlySerializedAs("opponentHandRoot")]
    [SerializeField] private Transform whiteViewOpponentHandRoot;
    [SerializeField] private Transform blackViewOpponentHandRoot;
    [SerializeField] private GameObject opponentCardBackTemplate;

    private readonly List<CardDefinition> whiteDeck =
        new List<CardDefinition>();
    private readonly List<CardDefinition> blackDeck =
        new List<CardDefinition>();
    private readonly List<CardDefinition> whiteHand =
        new List<CardDefinition>();
    private readonly List<CardDefinition> blackHand =
        new List<CardDefinition>();
    private readonly List<PendingCard> pendingCards =
        new List<PendingCard>();

    private LogicManager logicManager;
    private MultiplayerGameController multiplayerGameController;
    private bool isInitialized;
    private bool canDrawThisTurn;
    private bool preserveOpeningCard;
    private float waitingTextNextUpdateTime;
    private int waitingTextDotCount = 1;
    private bool wasWaitingForPlayer;
    private bool isUsingCardAnimationPlaying;
    private Transform lastPlayedCardTargetTransform;

    public Transform CardGameUiRoot
    {
        get { return cardGameUiRoot; }
    }

    public RectTransform RecycleCardRoot
    {
        get { return recycleCardRoot; }
    }

    public bool CanDrawThisTurn
    {
        get { return !IsRpgDisabled && canDrawThisTurn; }
    }

    private bool rpgDisabled;
    private int remoteOpponentHandCount = -1;
    private bool IsRpgDisabled
    {
        get { return rpgDisabled || (logicManager != null && logicManager.IsClassicChess); }
    }

    /// <summary>
    /// 清空牌堆與手牌並關閉 RPG 介面，包含棋盤上獨立的對手牌背容器。
    /// </summary>
    public void DisableRpgElements()
    {
        rpgDisabled = true;
        StopAllCoroutines();
        canDrawThisTurn = false;
        remoteOpponentHandCount = -1;
        whiteDeck.Clear();
        blackDeck.Clear();
        whiteHand.Clear();
        blackHand.Clear();
        pendingCards.Clear();
        ResolveCardGameUiReferences();
        ResolveOpponentHandReferences();
        ClearOpponentHandCards();
        HideOpponentHandDisplay(whiteViewOpponentHandRoot);
        HideOpponentHandDisplay(blackViewOpponentHandRoot);
        if (usingCardRoot != null) usingCardRoot.SetActive(false);
        if (drawButton != null) drawButton.interactable = false;
        SetCardGameUiActive(false);
        enabled = false;
    }

    /// <summary>
    /// 從正規化後的 Panel 找回手牌展示根物件，一併隱藏牌背外框與其他裝飾。
    /// </summary>
    private static void HideOpponentHandDisplay(Transform root)
    {
        if (root == null) return;
        for (Transform current = root; current != null; current = current.parent)
        {
            if (current.name == "WhiteHandCard" || current.name == "BlackHandCard" ||
                current.name == "MatchHandCard" || current.name == "BlackViewMatchHandCard" ||
                current.name == "WhiteMatchHandCard")
            {
                current.gameObject.SetActive(false);
                return;
            }
        }
        root.gameObject.SetActive(false);
    }

    public Transform LastPlayedCardTargetTransform
    {
        get { return lastPlayedCardTargetTransform; }
    }

    /// <summary>
    /// 設定卡牌遊戲介面的顯示狀態。
    /// </summary>
    public void SetCardGameUiActive(bool active)
    {
        ResolveCardGameUiReferences();
        if (cardGameUiRoot != null)
        {
            cardGameUiRoot.gameObject.SetActive(active && !IsRpgDisabled);
        }
    }

    public string LocalSerializedDeckIds
    {
        get
        {
            ResolveDefaults();
            return cardLibrary != null
                ? DeckStorage.BuildSerializedDeckIds(cardLibrary.Cards)
                : string.Empty;
        }
    }

    public bool LocalDeckHasOpeningCard
    {
        get
        {
            ResolveDefaults();
            return cardLibrary != null &&
                DeckStorage.HasOpeningCard(cardLibrary.Cards);
        }
    }

    /// <summary>
    /// 保存對局引用、補齊 UI 與卡牌資源並綁定抽牌操作。
    /// </summary>
    public void Initialize(LogicManager owner)
    {
        logicManager = owner;
        multiplayerGameController =
            FindFirstObjectByType<MultiplayerGameController>();
        ResolveDefaults();
        ResolveCardGameUiReferences();
        if (usingCardRoot != null)
        {
            usingCardRoot.SetActive(false);
        }
        BindDrawButton();
        isInitialized = true;
    }

    /// <summary>
    /// 監看連線等待狀態，更新等待提示並在準備完成時刷新手牌。
    /// </summary>
    private void Update()
    {
        bool isWaiting =
            multiplayerGameController != null &&
            !multiplayerGameController.CanGameplayOperate;

        if (wasWaitingForPlayer && !isWaiting && logicManager != null)
        {
            wasWaitingForPlayer = false;
            Refresh(logicManager.isWhiteTurn);
            return;
        }

        wasWaitingForPlayer = isWaiting;

        if (
            isWaiting
        )
        {
            RefreshWaitingForPlayerText();

            if (drawButton != null)
            {
                drawButton.interactable = false;
            }
        }
    }

    /// <summary>
    /// 判斷目前是否因結算、升變或對局狀態而禁止卡牌操作。
    /// </summary>
    private bool IsGameplayLocked()
    {
        return IsRpgDisabled || IsMultiplayerWaiting() ||
            isUsingCardAnimationPlaying ||
            (logicManager != null && logicManager.IsOperationLocked);
    }

    /// <summary>拖曳前檢查對局與操作資格，拒絕時說明原因而不關閉手牌查看。</summary>
    public bool TryBeginCardDrag()
    {
        string reason = GetCardInteractionBlockReason();
        if (reason == null) return true;
        ShowAlarm(reason);
        return false;
    }

    /// <summary>查詢操作限制而不顯示提示，供手牌光暈及拖曳預覽共用。</summary>
    public string GetCardInteractionBlockReason()
    {
        string reason = null;
        if (IsRpgDisabled) reason = "普通西洋棋模式不能使用卡牌";
        else if (logicManager == null) reason = "對局尚未準備完成";
        else if (Time.timeScale == 0f) reason = "對局已暫停或結束，現在不能使用卡牌";
        else if (IsMultiplayerWaiting())
            reason = multiplayerGameController.IsWaitingForPlayer ? "請等待另一位玩家加入" : "正在同步對局，請稍候";
        else if (!CanLocalPlayerUseCurrentTurnControls()) reason = "尚未輪到你，請等待對手完成回合";
        else if (logicManager.isPromotionActive) reason = "請先完成棋子升變";
        else if (isUsingCardAnimationPlaying || logicManager.IsOperationLocked || logicManager.IsFieldFusionPlaying)
            reason = "請等待目前演出或結算完成";

        return reason;
    }

    /// <summary>確認卡牌仍在目前玩家手牌內，而且對局允許操作。</summary>
    public bool CanPreviewCard(CardDefinition card)
    {
        return card != null && GetCardInteractionBlockReason() == null &&
            (logicManager.isWhiteTurn ? whiteHand : blackHand).Contains(card);
    }

    /// <summary>查詢手牌不可用原因，沿用操作限制、目標條件及場地驗證，不執行卡牌效果。</summary>
    public string GetCardUnavailableReason(CardDefinition card)
    {
        string blocked = GetCardInteractionBlockReason();
        if (blocked != null) return blocked;
        if (!CanPreviewCard(card)) return "這張卡牌已不在目前可操作的手牌中";
        CollectCardTargets(card, previewTargets);
        if (previewTargets.Count > 0) return null;
        if (card.cardType == CardType.Field)
        {
            string fallback = "目前沒有可使用的場地欄位";
            foreach (FieldCardPlace place in logicManager.GetFieldPlaces())
            {
                logicManager.CanPlayFieldCard(card, place, out string reason);
                if (!string.IsNullOrEmpty(reason)) fallback = reason;
                if (place != null && place.isActiveAndEnabled && place.ActiveCard == null) return fallback;
            }
            return fallback;
        }
        return "目前沒有符合這張卡牌條件的棋子";
    }

    /// <summary>列出合法棋子或場地，只查詢條件，不消耗手牌或觸發技能。</summary>
    public void CollectCardTargets(CardDefinition card, List<Component> targets)
    {
        targets.Clear();
        if (!CanPreviewCard(card)) return;
        if (card.cardType == CardType.Field)
        {
            foreach (FieldCardPlace place in logicManager.GetFieldPlaces())
                if (logicManager.CanPlayFieldCard(card, place, out _)) targets.Add(place);
            return;
        }
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                Piece piece = logicManager.boardMap[x, y];
                if (piece != null && piece.gameObject.activeInHierarchy &&
                    (card.cardType == CardType.Event || piece.IsWhite == logicManager.isWhiteTurn) &&
                    card.CanApplyTo(piece, logicManager)) targets.Add(piece);
            }
    }

    private readonly Dictionary<CardDefinition, bool> playableCardCache = new Dictionary<CardDefinition, bool>();
    private readonly List<Component> previewTargets = new List<Component>();
    private float nextPreviewRefresh;
    public bool IsWhiteCardTurn { get { return logicManager != null && logicManager.isWhiteTurn; } }

    /// <summary>重複手牌共用短期快取；操作鎖與手牌歸屬仍每幀檢查。</summary>
    public bool HasPlayableCardTarget(CardDefinition card)
    {
        if (!CanPreviewCard(card)) return false;
        if (Time.unscaledTime >= nextPreviewRefresh)
        {
            playableCardCache.Clear();
            nextPreviewRefresh = Time.unscaledTime + 0.15f;
        }
        if (!playableCardCache.TryGetValue(card, out bool playable))
        {
            CollectCardTargets(card, previewTargets);
            playable = previewTargets.Count > 0;
            playableCardCache[card] = playable;
        }
        return playable;
    }

    /// <summary>
    /// 判斷多人對局是否仍在等待玩家或必要資料。
    /// </summary>
    private bool IsMultiplayerWaiting()
    {
        if (multiplayerGameController == null)
        {
            multiplayerGameController =
                FindFirstObjectByType<MultiplayerGameController>();
        }

        return multiplayerGameController != null &&
            !multiplayerGameController.CanGameplayOperate;
    }

    /// <summary>
    /// 依多人連線等待狀態更新提示文字。
    /// </summary>
    private void RefreshWaitingForPlayerText(bool force = false)
    {
        ResolveCardGameUiReferences();

        if (turnText == null)
        {
            return;
        }

        if (!force && Time.unscaledTime < waitingTextNextUpdateTime)
        {
            return;
        }

        waitingTextNextUpdateTime = Time.unscaledTime + 0.45f;
        waitingTextDotCount++;
        if (waitingTextDotCount > 3)
        {
            waitingTextDotCount = 1;
        }

        string waitingReason = multiplayerGameController != null &&
            !multiplayerGameController.IsWaitingForPlayer ? "正在同步牌組" : "等待玩家加入";
        turnText.text = waitingReason + new string('.', waitingTextDotCount);
    }

    /// <summary>
    /// 重建雙方牌堆與起始手牌，並重設抽牌狀態。
    /// </summary>
    public void ResetHands(bool isWhiteTurn)
    {
        if (IsRpgDisabled) return;
        ResolveDefaults();

        remoteOpponentHandCount = -1;
        whiteDeck.Clear();
        blackDeck.Clear();
        whiteHand.Clear();
        blackHand.Clear();
        pendingCards.Clear();

        whiteDeck.AddRange(startingDeck);
        blackDeck.AddRange(startingDeck);
        Shuffle(whiteDeck, preserveOpeningCard);
        Shuffle(blackDeck, preserveOpeningCard);

        DrawCards(true, openingHandSize);
        DrawCards(false, openingHandSize);
        canDrawThisTurn = true;
        Refresh(isWhiteTurn);
        RefreshHealth();
    }

    /// <summary>
    /// 套用指定玩家提交的牌組，洗牌並建立起始手牌。
    /// </summary>
    public bool SetDeckForPlayerAsAuthority(
        bool isWhitePlayer,
        string serializedDeckIds,
        bool keepFirstCard
    )
    {
        if (IsRpgDisabled) return false;
        ResolveDefaults();

        List<CardDefinition> targetDeck =
            isWhitePlayer ? whiteDeck : blackDeck;
        List<CardDefinition> targetHand =
            isWhitePlayer ? whiteHand : blackHand;

        if (cardLibrary == null || !NetworkInputPolicy.TryParseDeck(serializedDeckIds,
            cardLibrary.GetCard, cardLibrary.Cards.Count, out List<CardDefinition> validatedDeck)) return false;
        targetDeck.Clear();
        targetHand.Clear();
        targetDeck.AddRange(validatedDeck);

        Shuffle(targetDeck, keepFirstCard);
        DrawCards(isWhitePlayer, openingHandSize);

        Debug.Log(
            $"[NetworkGame][DeckApplied] Player={(isWhitePlayer ? "White" : "Black")} | " +
            $"Deck={targetDeck.Count} | Hand={targetHand.Count} | Opening={keepFirstCard}"
        );

        if (logicManager != null)
        {
            Refresh(logicManager.isWhiteTurn);
        }

        return true;
    }

    /// <summary>
    /// 在棋盤回合切換時發放待領卡牌並更新抽牌與手牌介面。
    /// </summary>
    public void OnChessTurnChanged(bool isWhiteTurn)
    {
        if (IsRpgDisabled) return;
        if (!isInitialized)
        {
            Initialize(logicManager);
            return;
        }

        ReleasePendingCards(isWhiteTurn);
        canDrawThisTurn = true;
        Refresh(isWhiteTurn);
    }

    /// <summary>
    /// 停用本回合抽牌操作並更新介面。
    /// </summary>
    public void DisableDrawForCurrentTurn()
    {
        if (!canDrawThisTurn)
        {
            return;
        }

        canDrawThisTurn = false;
        if (logicManager != null)
        {
            Refresh(logicManager.isWhiteTurn);
        }
    }

    /// <summary>
    /// 將卡牌加入指定玩家的待發放清單。
    /// </summary>
    public bool QueueCardForPlayer(
        bool isWhitePlayer,
        CardDefinition card,
        int ownerTurnsDelay
    )
    {
        if (IsRpgDisabled) return false;
        if (card == null) return false;

        pendingCards.Add(new PendingCard
        {
            card = card,
            isWhitePlayer = isWhitePlayer,
            ownerTurnsRemaining = Mathf.Max(1, ownerTurnsDelay)
        });

        Debug.Log(
            $"[CardDebug][PendingCard] Player=" +
            $"{(isWhitePlayer ? "White" : "Black")} | " +
            $"Card={card.id} {card.cardName} | " +
            $"Delay={Mathf.Max(1, ownerTurnsDelay)} owner turn(s)"
        );
        return true;
    }

    /// <summary>
    /// 將符合發放時機的待領卡牌加入玩家手牌。
    /// </summary>
    private void ReleasePendingCards(bool isWhitePlayer)
    {
        List<CardDefinition> hand =
            isWhitePlayer ? whiteHand : blackHand;

        for (int i = pendingCards.Count - 1; i >= 0; i--)
        {
            PendingCard pending = pendingCards[i];
            if (pending == null || pending.isWhitePlayer != isWhitePlayer)
            {
                continue;
            }

            pending.ownerTurnsRemaining--;
            if (pending.ownerTurnsRemaining > 0) continue;

            hand.Add(pending.card);
            Debug.Log(
                $"[CardDebug][PendingCardReady] Player=" +
                $"{(isWhitePlayer ? "White" : "Black")} | " +
                $"Card={pending.card.id} {pending.card.cardName}"
            );
            pendingCards.RemoveAt(i);
        }
    }

    /// <summary>
    /// 處理目前玩家的抽牌操作；連線時送交主機執行。
    /// </summary>
    public void DrawForCurrentPlayer()
    {
        if (
            logicManager == null ||
            !canDrawThisTurn ||
            IsGameplayLocked()
        )
        {
            ShowAlarm("現在不能抽牌");
            return;
        }

        if (
            multiplayerGameController != null &&
            multiplayerGameController.IsOnline
        )
        {
            if (!multiplayerGameController.CanLocalPlayerAct(logicManager.isWhiteTurn))
            {
                Debug.Log(
                    "[NetworkGame][DrawRejected] Local player cannot draw on opponent turn."
                );
                ShowAlarm("不是你的回合");
                Refresh(logicManager.isWhiteTurn);
                return;
            }

            multiplayerGameController.SubmitCommand(
                multiplayerGameController.CreateDrawCardCommand()
            );
            return;
        }

        DrawForPlayerAsAuthority(logicManager.isWhiteTurn);
    }

    /// <summary>
    /// 檢查抽牌條件後抽取卡牌，觸發抽牌事件並更新本回合狀態。
    /// </summary>
    public bool DrawForPlayerAsAuthority(bool isWhitePlayer)
    {
        if (
            logicManager == null ||
            !canDrawThisTurn ||
            IsGameplayLocked() ||
            logicManager.isWhiteTurn != isWhitePlayer
        )
        {
            return false;
        }

        int drawnCount = DrawCards(logicManager.isWhiteTurn, cardsDrawnEachTurn);
        if (drawnCount > 0)
        {
            CardsDrawn?.Invoke(
                new CardDrawEvent(
                    logicManager.isWhiteTurn,
                    drawnCount,
                    Time.frameCount
                )
            );
        }

        canDrawThisTurn = false;
        Refresh(logicManager.isWhiteTurn);
        return true;
    }

    /// <summary>
    /// 嘗試回收卡牌；連線時轉成主機命令。
    /// </summary>
    public bool TryRecycleCard(CardDefinition card)
    {
        if (logicManager == null || card == null || IsGameplayLocked())
        {
            ShowAlarm("現在不能回收卡片");
            return false;
        }

        if (
            multiplayerGameController != null &&
            multiplayerGameController.IsOnline
        )
        {
            if (!multiplayerGameController.CanLocalPlayerAct(logicManager.isWhiteTurn))
            {
                Debug.Log(
                    "[NetworkGame][RecycleRejected] Local player cannot recycle on opponent turn."
                );
                ShowAlarm("不是你的回合");
                Refresh(logicManager.isWhiteTurn);
                return false;
            }

            multiplayerGameController.SubmitCommand(
                multiplayerGameController.CreateRecycleCardCommand(card)
            );
            return true;
        }

        return RecycleCardForPlayerAsAuthority(logicManager.isWhiteTurn, card.id);
    }

    /// <summary>
    /// 從指定玩家手牌移除回收卡，恢復抽牌資格並觸發回收事件。
    /// </summary>
    public bool RecycleCardForPlayerAsAuthority(
        bool isWhitePlayer,
        string cardId
    )
    {
        if (
            logicManager == null ||
            string.IsNullOrEmpty(cardId) ||
            IsGameplayLocked() ||
            logicManager.isWhiteTurn != isWhitePlayer
        )
        {
            return false;
        }

        CardDefinition card = cardLibrary != null
            ? cardLibrary.GetCard(cardId)
            : null;
        if (card == null)
        {
            return false;
        }

        List<CardDefinition> currentHand =
            isWhitePlayer ? whiteHand : blackHand;

        if (!currentHand.Remove(card))
        {
            return false;
        }

        canDrawThisTurn = true;
        CardRecycleEvent recycleEvent = new CardRecycleEvent(
            card,
            isWhitePlayer,
            Time.frameCount
        );

        Debug.Log(
            $"[CardDebug][RecycleCard] Player=" +
            $"{(isWhitePlayer ? "White" : "Black")} | " +
            $"Card={card.id} {card.cardName} | DrawReset=True"
        );

        CardRecycled?.Invoke(recycleEvent);
        Refresh(logicManager.isWhiteTurn);
        return true;
    }

    /// <summary>
    /// 依目前雙方血量更新卡牌介面的血條與文字。
    /// </summary>
    public void RefreshHealth()
    {
        if (IsRpgDisabled) return;
        if (logicManager == null)
        {
            return;
        }

        ResolveCardGameUiReferences();
        SetHealthUi(
            whiteHealthSlider,
            whiteHealthText,
            logicManager.whiteHealth
        );
        SetHealthUi(
            blackHealthSlider,
            blackHealthText,
            logicManager.blackHealth
        );
    }

    /// <summary>
    /// 依目前資料刷新此物件的顯示內容。
    /// </summary>
    public void Refresh(bool isWhiteTurn)
    {
        playableCardCache.Clear();
        if (IsRpgDisabled) return;
        ResolveCardGameUiReferences();

        if (IsMultiplayerWaiting())
        {
            RefreshWaitingForPlayerText(true);
            RenderHand(isWhiteTurn);
            RefreshOpponentHandDisplay(isWhiteTurn);

            if (drawButton != null)
            {
                drawButton.interactable = false;
            }
            return;
        }

        if (turnText != null)
        {
            turnText.text = isWhiteTurn
                ? "\u767D\u65B9\u56DE\u5408"
                : "\u9ED1\u65B9\u56DE\u5408";
        }

        RenderHand(isWhiteTurn);
        RefreshOpponentHandDisplay(isWhiteTurn);

        if (drawButton != null)
        {
            drawButton.interactable =
                canDrawThisTurn &&
                !IsGameplayLocked() &&
                CanLocalPlayerUseCurrentTurnControls();
        }
    }

    /// <summary>
    /// 序列化接收者手牌及對手張數，永不傳送牌堆順序。
    /// </summary>
    public string SerializeNetworkCardState(bool recipientWhite = false)
    {
        if (IsRpgDisabled) return string.Empty;
        return PrivateCardState.Encode(recipientWhite,
            SerializeCardList(recipientWhite ? whiteHand : blackHand),
            recipientWhite ? blackHand.Count : whiteHand.Count);
    }

    /// <summary>
    /// 解析主機傳來的私有手牌與公開張數，驗證完畢後一次套用。
    /// </summary>
    public void ApplyNetworkCardState(
        string serializedState,
        bool remoteCanDrawThisTurn,
        bool currentWhiteTurn
    )
    {
        if (IsRpgDisabled) return;
        ResolveDefaults();

        if (!PrivateCardState.TryDecode(serializedState, out PrivateCardState state)) return;
        if (multiplayerGameController != null && multiplayerGameController.IsOnline &&
            state.recipientWhite != (multiplayerGameController.LocalSide == PlayerSide.White)) return;
        var receivedHand = new List<CardDefinition>();
        if (!string.IsNullOrEmpty(state.handIds))
        {
            foreach (string id in state.handIds.Split(','))
            {
                CardDefinition card = cardLibrary.GetCard(id);
                if (card == null) return;
                receivedHand.Add(card);
            }
        }
        whiteDeck.Clear();
        blackDeck.Clear();
        whiteHand.Clear();
        blackHand.Clear();
        (state.recipientWhite ? whiteHand : blackHand).AddRange(receivedHand);
        remoteOpponentHandCount = state.opponentHandCount;
        canDrawThisTurn = remoteCanDrawThisTurn;
        Refresh(currentWhiteTurn);

        Debug.Log(
            $"[NetworkGame][CardStateSync] Turn={(currentWhiteTurn ? "White" : "Black")} | " +
            $"CanDraw={canDrawThisTurn} | " +
            $"WhiteHand={whiteHand.Count} | BlackHand={blackHand.Count}"
        );
    }

    /// <summary>
    /// 依牌堆順序將指定數量的卡牌移入手牌，回傳實際抽取數量。
    /// </summary>
    private int DrawCards(bool isWhitePlayer, int amount)
    {
        List<CardDefinition> deck =
            isWhitePlayer ? whiteDeck : blackDeck;

        List<CardDefinition> hand =
            isWhitePlayer ? whiteHand : blackHand;

        int drawnCount = 0;
        for (int i = 0; i < amount; i++)
        {
            if (deck.Count == 0)
            {
                return drawnCount;
            }

            hand.Add(deck[0]);
            Debug.Log(
                $"?賜? " +
                $"?拙振??{(isWhitePlayer ? "?賣" : "暺")} | " +
                $"?∠?={deck[0].cardName} | " +
                $"?拚??∠?={deck.Count - 1}"
            );
            deck.RemoveAt(0);
            drawnCount++;
        }

        return drawnCount;
    }

    /// <summary>
    /// 判斷本機玩家是否可操作目前回合的卡牌控制項。
    /// </summary>
    private bool CanLocalPlayerUseCurrentTurnControls()
    {
        if (IsRpgDisabled) return false;
        if (logicManager == null)
        {
            return false;
        }

        if (multiplayerGameController == null)
        {
            multiplayerGameController =
                FindFirstObjectByType<MultiplayerGameController>();
        }

        return multiplayerGameController == null ||
            !multiplayerGameController.IsOnline ||
            multiplayerGameController.CanLocalPlayerAct(logicManager.isWhiteTurn);
    }

    /// <summary>
    /// 啟動指定卡牌的出牌顯示動畫。
    /// </summary>
    public void PlayUsingCardAnimation(
        CardDefinition card,
        GameObject cardObject
    )
    {
        if (IsRpgDisabled) return;
        if (card == null)
        {
            if (cardObject != null)
            {
                Destroy(cardObject);
            }
            return;
        }

        StartCoroutine(
            PlayUsingCardRoutine(card, cardObject)
        );
    }

    /// <summary>
    /// 以指定棋子位置為目標播放出牌動畫。
    /// </summary>
    public bool PlayUsingCardAnimationOnPiece(
        string cardId,
        BoardCoordinate targetCoordinate
    )
    {
        if (IsRpgDisabled) return false;
        if (
            logicManager == null ||
            cardLibrary == null ||
            string.IsNullOrEmpty(cardId) ||
            !targetCoordinate.IsValid
        )
        {
            return false;
        }

        CardDefinition card = cardLibrary.GetCard(cardId);
        Piece target =
            logicManager.boardMap[targetCoordinate.x, targetCoordinate.y];
        if (card == null || target == null)
        {
            return false;
        }

        SetLastPlayedCardTarget(target.transform);
        PlayUsingCardAnimation(card, null);
        return true;
    }

    /// <summary>
    /// 以指定場地欄位為目標播放出牌動畫。
    /// </summary>
    public bool PlayUsingCardAnimationOnField(
        string cardId,
        string fieldPlaceName
    )
    {
        if (IsRpgDisabled) return false;
        if (
            cardLibrary == null ||
            string.IsNullOrEmpty(cardId) ||
            string.IsNullOrEmpty(fieldPlaceName)
        )
        {
            return false;
        }

        CardDefinition card = cardLibrary.GetCard(cardId);
        FieldCardPlace place = FindFieldCardPlace(fieldPlaceName);
        if (card == null || place == null)
        {
            return false;
        }

        SetLastPlayedCardTarget(place.transform);
        PlayUsingCardAnimation(card, null);
        return true;
    }

    /// <summary>
    /// 記錄最後出牌的目標，供後續動畫定位使用。
    /// </summary>
    private void SetLastPlayedCardTarget(Transform target)
    {
        lastPlayedCardTargetTransform = target;
    }

    /// <summary>
    /// 將卡牌清單轉成既有分隔格式的卡號字串。
    /// </summary>
    private string SerializeCardList(List<CardDefinition> cards)
    {
        if (cards == null || cards.Count == 0)
        {
            return string.Empty;
        }

        List<string> ids = new List<string>();
        foreach (CardDefinition card in cards)
        {
            ids.Add(card != null ? card.id : string.Empty);
        }

        return string.Join(",", ids);
    }

    /// <summary>
    /// 依目前可見的手牌清單重建手牌介面。
    /// </summary>
    private void RenderHand(bool isWhiteTurn)
    {
        if (handRoot == null)
        {
            return;
        }

        ClearChildren(handRoot);

        if (!TryGetVisibleHandSide(isWhiteTurn, out bool visibleWhiteHand))
        {
            return;
        }

        List<CardDefinition> hand =
            visibleWhiteHand ? whiteHand : blackHand;

        foreach (CardDefinition card in hand)
        {
            SpawnCard(card);
        }
    }

    /// <summary>
    /// 依對手手牌數量更新牌背顯示。
    /// </summary>
    private void RefreshOpponentHandDisplay(bool currentWhiteTurn)
    {
        ResolveOpponentHandReferences();
        Transform opponentHandRoot =
            ResolveActiveOpponentHandRoot(currentWhiteTurn);

        if (
            opponentHandRoot == null ||
            opponentCardBackTemplate == null
        )
        {
            return;
        }

        ClearOpponentHandCards();
        HideOpponentCardBackTemplateIfSceneObject();

        if (!TryGetOpponentHandSide(currentWhiteTurn, out bool opponentWhiteHand))
        {
            return;
        }

        int opponentHandCount =
            remoteOpponentHandCount >= 0 ? remoteOpponentHandCount :
            (opponentWhiteHand ? whiteHand.Count : blackHand.Count);

        for (int i = 0; i < opponentHandCount; i++)
        {
            GameObject cardBack =
                Instantiate(opponentCardBackTemplate, opponentHandRoot);
            cardBack.name = "CardBack";
            cardBack.SetActive(true);
        }
    }

    /// <summary>
    /// 若牌背樣板是場景物件，將其隱藏以免與複製出的牌背重疊。
    /// </summary>
    private void HideOpponentCardBackTemplateIfSceneObject()
    {
        if (
            opponentCardBackTemplate == null ||
            opponentCardBackTemplate.transform == null
        )
        {
            return;
        }

        bool isSceneTemplate =
            IsChildOf(opponentCardBackTemplate.transform, whiteViewOpponentHandRoot) ||
            IsChildOf(opponentCardBackTemplate.transform, blackViewOpponentHandRoot);

        if (isSceneTemplate)
        {
            opponentCardBackTemplate.SetActive(false);
        }
    }

    /// <summary>
    /// 清除對手手牌顯示物件。
    /// </summary>
    private void ClearOpponentHandCards()
    {
        ClearOpponentHandCardsInRoot(whiteViewOpponentHandRoot);
        ClearOpponentHandCardsInRoot(blackViewOpponentHandRoot);
    }

    /// <summary>
    /// 清除指定手牌容器內建立的對手牌背。
    /// </summary>
    private void ClearOpponentHandCardsInRoot(Transform opponentHandRoot)
    {
        if (opponentHandRoot == null)
        {
            return;
        }

        for (int i = opponentHandRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = opponentHandRoot.GetChild(i);
            if (
                opponentCardBackTemplate != null &&
                child.gameObject == opponentCardBackTemplate
            )
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// 依本機視角選擇目前使用的對手手牌容器。
    /// </summary>
    private Transform ResolveActiveOpponentHandRoot(bool currentWhiteTurn)
    {
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
            if (multiplayerGameController.LocalSide == PlayerSide.Black)
            {
                return blackViewOpponentHandRoot != null
                    ? blackViewOpponentHandRoot
                    : whiteViewOpponentHandRoot;
            }

            return whiteViewOpponentHandRoot != null
                ? whiteViewOpponentHandRoot
                : blackViewOpponentHandRoot;
        }

        bool viewerIsWhite = currentWhiteTurn;
        return viewerIsWhite
            ? whiteViewOpponentHandRoot
            : (
                blackViewOpponentHandRoot != null
                    ? blackViewOpponentHandRoot
                    : whiteViewOpponentHandRoot
            );
    }

    /// <summary>
    /// 嘗試取得目前應顯示牌背的對手陣營。
    /// </summary>
    private bool TryGetOpponentHandSide(
        bool currentWhiteTurn,
        out bool opponentWhiteHand
    )
    {
        opponentWhiteHand = !currentWhiteTurn;

        if (multiplayerGameController == null)
        {
            multiplayerGameController =
                FindFirstObjectByType<MultiplayerGameController>();
        }

        if (
            multiplayerGameController == null ||
            !multiplayerGameController.IsOnline
        )
        {
            return true;
        }

        PlayerSide side = multiplayerGameController.LocalSide;
        if (side == PlayerSide.White)
        {
            opponentWhiteHand = false;
            return true;
        }

        if (side == PlayerSide.Black)
        {
            opponentWhiteHand = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 嘗試取得目前可顯示手牌正面的玩家陣營。
    /// </summary>
    private bool TryGetVisibleHandSide(
        bool currentWhiteTurn,
        out bool visibleWhiteHand
    )
    {
        visibleWhiteHand = currentWhiteTurn;

        if (multiplayerGameController == null)
        {
            multiplayerGameController =
                FindFirstObjectByType<MultiplayerGameController>();
        }

        if (
            multiplayerGameController == null ||
            !multiplayerGameController.IsOnline
        )
        {
            return true;
        }

        PlayerSide side = multiplayerGameController.LocalSide;
        if (side == PlayerSide.White)
        {
            visibleWhiteHand = true;
            return true;
        }

        if (side == PlayerSide.Black)
        {
            visibleWhiteHand = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 依卡牌定義建立卡片物件並套用顯示資料。
    /// </summary>
    private void SpawnCard(CardDefinition card)
    {
        if (cardPrefab == null || handRoot == null || card == null)
        {
            return;
        }

        GameObject cardObject = Instantiate(cardPrefab, handRoot);
        cardObject.name = card.cardName;
        cardObject.transform.localScale =
            Vector3.one * cardScale;

        ApplyCardData(cardObject, card);

        CardDragHandler dragHandler =
            cardObject.GetComponent<CardDragHandler>();

        if (dragHandler == null)
        {
            Debug.LogError(
                $"{cardObject.name} prefab requires CardDragHandler."
            );
            Destroy(cardObject);
            return;
        }

        dragHandler.Initialize(this, card);
    }

    /// <summary>
    /// 依序播放出牌展示及目標收束動畫，並處理完成後清理。
    /// </summary>
    private IEnumerator PlayUsingCardRoutine(
        CardDefinition card,
        GameObject cardObject
    )
    {
        isUsingCardAnimationPlaying = true;
        logicManager?.PushOperationLock("UsingCard");
        Refresh(logicManager != null && logicManager.isWhiteTurn);

        try
        {
            ResolveCardGameUiReferences();

            if (cardObject != null)
            {
                Destroy(cardObject);
            }

            Vector3 usingCardStartPosition = Vector3.zero;
            Vector3 usingCardStartScale = Vector3.one;
            bool hasUsingCardTransform = usingCardRoot != null;
            if (hasUsingCardTransform)
            {
                usingCardStartPosition = usingCardRoot.transform.position;
                usingCardStartScale = usingCardRoot.transform.localScale;
            }

            float usingCardDuration = PlayUsingCardRoot(card);
            if (usingCardDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(usingCardDuration);
            }

            if (usingCardRoot != null)
            {
                usingCardRoot.SetActive(false);

                GameObject targetClone =
                    CreateUsingCardTargetClone(card);

                if (targetClone != null)
                {
                    yield return PlayUsingCardShrinkOnTarget(targetClone);
                    Destroy(targetClone);
                }
                else
                {
                    yield return ShrinkUsingCardCloneToLocalZero(
                        usingCardRoot
                    );
                }

                usingCardRoot.transform.position = usingCardStartPosition;
                usingCardRoot.transform.localScale = usingCardStartScale;
                usingCardRoot.SetActive(false);
            }

            while (
                logicManager != null &&
                logicManager.IsFullGameRestartDeferralActive &&
                !logicManager.HasDeferredFullGameRestart &&
                (
                    logicManager.GetActiveFieldCount("F12") > 0 ||
                    logicManager.IsFieldFusionPlaying
                )
            )
            {
                yield return null;
            }

            if (logicManager != null && logicManager.HasDeferredFullGameRestart)
            {
                // 先完成模式切換與重載，避免停用自身物件使這個協程提前中止。
                logicManager.CompleteDeferredFullGameRestart();
            }
            else
            {
                logicManager?.ClearFullGameRestartDeferral();
            }
        }
        finally
        {
            logicManager?.PopOperationLock("UsingCard");
            isUsingCardAnimationPlaying = false;

            if (
                logicManager != null &&
                cardGameUiRoot != null &&
                cardGameUiRoot.gameObject.activeInHierarchy
            )
            {
                Refresh(logicManager.isWhiteTurn);
            }
        }
    }

    /// <summary>
    /// 播放出牌展示根物件的動畫。
    /// </summary>
    private float PlayUsingCardRoot(CardDefinition card)
    {
        if (usingCardRoot == null)
        {
            return 0f;
        }

        ApplyCardData(usingCardRoot, card);
        BringUsingCardToFront(usingCardRoot);
        usingCardRoot.SetActive(true);

        Animation legacyAnimation = usingCardRoot.GetComponent<Animation>();
        if (legacyAnimation != null)
        {
            AnimationClip clip = legacyAnimation.clip;
            if (clip != null)
            {
                legacyAnimation.Stop();
                legacyAnimation.Play(clip.name);
                return clip.length;
            }
        }

        Animator animator = usingCardRoot.GetComponent<Animator>();
        if (animator == null)
        {
            return 0f;
        }

        return PlayAnimatorStateFromStart(
            animator,
            usingCardShowStateName,
            true
        );
    }

    /// <summary>
    /// 建立移往出牌目標的卡片顯示副本。
    /// </summary>
    private GameObject CreateUsingCardTargetClone(CardDefinition card)
    {
        Transform targetParent =
            ResolveUsingCardTargetParent(lastPlayedCardTargetTransform);
        if (usingCardRoot == null || targetParent == null)
        {
            return null;
        }

        GameObject clone = Instantiate(usingCardRoot, targetParent, false);
        clone.name = "UsingCard";
        ApplyCardData(clone, card);
        DisableCloneAnimationComponents(clone);
        BringUsingCardToFront(clone);
        clone.SetActive(true);

        RectTransform rectTransform =
            clone.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = usingCardTargetStartOffset;
            rectTransform.localRotation = Quaternion.identity;
        }
        else
        {
            clone.transform.localPosition =
                new Vector3(
                    usingCardTargetStartOffset.x,
                    usingCardTargetStartOffset.y,
                    0f
                );
            clone.transform.localRotation = Quaternion.identity;
        }

        clone.transform.localScale = usingCardRoot.transform.localScale;
        return clone;
    }

    /// <summary>
    /// 播放卡片朝目標縮小的動畫。
    /// </summary>
    private IEnumerator PlayUsingCardShrinkOnTarget(GameObject targetClone)
    {
        if (targetClone == null)
        {
            yield break;
        }

        Canvas targetCanvas =
            ConfigureUsingCardWorldCanvas(targetClone);
        Coroutine faceCameraRoutine =
            StartCoroutine(FaceUsingCardCameraRoutine(targetCanvas));

        Animator animator = targetClone.GetComponent<Animator>();
        try
        {
            if (animator != null)
            {
                float duration =
                    PlayAnimatorStateFromStart(
                        animator,
                        usingCardShrinkStateName,
                        false
                    );
                if (duration > 0f)
                {
                    yield return new WaitForSecondsRealtime(duration);
                    yield break;
                }
            }

            yield return ShrinkUsingCardCloneToLocalZero(targetClone);
        }
        finally
        {
            if (faceCameraRoutine != null)
            {
                StopCoroutine(faceCameraRoutine);
            }
        }
    }

    /// <summary>
    /// 設定出牌卡片使用的世界空間 Canvas。
    /// </summary>
    private Canvas ConfigureUsingCardWorldCanvas(GameObject targetClone)
    {
        if (targetClone == null)
        {
            return null;
        }

        Canvas targetCanvas =
            targetClone.GetComponent<Canvas>();
        if (targetCanvas == null)
        {
            targetCanvas = targetClone.AddComponent<Canvas>();
        }

        if (targetCanvas == null)
        {
            return null;
        }

        targetCanvas.renderMode = RenderMode.WorldSpace;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = usingCardSortingOrder;
        targetCanvas.worldCamera = ResolveUsingCardCamera();

        RectTransform canvasRect =
            targetCanvas.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            if (
                Mathf.Approximately(canvasRect.sizeDelta.x, 0f) ||
                Mathf.Approximately(canvasRect.sizeDelta.y, 0f)
            )
            {
                canvasRect.sizeDelta = usingCardWorldFallbackSize;
            }

            if (
                Mathf.Approximately(canvasRect.localScale.x, 0f) ||
                Mathf.Approximately(canvasRect.localScale.y, 0f) ||
                Mathf.Approximately(canvasRect.localScale.z, 0f)
            )
            {
                canvasRect.localScale = usingCardWorldFallbackScale;
            }
        }

        FaceUsingCardCamera(targetCanvas);
        return targetCanvas;
    }

    /// <summary>
    /// 調整出牌卡片的階層與排序，使其顯示於前方。
    /// </summary>
    private void BringUsingCardToFront(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        target.transform.SetAsLastSibling();

        Canvas canvas = target.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = target.AddComponent<Canvas>();
        }

        canvas.overrideSorting = true;
        canvas.sortingOrder = usingCardSortingOrder;
    }

    /// <summary>
    /// 在出牌動畫期間持續調整卡面朝向相機。
    /// </summary>
    private IEnumerator FaceUsingCardCameraRoutine(Canvas targetCanvas)
    {
        while (targetCanvas != null)
        {
            FaceUsingCardCamera(targetCanvas);
            yield return null;
        }
    }

    /// <summary>
    /// 依設定將出牌卡片轉向目前相機。
    /// </summary>
    private void FaceUsingCardCamera(Canvas targetCanvas)
    {
        if (!usingCardFaceCameraInWorldSpace || targetCanvas == null)
        {
            return;
        }

        Camera camera = ResolveUsingCardCamera();
        RectTransform canvasRect =
            targetCanvas.GetComponent<RectTransform>();
        if (camera == null || canvasRect == null)
        {
            return;
        }

        targetCanvas.worldCamera = camera;
        canvasRect.rotation = camera.transform.rotation;

        if (usingCardInvertCameraFacing)
        {
            canvasRect.Rotate(0f, 180f, 0f, Space.Self);
        }
    }

    /// <summary>
    /// 取得出牌卡片世界空間顯示使用的相機。
    /// </summary>
    private Camera ResolveUsingCardCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        Camera[] cameras =
            Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.isActiveAndEnabled)
            {
                return camera;
            }
        }

        return null;
    }

    /// <summary>
    /// 從指定動畫狀態的起點播放 Animator。
    /// </summary>
    private float PlayAnimatorStateFromStart(
        Animator animator,
        string stateName,
        bool fallbackToCurrentState
    )
    {
        if (animator == null)
        {
            return 0f;
        }

        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);

        int stateHash = ResolveAnimatorStateHash(animator, stateName);
        if (stateHash == 0)
        {
            if (!fallbackToCurrentState)
            {
                return 0f;
            }

            AnimatorStateInfo currentState =
                animator.GetCurrentAnimatorStateInfo(0);
            stateHash = currentState.fullPathHash;
        }

        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);

        AnimatorStateInfo state =
            animator.GetCurrentAnimatorStateInfo(0);
        return state.length > 0f ? state.length : 0f;
    }

    /// <summary>
    /// 解析 Animator 可用的狀態雜湊值。
    /// </summary>
    private int ResolveAnimatorStateHash(
        Animator animator,
        string stateName
    )
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return 0;
        }

        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, shortHash))
        {
            return shortHash;
        }

        int fullHash = Animator.StringToHash("Base Layer." + stateName);
        if (animator.HasState(0, fullHash))
        {
            return fullHash;
        }

        Debug.LogWarning(
            $"[CardDebug][UsingCardAnimationMissing] State={stateName}",
            animator
        );
        return 0;
    }

    /// <summary>
    /// 選取出牌目標動畫的父物件。
    /// </summary>
    private Transform ResolveUsingCardTargetParent(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        Canvas canvas = target.GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            return canvas.transform;
        }

        return target;
    }

    /// <summary>
    /// 停用顯示副本中會干擾出牌動畫的元件。
    /// </summary>
    private void DisableCloneAnimationComponents(GameObject clone)
    {
        Animation legacyAnimation = clone.GetComponent<Animation>();
        if (legacyAnimation != null)
        {
            legacyAnimation.Stop();
            legacyAnimation.enabled = false;
        }

        Animator animator = clone.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = true;
        }
    }

    /// <summary>
    /// 逐步將卡片副本移向父物件原點並縮小。
    /// </summary>
    private IEnumerator ShrinkUsingCardCloneToLocalZero(
        GameObject cardObject
    )
    {
        if (cardObject == null)
        {
            yield break;
        }

        RectTransform rectTransform =
            cardObject.GetComponent<RectTransform>();
        Transform cardTransform = cardObject.transform;

        Vector2 startAnchoredPosition = rectTransform != null
            ? rectTransform.anchoredPosition
            : Vector2.zero;
        Vector3 startLocalPosition = cardTransform.localPosition;
        Vector2 endAnchoredPosition = Vector2.zero;
        Vector3 endLocalPosition = Vector3.zero;
        Vector3 startScale = cardTransform.localScale;
        float duration = Mathf.Max(0.01f, playedCardShrinkSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition =
                    Vector2.Lerp(
                        startAnchoredPosition,
                        endAnchoredPosition,
                        t
                    );
            }
            else
            {
                cardTransform.localPosition =
                    Vector3.Lerp(startLocalPosition, endLocalPosition, t);
            }

            cardTransform.localScale =
                Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }
    }

    /// <summary>
    /// 檢查拖牌目標並嘗試對棋子出牌；連線時送交主機。
    /// </summary>
    public bool TryApplyCardToPiece(
        CardDefinition card,
        Vector2 screenPosition
    )
    {
        if (IsGameplayLocked() || !CanLocalPlayerUseCurrentTurnControls())
        {
            ShowAlarm("現在不能使用卡片");
            return false;
        }

        if (
            logicManager == null ||
            card == null ||
            Camera.main == null ||
            Time.timeScale == 0 ||
            logicManager.isPromotionActive
        )
        {
            return false;
        }

        if (card.cardType == CardType.Event)
        {
            return TryResolveEventCard(card, screenPosition);
        }

        if (card.cardType == CardType.Field)
        {
            ShowAlarm("場地卡要放到場地區");
            return false;
        }

        Ray ray = Camera.main.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            ShowAlarm("卡片不能裝在這裡");
            return false;
        }

        Piece target = hit.transform.GetComponentInParent<Piece>();

        if (
            multiplayerGameController != null &&
            multiplayerGameController.IsOnline
        )
        {
            if (target == null)
            {
                ShowAlarm("卡片不能裝在這裡");
                return false;
            }

            if (
                target.IsWhite != logicManager.isWhiteTurn ||
                !card.CanApplyTo(target, logicManager)
            )
            {
                ShowAlarm("卡片不能裝");
                return false;
            }

            multiplayerGameController.SubmitCommand(
                multiplayerGameController.CreatePlayCardOnPieceCommand(
                    card,
                    target
                )
            );
            SetLastPlayedCardTarget(target.transform);
            return true;
        }

        if (
            target == null ||
            target.IsWhite != logicManager.isWhiteTurn ||
            !card.CanApplyTo(target, logicManager)
        )
        {
            ShowAlarm("卡片不能裝");
            Debug.Log(
                $"鋆??∠?憭望? ?∠?={card.cardName} | " +
                $"?格?={(target != null ? target.name : "None")} | " +
                "??=?格??摰嗆?航炊?皛輯雲璇辣"
            );
            return false;
        }

        List<CardDefinition> currentHand =
            logicManager.isWhiteTurn ? whiteHand : blackHand;

        if (!currentHand.Remove(card))
        {
            return false;
        }

        target.ApplyCard(card);
        CardPlayFeedback.Show(target.transform);
        SetLastPlayedCardTarget(target.transform);
        OperateLogUI.LogCard(
            logicManager.isWhiteTurn,
            card,
            $"到 {target.name} {target.GetCoordinates()}"
        );
        Debug.Log(
            $"鋆??∠??? ?∠?={card.cardName} | " +
            $"?格?={target.name} | 雿蔭={target.GetCoordinates()}"
        );
        logicManager.RefreshBoardFieldEffects();
        logicManager.UpdateCheckMap();
        Refresh(logicManager.isWhiteTurn);

        return true;
    }

    /// <summary>
    /// 檢查場地卡目標並嘗試放置；連線時送交主機。
    /// </summary>
    public bool TryApplyFieldCardToPlace(
        CardDefinition card,
        FieldCardPlace place
    )
    {
        if (
            logicManager == null ||
            card == null ||
            place == null ||
            IsGameplayLocked() ||
            !CanLocalPlayerUseCurrentTurnControls()
        )
        {
            ShowAlarm("場地區不能放");
            return false;
        }

        if (card.cardType != CardType.Field)
        {
            ShowAlarm("這不是場地卡");
            return false;
        }

        if (
            multiplayerGameController != null &&
            multiplayerGameController.IsOnline
        )
        {
            logicManager.DeferFullGameRestartUntilUsingCard();
            multiplayerGameController.SubmitCommand(
                multiplayerGameController.CreatePlayFieldCardCommand(
                    card,
                    place
                )
            );
            SetLastPlayedCardTarget(place.transform);
            return true;
        }

        List<CardDefinition> currentHand =
            logicManager.isWhiteTurn ? whiteHand : blackHand;

        if (!currentHand.Contains(card))
        {
            ShowAlarm("手牌沒有這張卡");
            return false;
        }

        logicManager.DeferFullGameRestartUntilUsingCard();
        if (!logicManager.TryPlayFieldCard(card, place))
        {
            logicManager.ClearFullGameRestartDeferral();
            ShowAlarm("場地區不能放");
            return false;
        }

        currentHand.Remove(card);
        SetLastPlayedCardTarget(place.transform);
        CardPlayFeedback.Show(place.transform);
        Debug.Log(
            $"[CardDebug][CardPlayed] Type=Field | " +
            $"Card={card.id} {card.cardName}"
        );
        OperateLogUI.LogCard(
            logicManager.isWhiteTurn,
            card,
            $"到 {place.name}"
        );

        Refresh(logicManager.isWhiteTurn);
        return true;
    }

    /// <summary>
    /// 依事件卡的目標條件嘗試執行技能。
    /// </summary>
    private bool TryResolveEventCard(
        CardDefinition card,
        Vector2 screenPosition
    )
    {
        if (!CanLocalPlayerUseCurrentTurnControls())
        {
            ShowAlarm("現在不能使用卡片");
            return false;
        }

        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            ShowAlarm("卡片不能使用在這裡");
            return false;
        }

        Piece target = hit.transform.GetComponentInParent<Piece>();

        if (
            multiplayerGameController != null &&
            multiplayerGameController.IsOnline
        )
        {
            if (target == null)
            {
                ShowAlarm("卡片不能使用在這裡");
                return false;
            }

            if (!card.CanApplyTo(target, logicManager))
            {
                ShowAlarm("卡片不能使用");
                return false;
            }

            multiplayerGameController.SubmitCommand(
                multiplayerGameController.CreatePlayCardOnPieceCommand(
                    card,
                    target
                )
            );
            SetLastPlayedCardTarget(target.transform);
            return true;
        }

        if (target == null || !card.CanApplyTo(target, logicManager))
        {
            ShowAlarm("卡片不能使用");
            Debug.Log(
                $"[CardDebug][EventRejected] Card={card.id} {card.cardName} | " +
                $"Target={(target != null ? target.name : "None")}"
            );
            return false;
        }

        List<CardDefinition> currentHand =
            logicManager.isWhiteTurn ? whiteHand : blackHand;
        if (!currentHand.Contains(card)) return false;

        if (!card.skill.ResolveEvent(
            new CardSkillContext(logicManager, target, card)))
        {
            ShowAlarm("卡片不能使用");
            return false;
        }

        currentHand.Remove(card);
        SetLastPlayedCardTarget(target.transform);
        CardPlayFeedback.Show(target.transform);
        OperateLogUI.LogCard(
            logicManager.isWhiteTurn,
            card,
            $"對 {target.name} {target.GetCoordinates()}"
        );
        Debug.Log(
            $"[CardDebug][CardPlayed] Type=Event | " +
            $"Card={card.id} {card.cardName} | Target={target.name} | " +
            $"Cell={target.GetCoordinates()}"
        );

        logicManager.RefreshBoardFieldEffects();
        logicManager.UpdateCheckMap();
        Refresh(logicManager.isWhiteTurn);
        return true;
    }

    /// <summary>
    /// 依卡號與座標套用出牌效果；遠端重播可指定不再次移除手牌。
    /// </summary>
    public bool PlayCardOnPieceAsAuthority(
        bool isWhitePlayer,
        string cardId,
        BoardCoordinate targetCoordinate,
        bool removeFromHand = true
    )
    {
        if (IsRpgDisabled) return false;
        if (
            logicManager == null ||
            string.IsNullOrEmpty(cardId) ||
            !targetCoordinate.IsValid ||
            logicManager.isWhiteTurn != isWhitePlayer
        )
        {
            return false;
        }

        CardDefinition card = cardLibrary != null
            ? cardLibrary.GetCard(cardId)
            : null;
        Piece target =
            logicManager.boardMap[targetCoordinate.x, targetCoordinate.y];
        if (card == null || target == null)
        {
            return false;
        }

        List<CardDefinition> currentHand =
            isWhitePlayer ? whiteHand : blackHand;
        if (removeFromHand && !currentHand.Contains(card))
        {
            return false;
        }

        bool accepted;
        if (card.cardType == CardType.Event)
        {
            accepted =
                card.CanApplyTo(target, logicManager) &&
                card.skill != null &&
                card.skill.ResolveEvent(
                    new CardSkillContext(logicManager, target, card)
                );
        }
        else
        {
            accepted =
                card.cardType != CardType.Field &&
                target.IsWhite == isWhitePlayer &&
                card.CanApplyTo(target, logicManager);

            if (accepted)
            {
                target.ApplyCard(card);
            }
        }

        if (!accepted)
        {
            return false;
        }

        CardPlayFeedback.Show(target.transform);
        if (removeFromHand)
        {
            currentHand.Remove(card);
        }
        logicManager.RefreshBoardFieldEffects();
        logicManager.UpdateCheckMap();
        Refresh(logicManager.isWhiteTurn);
        OperateLogUI.LogCard(
            isWhitePlayer,
            card,
            $"對 {target.name} {targetCoordinate}"
        );

        Debug.Log(
            $"[NetworkGame][CardPlayed] Player={(isWhitePlayer ? "White" : "Black")} | " +
            $"Card={card.id} {card.cardName} | Target={target.name} | " +
            $"Cell={targetCoordinate}"
        );
        return true;
    }

    /// <summary>
    /// 依卡號與欄位名稱套用場地卡；遠端重播可指定不再次移除手牌。
    /// </summary>
    public bool PlayFieldCardAsAuthority(
        bool isWhitePlayer,
        string cardId,
        string fieldPlaceName,
        bool removeFromHand = true
    )
    {
        if (IsRpgDisabled) return false;
        if (
            logicManager == null ||
            string.IsNullOrEmpty(cardId) ||
            string.IsNullOrEmpty(fieldPlaceName) ||
            logicManager.isWhiteTurn != isWhitePlayer
        )
        {
            return false;
        }

        CardDefinition card = cardLibrary != null
            ? cardLibrary.GetCard(cardId)
            : null;
        FieldCardPlace place = FindFieldCardPlace(fieldPlaceName);
        if (card == null || place == null || card.cardType != CardType.Field)
        {
            return false;
        }

        List<CardDefinition> currentHand =
            isWhitePlayer ? whiteHand : blackHand;
        if (removeFromHand && !currentHand.Contains(card))
        {
            return false;
        }

        if (!logicManager.TryPlayFieldCard(card, place))
        {
            return false;
        }

        if (!IsRpgDisabled) CardPlayFeedback.Show(place.transform);
        if (removeFromHand)
        {
            currentHand.Remove(card);
        }
        Refresh(logicManager.isWhiteTurn);
        OperateLogUI.LogCard(
            isWhitePlayer,
            card,
            $"到 {place.name}"
        );

        Debug.Log(
            $"[NetworkGame][FieldCardPlayed] Player={(isWhitePlayer ? "White" : "Black")} | " +
            $"Card={card.id} {card.cardName} | Place={place.name}"
        );
        return true;
    }

    /// <summary>
    /// 依既有欄位名稱尋找場地卡放置元件。
    /// </summary>
    private FieldCardPlace FindFieldCardPlace(string fieldPlaceName)
    {
        FieldCardPlace[] places =
            Object.FindObjectsByType<FieldCardPlace>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (FieldCardPlace place in places)
        {
            if (place != null && place.name == fieldPlaceName)
            {
                return place;
            }
        }

        return null;
    }

    /// <summary>
    /// 將卡牌圖像、文字與相關資訊套用到卡片 UI。
    /// </summary>
    private void ApplyCardData(GameObject cardObject, CardDefinition card)
    {
        CardPresentation.ApplyCardData(cardObject, card);
    }

    /// <summary>
    /// 補齊卡牌介面所需的容器與控制項引用。
    /// </summary>
    private void ResolveCardGameUiReferences()
    {
        if (cardGameUiRoot == null)
        {
            Debug.LogError(
                "CardHandManager requires CardGameUI root to be assigned."
            );
            return;
        }

        if (turnText == null)
        {
            turnText = FindChildComponent<TMP_Text>(
                cardGameUiRoot,
                "TurnText"
            );
        }

        if (handRoot == null)
        {
            Transform handCard =
                FindChildRecursive(cardGameUiRoot, "HandCard");
            handRoot = handCard as RectTransform;
        }

        if (drawButton == null)
        {
            drawButton = FindChildComponent<Button>(
                cardGameUiRoot,
                "Draw"
            );

            BindDrawButton();
        }

        if (recycleCardRoot == null)
        {
            Transform recycleCard =
                FindChildRecursive(cardGameUiRoot, "RecycleCard");
            recycleCardRoot = recycleCard as RectTransform;
        }

        if (usingCardRoot == null)
        {
            Transform usingCard =
                FindChildRecursive(cardGameUiRoot, "UsingCard");
            if (usingCard != null)
            {
                usingCardRoot = usingCard.gameObject;
            }
        }

        ResolveHealthReferences(
            "WhiteHP",
            ref whiteHealthSlider,
            ref whiteHealthText
        );
        ResolveHealthReferences(
            "BlackHP",
            ref blackHealthSlider,
            ref blackHealthText
        );

        ResolveOpponentHandReferences();
    }

    /// <summary>
    /// 補齊雙方視角下的對手手牌顯示引用。
    /// </summary>
    private void ResolveOpponentHandReferences()
    {
        if (whiteViewOpponentHandRoot == null)
        {
            Transform matchHandCard = FindSceneTransform("WhiteHandCard");
            if (matchHandCard == null)
            {
                matchHandCard = FindSceneTransform("MatchHandCard");
            }

            if (matchHandCard != null)
            {
                whiteViewOpponentHandRoot = matchHandCard;
            }
        }

        if (blackViewOpponentHandRoot == null)
        {
            Transform matchHandCard =
                FindSceneTransform("BlackHandCard");
            if (matchHandCard == null)
            {
                matchHandCard = FindSceneTransform("BlackViewMatchHandCard");
            }
            if (matchHandCard == null)
            {
                matchHandCard = FindSceneTransform("WhiteMatchHandCard");
            }

            if (matchHandCard != null)
            {
                blackViewOpponentHandRoot = matchHandCard;
            }
        }

        whiteViewOpponentHandRoot =
            NormalizeOpponentHandRoot(whiteViewOpponentHandRoot);
        blackViewOpponentHandRoot =
            NormalizeOpponentHandRoot(blackViewOpponentHandRoot);

        if (
            opponentCardBackTemplate == null &&
            whiteViewOpponentHandRoot != null
        )
        {
            Transform cardBack =
                FindChildRecursive(whiteViewOpponentHandRoot, "CardBack");
            if (cardBack != null)
            {
                opponentCardBackTemplate = cardBack.gameObject;
            }
        }

        if (
            opponentCardBackTemplate == null &&
            blackViewOpponentHandRoot != null
        )
        {
            Transform cardBack =
                FindChildRecursive(blackViewOpponentHandRoot, "CardBack");
            if (cardBack != null)
            {
                opponentCardBackTemplate = cardBack.gameObject;
            }
        }

        if (opponentCardBackTemplate == null)
        {
            opponentCardBackTemplate = LoadCardBackPrefab();
        }

        if (opponentCardBackTemplate == null)
        {
            opponentCardBackTemplate = CreateFallbackCardBackTemplate();
        }
    }

    /// <summary>
    /// 將對手手牌引用解析為實際使用的容器。
    /// </summary>
    private Transform NormalizeOpponentHandRoot(Transform root)
    {
        if (root == null || root.name == "Panel")
        {
            return root;
        }

        Transform panel = FindChildRecursive(root, "Panel");
        return panel != null ? panel : root;
    }

    /// <summary>
    /// 判斷指定物件是否位於目標父物件之下。
    /// </summary>
    private bool IsChildOf(Transform child, Transform parent)
    {
        if (child == null || parent == null)
        {
            return false;
        }

        Transform current = child;
        while (current != null)
        {
            if (current == parent)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    /// <summary>
    /// 載入對手手牌顯示所需的牌背 Prefab。
    /// </summary>
    private GameObject LoadCardBackPrefab()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/CardPrefabs/CardBack.prefab"
        );
#else
        return null;
#endif
    }

    /// <summary>
    /// 缺少牌背樣板時建立替代顯示物件。
    /// </summary>
    private GameObject CreateFallbackCardBackTemplate()
    {
        Transform parent = whiteViewOpponentHandRoot != null
            ? whiteViewOpponentHandRoot
            : blackViewOpponentHandRoot;
        if (parent == null)
        {
            return null;
        }

        GameObject template = new GameObject(
            "CardBack",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        template.transform.SetParent(parent, false);

        RectTransform rectTransform =
            template.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(80f, 120f);

        Image image = template.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = Color.white;

#if UNITY_EDITOR
        Sprite cardBackSprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/ChessCardImages/CardBack.png"
            );
        if (cardBackSprite != null)
        {
            image.sprite = cardBackSprite;
        }
#endif

        template.SetActive(false);
        return template;
    }

    /// <summary>
    /// 補齊雙方血條及血量文字的引用。
    /// </summary>
    private void ResolveHealthReferences(
        string rootName,
        ref Slider slider,
        ref TMP_Text healthText
    )
    {
        Transform healthRoot =
            FindChildRecursive(cardGameUiRoot, rootName);

        if (healthRoot == null)
        {
            return;
        }

        if (slider == null)
        {
            slider = healthRoot.GetComponentInChildren<Slider>(true);
        }

        if (healthText == null)
        {
            healthText = FindChildComponent<TMP_Text>(
                healthRoot,
                "HPText"
            );
        }
    }

    /// <summary>
    /// 綁定抽牌按鈕的操作事件。
    /// </summary>
    private void BindDrawButton()
    {
        if (drawButton == null)
        {
            return;
        }

        drawButton.onClick.RemoveListener(DrawForCurrentPlayer);
        drawButton.onClick.AddListener(DrawForCurrentPlayer);
    }

    /// <summary>
    /// 將血量更新到指定血條與文字元件。
    /// </summary>
    private void SetHealthUi(
        Slider slider,
        TMP_Text healthText,
        int health
    )
    {
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = LogicManager.MaxHealth;
            slider.wholeNumbers = true;
            slider.value = health;
            slider.interactable = false;
        }

        if (healthText != null)
        {
            healthText.text = health.ToString();
        }
    }

    /// <summary>
    /// 補齊元件所需的預設資料或場景引用。
    /// </summary>
    private void ResolveDefaults()
    {
        if (cardLibrary == null)
        {
            cardLibrary = GetComponent<ChessCard>();
        }

        if (cardLibrary == null)
        {
            Debug.LogError("CardHandManager requires ChessCard on the same object.");
            return;
        }

        if (startingDeck.Count == 0)
        {
            preserveOpeningCard = DeckStorage.HasOpeningCard(cardLibrary.Cards);
            foreach (CardDefinition card in
                DeckStorage.BuildDeck(cardLibrary.Cards))
            {
                if (card != null)
                {
                    startingDeck.Add(card);
                }
            }
        }
    }

    /// <summary>
    /// 清除指定容器下的子物件。
    /// </summary>
    private void ClearChildren(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// 依現有隨機來源洗牌；指定保留首張時不移動起始卡牌。
    /// </summary>
    private void Shuffle(List<CardDefinition> deck, bool keepFirstCard)
    {
        int firstShuffleIndex = keepFirstCard ? 1 : 0;
        for (int i = deck.Count - 1; i > firstShuffleIndex; i--)
        {
            int swapIndex = UnityEngine.Random.Range(firstShuffleIndex, i + 1);
            CardDefinition card = deck[i];
            deck[i] = deck[swapIndex];
            deck[swapIndex] = card;
        }
    }

    /// <summary>
    /// 尋找指定文字元件並更新其顯示內容。
    /// </summary>
    /// <summary>
    /// 依名稱尋找子物件，再取得所需類型的元件。
    /// </summary>
    private T FindChildComponent<T>(
        Transform root,
        string childName
    ) where T : Component
    {
        Transform child = FindChildRecursive(root, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    /// <summary>
    /// 依階層順序遞迴尋找指定名稱的 Transform；回傳第一個符合的物件。
    /// </summary>
    private Transform FindChildRecursive(
        Transform root,
        string childName
    )
    {
        if (root.name == childName)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            Transform match =
                FindChildRecursive(child, childName);

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// 依名稱尋找目前場景中的 Transform。
    /// </summary>
    private Transform FindSceneTransform(string targetName)
    {
        GameObject[] roots =
            UnityEngine.SceneManagement.SceneManager
                .GetActiveScene()
                .GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            Transform found = FindChildRecursive(root.transform, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 顯示操作或對局提示訊息。
    /// </summary>
    private void ShowAlarm(string message)
    {
        GameFlowUI.Show(message);
    }
}
