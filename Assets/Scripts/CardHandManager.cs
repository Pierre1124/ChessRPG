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
        get { return canDrawThisTurn; }
    }

    public Transform LastPlayedCardTargetTransform
    {
        get { return lastPlayedCardTargetTransform; }
    }

    public void SetCardGameUiActive(bool active)
    {
        ResolveCardGameUiReferences();
        if (cardGameUiRoot != null)
        {
            cardGameUiRoot.gameObject.SetActive(active);
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

    private bool IsGameplayLocked()
    {
        return IsMultiplayerWaiting() ||
            isUsingCardAnimationPlaying ||
            (logicManager != null && logicManager.IsOperationLocked);
    }

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

        turnText.text =
            "\u7B49\u5F85\u73A9\u5BB6\u52A0\u5165" + new string('.', waitingTextDotCount);
    }

    public void ResetHands(bool isWhiteTurn)
    {
        ResolveDefaults();

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

    public bool SetDeckForPlayerAsAuthority(
        bool isWhitePlayer,
        string serializedDeckIds,
        bool keepFirstCard
    )
    {
        ResolveDefaults();

        List<CardDefinition> targetDeck =
            isWhitePlayer ? whiteDeck : blackDeck;
        List<CardDefinition> targetHand =
            isWhitePlayer ? whiteHand : blackHand;

        targetDeck.Clear();
        targetHand.Clear();
        AddCardsFromIds(targetDeck, serializedDeckIds);

        if (targetDeck.Count == 0)
        {
            Debug.LogWarning(
                $"[NetworkGame][DeckRejected] Player={(isWhitePlayer ? "White" : "Black")} sent empty deck."
            );
            return false;
        }

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

    public void OnChessTurnChanged(bool isWhiteTurn)
    {
        if (!isInitialized)
        {
            Initialize(logicManager);
            return;
        }

        ReleasePendingCards(isWhiteTurn);
        canDrawThisTurn = true;
        Refresh(isWhiteTurn);
    }

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

    public bool QueueCardForPlayer(
        bool isWhitePlayer,
        CardDefinition card,
        int ownerTurnsDelay
    )
    {
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

    public void RefreshHealth()
    {
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

    public void Refresh(bool isWhiteTurn)
    {
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
                ? "?賣??"
                : "暺??";
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

    public string SerializeNetworkCardState()
    {
        return string.Join(
            ";",
            SerializeCardList(whiteDeck),
            SerializeCardList(blackDeck),
            SerializeCardList(whiteHand),
            SerializeCardList(blackHand)
        );
    }

    public void ApplyNetworkCardState(
        string serializedState,
        bool remoteCanDrawThisTurn,
        bool currentWhiteTurn
    )
    {
        ResolveDefaults();

        string[] sections = string.IsNullOrEmpty(serializedState)
            ? new string[0]
            : serializedState.Split(';');

        if (sections.Length < 4)
        {
            Debug.LogWarning(
                $"[NetworkGame][CardStateRejected] Invalid state={serializedState}"
            );
            return;
        }

        ApplyCardListState(whiteDeck, sections[0]);
        ApplyCardListState(blackDeck, sections[1]);
        ApplyCardListState(whiteHand, sections[2]);
        ApplyCardListState(blackHand, sections[3]);
        canDrawThisTurn = remoteCanDrawThisTurn;
        Refresh(currentWhiteTurn);

        Debug.Log(
            $"[NetworkGame][CardStateSync] Turn={(currentWhiteTurn ? "White" : "Black")} | " +
            $"CanDraw={canDrawThisTurn} | " +
            $"WhiteHand={whiteHand.Count} | BlackHand={blackHand.Count}"
        );
    }

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

    private bool CanLocalPlayerUseCurrentTurnControls()
    {
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

    public void PlayUsingCardAnimation(
        CardDefinition card,
        GameObject cardObject
    )
    {
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

    public bool PlayUsingCardAnimationOnPiece(
        string cardId,
        BoardCoordinate targetCoordinate
    )
    {
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

    public bool PlayUsingCardAnimationOnField(
        string cardId,
        string fieldPlaceName
    )
    {
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

    private void SetLastPlayedCardTarget(Transform target)
    {
        lastPlayedCardTargetTransform = target;
    }

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

    private void ApplyCardListState(
        List<CardDefinition> target,
        string serializedIds
    )
    {
        target.Clear();
        AddCardsFromIds(target, serializedIds);
    }

    private void AddCardsFromIds(
        List<CardDefinition> target,
        string serializedIds
    )
    {
        if (target == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(serializedIds))
        {
            return;
        }

        string[] ids = serializedIds.Split(',');
        foreach (string id in ids)
        {
            CardDefinition card = cardLibrary != null
                ? cardLibrary.GetCard(id)
                : null;

            if (card != null)
            {
                target.Add(card);
            }
            else
            {
                Debug.LogWarning(
                    $"[NetworkGame][CardStateMissingCard] Id={id}"
                );
            }
        }
    }

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
            opponentWhiteHand ? whiteHand.Count : blackHand.Count;

        for (int i = 0; i < opponentHandCount; i++)
        {
            GameObject cardBack =
                Instantiate(opponentCardBackTemplate, opponentHandRoot);
            cardBack.name = "CardBack";
            cardBack.SetActive(true);
        }
    }

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

    private void ClearOpponentHandCards()
    {
        ClearOpponentHandCardsInRoot(whiteViewOpponentHandRoot);
        ClearOpponentHandCardsInRoot(blackViewOpponentHandRoot);
    }

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
                if (cardGameUiRoot != null)
                {
                    cardGameUiRoot.gameObject.SetActive(false);
                }

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

    private IEnumerator FaceUsingCardCameraRoutine(Canvas targetCanvas)
    {
        while (targetCanvas != null)
        {
            FaceUsingCardCamera(targetCanvas);
            yield return null;
        }
    }

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

    public bool PlayCardOnPieceAsAuthority(
        bool isWhitePlayer,
        string cardId,
        BoardCoordinate targetCoordinate,
        bool removeFromHand = true
    )
    {
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

    public bool PlayFieldCardAsAuthority(
        bool isWhitePlayer,
        string cardId,
        string fieldPlaceName,
        bool removeFromHand = true
    )
    {
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

    private void ApplyCardData(GameObject cardObject, CardDefinition card)
    {
        Image image = cardObject.GetComponent<Image>();
        if (image == null)
        {
            image = FindChildComponent<Image>(
                cardObject.transform,
                "CardImage"
            );
        }

        if (image != null && card.cardImage != null)
        {
            image.sprite = card.cardImage;
        }

        SetText(cardObject.transform, "CardNameText", card.cardName);
        SetNestedText(
            cardObject.transform,
            "CardLord",
            "CardLordText",
            card.description
        );
        ApplyTags(cardObject.transform, card.tags);
    }

    private void ApplyTags(Transform root, List<string> tags)
    {
        Transform tagGroup = FindChildRecursive(root, "CardTagGroup");
        Transform template =
            tagGroup != null
                ? FindChildRecursive(tagGroup, "CardTag")
                : FindChildRecursive(root, "CardTag");

        if (template == null || template.parent == null)
        {
            return;
        }

        for (int i = template.parent.childCount - 1; i >= 0; i--)
        {
            Transform child = template.parent.GetChild(i);
            if (child != template && child.name == "CardTag")
            {
                Destroy(child.gameObject);
            }
        }

        if (tags == null || tags.Count == 0)
        {
            template.gameObject.SetActive(false);
            return;
        }

        for (int i = 0; i < tags.Count; i++)
        {
            Transform tagTransform =
                i == 0
                    ? template
                    : Instantiate(template.gameObject, template.parent)
                        .transform;

            tagTransform.name = "CardTag";
            tagTransform.gameObject.SetActive(true);

            TMP_Text text =
                tagTransform.GetComponentInChildren<TMP_Text>(true);

            if (text != null)
            {
                text.text = tags[i];
            }
        }
    }

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

    private Transform NormalizeOpponentHandRoot(Transform root)
    {
        if (root == null || root.name == "Panel")
        {
            return root;
        }

        Transform panel = FindChildRecursive(root, "Panel");
        return panel != null ? panel : root;
    }

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

    private void BindDrawButton()
    {
        if (drawButton == null)
        {
            return;
        }

        drawButton.onClick.RemoveListener(DrawForCurrentPlayer);
        drawButton.onClick.AddListener(DrawForCurrentPlayer);
    }

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

    private void ClearChildren(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }
    }

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

    private void SetText(
        Transform root,
        string childName,
        string value
    )
    {
        TMP_Text text = FindChildComponent<TMP_Text>(root, childName);
        if (text != null)
        {
            text.text = value;
        }
    }

    private void SetNestedText(
        Transform root,
        string parentName,
        string childName,
        string value
    )
    {
        Transform parent = FindChildRecursive(root, parentName);
        if (parent != null)
        {
            SetText(parent, childName, value);
        }
    }

    private T FindChildComponent<T>(
        Transform root,
        string childName
    ) where T : Component
    {
        Transform child = FindChildRecursive(root, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

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

    private void ShowAlarm(string message)
    {
        GameFlowUI.Show(message);
    }
}
