using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 镼踵?璉蜓瘚?蝞∠??具?/// ??砍鞎痊嚗??方???????鞎摰??UI ????/// ?∠????摰喋?銵????賡?銝剖 CardBattleSystem??/// </summary>
public class LogicManager : MonoBehaviour
{
    public const int MaxHealth = 100;
    private static bool hideCardGameUiAfterFullRestart;

    [Header("Board State")]
    public Piece[,] boardMap = new Piece[8, 8];
    public Square[,] squares = new Square[8, 8];
    public bool[,] whiteCheckMap = new bool[8, 8];
    public bool[,] blackCheckMap = new bool[8, 8];
    public List<Piece> piecesOnBoard = new List<Piece>();

    [Header("Turn State")]
    public bool isWhiteTurn;
    public bool isPromotionActive;
    public Piece lastMovedPiece;
    public Vector2 lastMovedPieceStartPosition;
    public Vector2 lastMovedPieceEndPosition;
    [SerializeField] private int whiteCapturedPieceCount;
    [SerializeField] private int blackCapturedPieceCount;

    [Header("Player Health")]
    public int whiteHealth;
    public int blackHealth;

    [Header("Scene References")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private GameOverUI gameOverUI;
    [SerializeField] private PromotionUI promotionUI;
    [SerializeField] private CardHandManager cardHandManager;
    [SerializeField] private DamageCalculationVisualizer damageVisualizer;
    [SerializeField] private FieldCardPlace[] fieldCardPlaces;
    [SerializeField] private Animation boardAnimation;
    [SerializeField] private string fieldFusionAnimationName = "FieldCardFusing";

    [Header("Audio")]
    public AudioSource moveSound;
    public AudioSource captureSound;
    public bool isCameraRotationEnabled = true;
    public bool isSoundEnabled = true;
    public float soundVolume = 0.5f;

    private CardBattleSystem cardBattle;
    private MultiplayerGameController multiplayerGameController;
    private Board board;
    private bool isCardHandManagerInitialized;
    private bool isDamageCalculationPlaying;
    private bool isApplyingRemoteDamageCalculation;
    private bool isApplyingRemoteMoveResult;
    private bool isEndTurnWaitingForDamage;
    private bool isTurnPhaseChangePlaying;
    private bool isTurnFlowPlaying;
    private int externalOperationLockCount;
    private bool deferFullGameRestartUntilUsingCard;
    private bool deferredFullGameRestartPending;
    private bool isSubscribedToCardHandEvents;
    private int damageCalculationBatchDepth;
    private readonly List<DamageCalculationRequest> pendingDamageBatch =
        new List<DamageCalculationRequest>();
    private readonly Queue<DamageCalculationBatch> damageCalculationQueue =
        new Queue<DamageCalculationBatch>();
    private readonly HashSet<int> remoteCaptureVisualizedPieceIds =
        new HashSet<int>();
    private readonly List<CardDefinition> activeFieldCards =
        new List<CardDefinition>();
    private readonly List<GameObject> boardFieldVisuals =
        new List<GameObject>();
    private readonly List<Material> boardFieldVisualMaterials =
        new List<Material>();
    private Material boardFieldVisualMaterial;
    private bool isFieldFusionPlaying;

    private CardBattleSystem CardBattle
    {
        get
        {
            if (cardBattle == null)
            {
                cardBattle = new CardBattleSystem(this);
            }

            return cardBattle;
        }
    }

    private MultiplayerGameController Multiplayer
    {
        get
        {
            if (multiplayerGameController == null)
            {
                multiplayerGameController =
                    FindFirstObjectByType<MultiplayerGameController>();
            }

            return multiplayerGameController;
        }
    }

    private bool IsNetworkClientOnly
    {
        get { return Multiplayer != null && Multiplayer.IsClientOnly; }
    }

    public bool IsDamageCalculationBusy
    {
        get
        {
            return isTurnFlowPlaying ||
                isTurnPhaseChangePlaying ||
                isDamageCalculationPlaying ||
                damageCalculationQueue.Count > 0 ||
                pendingDamageBatch.Count > 0;
        }
    }

    private bool HasDamageCalculationWork
    {
        get
        {
            return isDamageCalculationPlaying ||
                damageCalculationQueue.Count > 0 ||
                pendingDamageBatch.Count > 0;
        }
    }

    public bool IsOperationLocked
    {
        get { return IsDamageCalculationBusy || externalOperationLockCount > 0; }
    }

    public bool HasDeferredFullGameRestart
    {
        get { return deferredFullGameRestartPending; }
    }

    public bool IsFullGameRestartDeferralActive
    {
        get { return deferFullGameRestartUntilUsingCard; }
    }

    public bool IsFieldFusionPlaying
    {
        get { return isFieldFusionPlaying; }
    }

    public bool ShouldApplyLocalHealthChange
    {
        get { return !IsNetworkClientOnly; }
    }

    private void Start()
    {
        EnsureCardHandManager();
    }

    /// <summary>
    /// ?啣????oard ?Ｙ?璉???澆?ㄐ??    /// </summary>
    public void Initialize()
    {
        Time.timeScale = 1f;
        isWhiteTurn = true;
        isPromotionActive = false;
        whiteCapturedPieceCount = 0;
        blackCapturedPieceCount = 0;
        externalOperationLockCount = 0;
        deferFullGameRestartUntilUsingCard = false;
        deferredFullGameRestartPending = false;
        isEndTurnWaitingForDamage = false;
        isApplyingRemoteMoveResult = false;
        remoteCaptureVisualizedPieceIds.Clear();
        isTurnPhaseChangePlaying = false;
        isTurnFlowPlaying = false;
        activeFieldCards.Clear();
        ResolveFieldCardPlaces();
        ClearFieldCardPlaces();
        whiteHealth = MaxHealth;
        blackHealth = MaxHealth;

        if (EnsureCardHandManager())
        {
            cardHandManager.ResetHands(isWhiteTurn);
            cardHandManager.RefreshHealth();

            if (hideCardGameUiAfterFullRestart)
            {
                cardHandManager.SetCardGameUiActive(false);
                hideCardGameUiAfterFullRestart = false;
            }
        }

        Debug.Log("[CardDebug][Initialize] White starts | HP=100/100");
        GameFlowUI.SetPersistent("Check", string.Empty, false);
        OperateLogUI.ResetLog(isWhiteTurn);
        BroadcastNetworkStateIfAuthority();
    }

    /// <summary>
    /// ?臭?甇??????????    /// InputManager 蝘餃?摰??romotionUI ??摰?敺??賣?韏圈ㄐ??    /// </summary>
    public void EndTurn()
    {
        if (Time.timeScale == 0f)
        {
            return;
        }

        if (isTurnFlowPlaying || isTurnPhaseChangePlaying)
        {
            return;
        }

        if (HasDamageCalculationWork)
        {
            if (!isEndTurnWaitingForDamage)
            {
                StartCoroutine(EndTurnAfterDamageCalculations());
            }

            return;
        }

        StartCoroutine(TurnFlowRoutine());
    }

    private IEnumerator EndTurnAfterDamageCalculations()
    {
        isEndTurnWaitingForDamage = true;

        yield return WaitForDamageCalculations();

        isEndTurnWaitingForDamage = false;
        if (
            Time.timeScale == 0f ||
            isPromotionActive ||
            isTurnFlowPlaying ||
            isTurnPhaseChangePlaying
        )
        {
            yield break;
        }

        StartCoroutine(TurnFlowRoutine());
    }

    private IEnumerator TurnFlowRoutine()
    {
        isTurnFlowPlaying = true;
        try
        {
            yield return PlayPhaseChange("回合結束");
            if (Time.timeScale == 0f)
            {
                yield break;
            }

            BeginDamageCalculationBatch();
            try
            {
                CardBattle.OnTurnEnded(isWhiteTurn);
                ResolveFieldTurnEndedEffects(isWhiteTurn);
            }
            finally
            {
                EndDamageCalculationBatch();
            }

            yield return WaitForDamageCalculations();
            if (Time.timeScale == 0f)
            {
                yield break;
            }

            isWhiteTurn = !isWhiteTurn;

            Debug.Log(
                $"[CardDebug][Turn] Current={(isWhiteTurn ? "White" : "Black")}"
            );

        if (EnsureCardHandManager())
        {
            cardHandManager.OnChessTurnChanged(isWhiteTurn);
        }
        OperateLogUI.BeginTurn(isWhiteTurn);

            CheckGameOver();
            if (Time.timeScale == 0f)
            {
                yield break;
            }

            yield return PlayPhaseChange("回合開始");
            if (Time.timeScale == 0f)
            {
                yield break;
            }

            BeginDamageCalculationBatch();
            try
            {
                CardBattle.OnTurnStarted(isWhiteTurn);
            }
            finally
            {
                EndDamageCalculationBatch();
            }

            yield return WaitForDamageCalculations();
            if (Time.timeScale == 0f)
            {
                yield break;
            }

            yield return PlayPhaseChange("操作階段");

            RotateCameraForCurrentTurn();
            BroadcastNetworkStateIfAuthority();
        }
        finally
        {
            isTurnFlowPlaying = false;
            if (
                Time.timeScale != 0f &&
                EnsureCardHandManager()
            )
            {
                cardHandManager.Refresh(isWhiteTurn);
            }
        }
    }

    private IEnumerator PlayPhaseChange(string message)
    {
        isTurnPhaseChangePlaying = true;
        bool completed = false;

        Multiplayer?.BroadcastPhaseChange(message);

        GameFlowUI.PlayPhase(
            this,
            message,
            () =>
            {
                completed = true;
            }
        );

        while (!completed)
        {
            yield return null;
        }

        isTurnPhaseChangePlaying = false;
    }

    private IEnumerator WaitForDamageCalculations()
    {
        while (HasDamageCalculationWork)
        {
            yield return null;
        }
    }
    public void OnPieceMoved(Piece piece)
    {
        if (EnsureCardHandManager())
        {
            cardHandManager.DisableDrawForCurrentTurn();
        }

        CardBattle.OnPieceMoved(piece);
        RefreshBoardFieldEffects();
    }

    public bool TryExecuteNetworkMove(
        BoardCoordinate from,
        BoardCoordinate to,
        bool isWhitePlayer
    )
    {
        if (
            Time.timeScale == 0f ||
            isPromotionActive ||
            IsOperationLocked ||
            !from.IsValid ||
            !to.IsValid ||
            isWhiteTurn != isWhitePlayer
        )
        {
            Debug.Log(
                $"[NetworkGame][MoveRejected] Basic state invalid | " +
                $"From={from} | To={to} | Player={(isWhitePlayer ? "White" : "Black")}"
            );
            return false;
        }

        Piece piece = boardMap[from.x, from.y];
        if (piece == null || piece.IsWhite != isWhitePlayer)
        {
            Debug.Log(
                $"[NetworkGame][MoveRejected] No matching piece | " +
                $"From={from} | To={to}"
            );
            return false;
        }

        Vector2 targetCoordinates = to.ToVector2();
        if (!piece.GetLegalMoves().Contains(targetCoordinates))
        {
            Debug.Log(
                $"[NetworkGame][MoveRejected] Illegal move | " +
                $"Piece={piece.name} | From={from} | To={to}"
            );
            return false;
        }

        Vector2 startPosition = piece.GetCoordinates();
        piece.Move(targetCoordinates);
        OperateLogUI.LogMove(piece, startPosition, piece.GetCoordinates());

        lastMovedPiece = piece;
        lastMovedPieceStartPosition = startPosition;
        lastMovedPieceEndPosition = piece.GetCoordinates();

        if (!isPromotionActive)
        {
            UpdateCheckMap();
            if (!IsNetworkClientOnly)
            {
                EndTurn();
            }
        }

        Debug.Log(
            $"[NetworkGame][MoveApplied] Piece={piece.name} | " +
            $"From={from} | To={to}"
        );
        return true;
    }

    public bool ApplyNetworkMoveResult(
        BoardCoordinate from,
        BoardCoordinate to,
        bool isWhitePlayer
    )
    {
        if (
            Time.timeScale == 0f ||
            isPromotionActive ||
            !from.IsValid ||
            !to.IsValid
        )
        {
            Debug.Log(
                $"[NetworkGame][RemoteMoveRejected] Basic state invalid | " +
                $"From={from} | To={to} | Player={(isWhitePlayer ? "White" : "Black")}"
            );
            return false;
        }

        Piece piece = boardMap[from.x, from.y];
        if (piece == null || piece.IsWhite != isWhitePlayer)
        {
            Debug.Log(
                $"[NetworkGame][RemoteMoveRejected] No matching piece | " +
                $"From={from} | To={to}"
            );
            return false;
        }

        Vector2 startPosition = piece.GetCoordinates();
        Piece capturedPiece = FindCapturedPieceForMove(piece, to);

        isApplyingRemoteMoveResult = true;
        try
        {
            piece.Move(to.ToVector2());
        }
        finally
        {
            isApplyingRemoteMoveResult = false;
        }

        if (IsNetworkClientOnly && capturedPiece != null)
        {
            StartCoroutine(EnsureRemoteCapturedPieceRemoved(capturedPiece));
        }

        OperateLogUI.LogMove(piece, startPosition, piece.GetCoordinates());

        lastMovedPiece = piece;
        lastMovedPieceStartPosition = startPosition;
        lastMovedPieceEndPosition = piece.GetCoordinates();

        if (!isPromotionActive)
        {
            UpdateCheckMap();
            if (!IsNetworkClientOnly)
            {
                EndTurn();
            }
            else
            {
                Debug.Log(
                    "[NetworkGame][RemoteMoveApplied] Client skipped local EndTurn; waiting for host state."
                );
            }
        }

        Debug.Log(
            $"[NetworkGame][RemoteMoveApplied] Piece={piece.name} | " +
            $"From={from} | To={to}"
        );
        return true;
    }

    public void ApplyRemoteNetworkState(
        bool remoteIsWhiteTurn,
        int remoteWhiteHealth,
        int remoteBlackHealth,
        string remoteCardState,
        bool remoteCanDrawThisTurn
    )
    {
        bool turnChanged = isWhiteTurn != remoteIsWhiteTurn;
        isWhiteTurn = remoteIsWhiteTurn;
        whiteHealth = remoteWhiteHealth;
        blackHealth = remoteBlackHealth;

        if (EnsureCardHandManager())
        {
            cardHandManager.Refresh(isWhiteTurn);
            cardHandManager.RefreshHealth();
            cardHandManager.ApplyNetworkCardState(
                remoteCardState,
                remoteCanDrawThisTurn,
                isWhiteTurn
            );
        }

        if (turnChanged)
        {
            OperateLogUI.BeginTurn(isWhiteTurn);
        }

        Debug.Log(
            $"[NetworkGame][StateSync] Turn={(isWhiteTurn ? "White" : "Black")} | " +
            $"HP={whiteHealth}/{blackHealth}"
        );

        UpdateCheckMap();
        SetCheckAlarm(CheckKingStatus());
    }

    public void ApplyRemoteGameOver(string result)
    {
        ShowGameOverLocal(result);
    }

    public void OnPiecesCastled(King king, Rook rook)
    {
        CardBattle.OnPiecesCastled(king, rook);
    }

    public void OnPieceCaptured(Piece capturingPiece, Piece capturedPiece)
    {
        if (IsNetworkClientOnly && isApplyingRemoteMoveResult)
        {
            return;
        }

        CardBattle.OnPieceCaptured(capturingPiece, capturedPiece);
        BreakBoardFieldCard(capturedPiece, "PieceCaptured", true);
    }

    public void OnPieceRemovedFromBoard(Piece removedPiece)
    {
        RefreshBoardFieldEffects();
    }

    public void RefreshBoardFieldEffects()
    {
        ClearBoardFieldVisuals();

        List<BoardFieldEffectZone> zones = GetActiveBoardFieldZones();
        foreach (BoardFieldEffectZone zone in zones)
        {
            Piece visualSource = zone.firstSource != null
                ? zone.firstSource
                : zone.secondSource;

            foreach (Vector2Int cell in zone.cells)
            {
                CreateBoardFieldVisual(visualSource, cell, zone.type);
            }
        }
    }

    public List<Vector2> ApplyBoardFieldEffects(
        Piece mover,
        List<Vector2> legalMoves
    )
    {
        if (mover == null || legalMoves == null || legalMoves.Count == 0)
        {
            return legalMoves;
        }

        List<BoardFieldEffectZone> zones = GetActiveBoardFieldZones();
        if (zones.Count == 0)
        {
            return legalMoves;
        }

        if (IsBoardFieldSource(mover, zones))
        {
            Debug.Log(
                $"[CardDebug][FieldSourceLocked] " +
                $"Mover={DescribePieceForField(mover)}"
            );
            return new List<Vector2>();
        }

        List<Vector2> filteredMoves = new List<Vector2>();
        Vector2Int start = ToBoardCell(mover.GetCoordinates());

        foreach (Vector2 move in legalMoves)
        {
            Vector2Int target = ToBoardCell(move);

            if (
                TryGetFirstFieldCellOnPath(
                    mover,
                    start,
                    target,
                    zones,
                    out Vector2Int fieldCell,
                    out BoardFieldEffectType fieldType
                ) &&
                fieldCell != target
            )
            {
                Debug.Log(
                    $"[CardDebug][FieldBlock] Type={fieldType} | " +
                    $"Mover={DescribePieceForField(mover)} | " +
                    $"Start=({start.x},{start.y}) | " +
                    $"BlockedAt=({fieldCell.x},{fieldCell.y}) | " +
                    $"OriginalTarget=({target.x},{target.y})"
                );
                continue;
            }

            filteredMoves.Add(move);
        }

        return filteredMoves;
    }

    private bool IsBoardFieldSource(
        Piece mover,
        List<BoardFieldEffectZone> zones
    )
    {
        foreach (BoardFieldEffectZone zone in zones)
        {
            if (zone.firstSource == mover || zone.secondSource == mover)
            {
                return true;
            }
        }

        return false;
    }

    public void BreakBoardFieldCard(
        Piece sourcePiece,
        string reason = "CardDestroyed",
        bool applyDestroyedEffect = true
    )
    {
        BoardFieldEffectType type = GetBoardFieldEffectType(sourcePiece);
        if (type == BoardFieldEffectType.None)
        {
            return;
        }

        Vector2Int sourceCell = ToBoardCell(sourcePiece.GetCoordinates());
        bool resolvedDestroyedEffect = false;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Piece candidate = boardMap[x, y];
                if (
                    candidate == null ||
                    candidate == sourcePiece ||
                    candidate.IsWhite != sourcePiece.IsWhite ||
                    GetBoardFieldEffectType(candidate) != type
                )
                {
                    continue;
                }

                Vector2Int candidateCell =
                    ToBoardCell(candidate.GetCoordinates());
                if (
                    candidateCell.x != sourceCell.x &&
                    candidateCell.y != sourceCell.y
                )
                {
                    continue;
                }

                if (applyDestroyedEffect && !resolvedDestroyedEffect)
                {
                    ResolveBoardFieldDestroyedEffect(sourcePiece, type);
                    resolvedDestroyedEffect = true;
                }

                Debug.Log(
                    $"[CardDebug][FieldPairBroken] Type={type} | " +
                    $"Source={DescribePieceForField(sourcePiece)} | " +
                    $"RemoveFrom={DescribePieceForField(candidate)} | " +
                    $"Reason={reason}"
                );

                candidate.ApplyCard(null);
            }
        }

        if (sourcePiece != null && GetBoardFieldEffectType(sourcePiece) == type)
        {
            sourcePiece.ApplyCard(null);
        }
    }

    private void ResolveBoardFieldDestroyedEffect(
        Piece sourcePiece,
        BoardFieldEffectType type
    )
    {
        if (sourcePiece == null)
        {
            return;
        }

        if (type == BoardFieldEffectType.Moat)
        {
            Debug.Log(
                $"[CardDebug][FieldDestroyed] Type=Moat | " +
                $"Owner={(sourcePiece.IsWhite ? "White" : "Black")} | " +
                "Heal=5"
            );
            PlayMoatDestroyedHeal(sourcePiece, 5);
            return;
        }

        if (type == BoardFieldEffectType.ElectricNet)
        {
            StatusDefinition powerGridStatus =
                GetFirstStatus(sourcePiece.cardDefinition);
            int damage = GetPowerGridDamageAmount(powerGridStatus);

            Debug.Log(
                $"[CardDebug][FieldDestroyed] Type=ElectricNet | " +
                $"Owner={(sourcePiece.IsWhite ? "White" : "Black")} | " +
                $"Status={(powerGridStatus != null ? powerGridStatus.statusName : "None")} | " +
                $"Damage={damage}"
            );
            PlayPowerGridDestroyedDamage(sourcePiece, powerGridStatus, damage);
        }
    }

    private void PlayMoatDestroyedHeal(Piece sourcePiece, int healAmount)
    {
        if (sourcePiece == null)
        {
            return;
        }

        bool ownerIsWhite = sourcePiece.IsWhite;
        Vector3 sourcePosition = sourcePiece.transform.position;
        int resolvedHeal = Mathf.Max(0, healAmount);
        Sprite moatIcon = GetFirstStatusIcon(sourcePiece.cardDefinition);

        DamageCalculationSequence sequence = new DamageCalculationSequence
        {
            attacker = sourcePiece,
            target = sourcePiece,
            damagedWhitePlayer = ownerIsWhite,
            isHealing = true,
            finalDamage = resolvedHeal,
            resultStartWorldPosition = sourcePosition
        };

        sequence.steps.Add(new DamageCalculationStep
        {
            icon = moatIcon,
            usePieceIcon = false,
            countingIcon = DamageCountingIcon.Heal,
            side = DamageStepSide.Attack,
            displayText = resolvedHeal.ToString(),
            worldPosition = sourcePosition,
            color = new Color(0.45f, 1f, 0.55f, 1f)
        });

        PlayDamageCalculation(
            sequence,
            null,
            () => HealPlayer(ownerIsWhite, resolvedHeal)
        );
    }

    private Sprite GetFirstStatusIcon(CardDefinition card)
    {
        if (card == null || card.statusesToApply == null)
        {
            return null;
        }

        foreach (StatusDefinition status in card.statusesToApply)
        {
            if (status != null && status.icon != null)
            {
                return status.icon;
            }
        }

        return null;
    }

    private StatusDefinition GetFirstStatus(CardDefinition card)
    {
        return card != null && card.statusesToApply != null &&
            card.statusesToApply.Count > 0
            ? card.statusesToApply[0]
            : null;
    }

    private int GetPowerGridDamageAmount(StatusDefinition powerGridStatus)
    {
        if (powerGridStatus == null || powerGridStatus.effects == null)
        {
            return 5;
        }

        foreach (CardEffectData effect in powerGridStatus.effects)
        {
            if (
                effect != null &&
                effect.effectType == CardEffectType.DamagePlayer
            )
            {
                return Mathf.Max(0, effect.value);
            }
        }

        return 5;
    }

    private void PlayPowerGridDestroyedDamage(
        Piece sourcePiece,
        StatusDefinition powerGridStatus,
        int damage
    )
    {
        if (sourcePiece == null)
        {
            return;
        }

        bool ownerIsWhite = sourcePiece.IsWhite;
        Vector3 sourcePosition = sourcePiece.transform.position;
        int resolvedDamage = Mathf.Max(0, damage);

        DamageCalculationSequence sequence = new DamageCalculationSequence
        {
            damageContext = new DamageContext
            {
                baseDamage = damage,
                source = sourcePiece,
                target = sourcePiece,
                sourceCard = sourcePiece.cardDefinition,
                sourceEffect = GetPowerGridDamageEffect(powerGridStatus),
                trigger = CardEffectTrigger.OnOwnerDestroyed,
                tags = sourcePiece.cardDefinition != null
                    ? sourcePiece.cardDefinition.damageTags
                    : DamageTag.Electric | DamageTag.Skill
            },
            attacker = sourcePiece,
            target = sourcePiece,
            damagedWhitePlayer = ownerIsWhite,
            finalDamage = resolvedDamage,
            resultStartWorldPosition = sourcePosition
        };

        sequence.steps.Add(new DamageCalculationStep
        {
            icon = powerGridStatus != null ? powerGridStatus.icon : null,
            iconPiece = sourcePiece,
            usePieceIcon = powerGridStatus == null ||
                powerGridStatus.icon == null,
            side = DamageStepSide.Attack,
            displayText = resolvedDamage.ToString(),
            worldPosition = sourcePosition,
            color = new Color(1f, 0.45f, 0.9f, 1f)
        });

        resolvedDamage = CardBattle.ResolveDamageDealtWithSteps(
            sourcePiece,
            resolvedDamage,
            sequence,
            false
        );

        resolvedDamage = CardBattle.ResolvePlayerDamageTakenWithSteps(
            ownerIsWhite,
            resolvedDamage,
            sequence
        );
        resolvedDamage = CardBattle.ResolveDamageDealtWithSteps(
            sourcePiece,
            resolvedDamage,
            sequence,
            true
        );
        sequence.finalDamage = resolvedDamage;
        sequence.damageContext.resolvedDamage = resolvedDamage;

        PlayDamageCalculation(
            sequence,
            null,
            () => CardBattle.DamagePlayer(
                ownerIsWhite,
                resolvedDamage,
                false,
                sequence.damageContext
            )
        );
    }

    private CardEffectData GetPowerGridDamageEffect(
        StatusDefinition powerGridStatus
    )
    {
        if (powerGridStatus == null || powerGridStatus.effects == null)
        {
            return null;
        }

        foreach (CardEffectData effect in powerGridStatus.effects)
        {
            if (effect != null &&
                effect.effectType == CardEffectType.DamagePlayer)
            {
                return effect;
            }
        }

        return null;
    }

    public List<BoardFieldEffectZone> GetActiveBoardFieldZones()
    {
        UpdatePiecesOnBoard();

        List<BoardFieldEffectZone> zones =
            new List<BoardFieldEffectZone>();
        List<Piece> moatSources = new List<Piece>();
        List<Piece> electricNetSources = new List<Piece>();

        foreach (Piece piece in piecesOnBoard)
        {
            BoardFieldEffectType type = GetBoardFieldEffectType(piece);

            if (type == BoardFieldEffectType.Moat)
            {
                moatSources.Add(piece);
            }
            else if (type == BoardFieldEffectType.ElectricNet)
            {
                electricNetSources.Add(piece);
            }
        }

        AddAlignedFieldZones(
            zones,
            moatSources,
            BoardFieldEffectType.Moat
        );
        AddAlignedFieldZones(
            zones,
            electricNetSources,
            BoardFieldEffectType.ElectricNet
        );

        return zones;
    }

    private void AddAlignedFieldZones(
        List<BoardFieldEffectZone> zones,
        List<Piece> sources,
        BoardFieldEffectType type
    )
    {
        for (int i = 0; i < sources.Count; i++)
        {
            for (int j = i + 1; j < sources.Count; j++)
            {
                Piece first = sources[i];
                Piece second = sources[j];

                if (
                    first == null ||
                    second == null ||
                    first.IsWhite != second.IsWhite
                )
                {
                    continue;
                }

                Vector2Int firstCell = ToBoardCell(first.GetCoordinates());
                Vector2Int secondCell = ToBoardCell(second.GetCoordinates());

                if (firstCell.x != secondCell.x && firstCell.y != secondCell.y)
                {
                    continue;
                }

                BoardFieldEffectZone zone = BuildFieldZone(
                    type,
                    first,
                    second,
                    firstCell,
                    secondCell
                );

                if (zone.cells.Count == 0)
                {
                    continue;
                }

                zones.Add(zone);
            }
        }
    }

    private BoardFieldEffectZone BuildFieldZone(
        BoardFieldEffectType type,
        Piece first,
        Piece second,
        Vector2Int firstCell,
        Vector2Int secondCell
    )
    {
        BoardFieldEffectZone zone = new BoardFieldEffectZone
        {
            type = type,
            firstSource = first,
            secondSource = second
        };

        Vector2Int direction = new Vector2Int(
            firstCell.x == secondCell.x
                ? 0
                : firstCell.x < secondCell.x ? 1 : -1,
            firstCell.y == secondCell.y
                ? 0
                : firstCell.y < secondCell.y ? 1 : -1
        );

        Vector2Int cursor = firstCell + direction;
        while (cursor != secondCell)
        {
            zone.cells.Add(cursor);
            cursor += direction;
        }

        return zone;
    }

    private bool TryGetFirstFieldCellOnPath(
        Piece mover,
        Vector2Int start,
        Vector2Int target,
        List<BoardFieldEffectZone> zones,
        out Vector2Int fieldCell,
        out BoardFieldEffectType fieldType
    )
    {
        fieldCell = new Vector2Int();
        fieldType = BoardFieldEffectType.None;

        if (mover is Knight)
        {
            return false;
        }

        Vector2Int delta = target - start;
        int absX = Mathf.Abs(delta.x);
        int absY = Mathf.Abs(delta.y);
        bool isStraight = delta.x == 0 || delta.y == 0;
        bool isDiagonal = absX == absY;

        if (!isStraight && !isDiagonal)
        {
            return false;
        }

        Vector2Int step = new Vector2Int(
            delta.x == 0 ? 0 : delta.x / absX,
            delta.y == 0 ? 0 : delta.y / absY
        );

        Vector2Int cursor = start + step;
        while (true)
        {
            foreach (BoardFieldEffectZone zone in zones)
            {
                if (zone.Contains(cursor))
                {
                    fieldCell = cursor;
                    fieldType = zone.type;
                    return true;
                }
            }

            if (cursor == target)
            {
                return false;
            }

            cursor += step;
        }
    }

    private BoardFieldEffectType GetBoardFieldEffectType(Piece piece)
    {
        if (!(piece is Rook) || piece.cardDefinition == null)
        {
            return BoardFieldEffectType.None;
        }

        CardDefinition card = piece.cardDefinition.Api;
        string cardId = card != null ? card.id : string.Empty;
        string cardName = piece.cardDefinition.cardName;

        if (cardId == "J03" || cardName == "護城河")
        {
            return BoardFieldEffectType.Moat;
        }

        if (cardId == "J04" || cardName == "電網")
        {
            return BoardFieldEffectType.ElectricNet;
        }

        return BoardFieldEffectType.None;
    }

    private Vector2Int ToBoardCell(Vector2 position)
    {
        return new Vector2Int(
            Mathf.RoundToInt(position.x),
            Mathf.RoundToInt(position.y)
        );
    }

    private string DescribePieceForField(Piece piece)
    {
        if (piece == null)
        {
            return "None";
        }

        return
            $"{(piece.IsWhite ? "White" : "Black")} " +
            $"{(string.IsNullOrEmpty(piece.PieceType) ? piece.GetType().Name : piece.PieceType)} " +
            $"({piece.GetCoordinates().x:0},{piece.GetCoordinates().y:0})";
    }

    private void CreateBoardFieldVisual(
        Piece source,
        Vector2Int cell,
        BoardFieldEffectType type
    )
    {
        if (source == null)
        {
            return;
        }

        GameObject root = new GameObject($"{type}Clone_{cell.x}_{cell.y}");
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(
            cell.x,
            source.transform.position.y,
            cell.y
        );
        root.transform.rotation = source.transform.rotation;
        root.transform.localScale = source.transform.localScale;

        bool copiedAnyRenderer = false;
        Renderer[] renderers = source.GetComponentsInChildren<Renderer>();
        foreach (Renderer sourceRenderer in renderers)
        {
            Mesh mesh = GetRendererMesh(sourceRenderer);
            if (mesh == null)
            {
                continue;
            }

            GameObject part = new GameObject(sourceRenderer.name);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition =
                source.transform.InverseTransformPoint(
                    sourceRenderer.transform.position
                );
            part.transform.localRotation =
                Quaternion.Inverse(source.transform.rotation) *
                sourceRenderer.transform.rotation;
            part.transform.localScale = sourceRenderer.transform.localScale;

            MeshFilter meshFilter = part.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = part.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial =
                CreateBoardFieldVisualMaterial(
                    sourceRenderer.sharedMaterial,
                    type
                );
            copiedAnyRenderer = true;
        }

        if (!copiedAnyRenderer)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Fallback";
            cube.transform.SetParent(root.transform, false);
            cube.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial =
                    CreateBoardFieldVisualMaterial(null, type);
            }

            Collider collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        boardFieldVisuals.Add(root);
    }

    private Mesh GetRendererMesh(Renderer renderer)
    {
        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            return meshFilter.sharedMesh;
        }

        SkinnedMeshRenderer skinnedRenderer =
            renderer as SkinnedMeshRenderer;
        return skinnedRenderer != null
            ? skinnedRenderer.sharedMesh
            : null;
    }

    private Material CreateFallbackBoardFieldVisualMaterial(
        BoardFieldEffectType type
    )
    {
        if (boardFieldVisualMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            boardFieldVisualMaterial = new Material(shader);
        }

        Material material = new Material(boardFieldVisualMaterial);
        ApplyBoardFieldColor(material, type);
        boardFieldVisualMaterials.Add(material);
        return material;
    }

    private Material CreateBoardFieldVisualMaterial(
        Material sourceMaterial,
        BoardFieldEffectType type
    )
    {
        bool canUseSource =
            sourceMaterial != null &&
            sourceMaterial.shader != null &&
            sourceMaterial.shader.name != "Hidden/InternalErrorShader";

        if (!canUseSource)
        {
            return CreateFallbackBoardFieldVisualMaterial(type);
        }

        Material material = new Material(sourceMaterial);
        ApplyBoardFieldColor(material, type);
        boardFieldVisualMaterials.Add(material);
        return material;
    }

    private void ApplyBoardFieldColor(
        Material material,
        BoardFieldEffectType type
    )
    {
        if (material == null)
        {
            return;
        }

        Color color = type == BoardFieldEffectType.ElectricNet
            ? new Color(0.55f, 0.18f, 1f, 0.8f)
            : new Color(0.15f, 0.45f, 1f, 0.75f);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private void ClearBoardFieldVisuals()
    {
        for (int i = boardFieldVisuals.Count - 1; i >= 0; i--)
        {
            if (boardFieldVisuals[i] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(boardFieldVisuals[i]);
            }
            else
            {
                DestroyImmediate(boardFieldVisuals[i]);
            }
        }

        boardFieldVisuals.Clear();

        for (int i = boardFieldVisualMaterials.Count - 1; i >= 0; i--)
        {
            if (boardFieldVisualMaterials[i] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(boardFieldVisualMaterials[i]);
            }
            else
            {
                DestroyImmediate(boardFieldVisualMaterials[i]);
            }
        }

        boardFieldVisualMaterials.Clear();
    }

    public void PlayDamageCalculation(
        DamageCalculationSequence sequence,
        System.Action onCaptureVisual,
        System.Action onDamageApplied
    )
    {
        if (IsNetworkClientOnly && !isApplyingRemoteDamageCalculation)
        {
            Debug.Log(
                "[NetworkGame][DamageVisual] Client skipped local-only damage calculation; waiting for host visual sync."
            );
            return;
        }

        DamageCalculationRequest request = new DamageCalculationRequest(
            sequence,
            onCaptureVisual,
            onDamageApplied
        );

        if (damageCalculationBatchDepth > 0)
        {
            pendingDamageBatch.Add(request);
            return;
        }

        EnqueueDamageBatch(new List<DamageCalculationRequest> { request });
    }

    private void BeginDamageCalculationBatch()
    {
        damageCalculationBatchDepth++;
    }

    private void EndDamageCalculationBatch()
    {
        if (damageCalculationBatchDepth <= 0)
        {
            return;
        }

        damageCalculationBatchDepth--;
        if (damageCalculationBatchDepth > 0)
        {
            return;
        }

        if (pendingDamageBatch.Count == 0)
        {
            return;
        }

        List<DamageCalculationRequest> batch =
            new List<DamageCalculationRequest>(pendingDamageBatch);
        pendingDamageBatch.Clear();
        EnqueueDamageBatch(batch);
    }

    private void EnqueueDamageBatch(List<DamageCalculationRequest> requests)
    {
        if (requests == null || requests.Count == 0)
        {
            return;
        }

        BroadcastDamageCalculationBatchIfAuthority(requests);
        damageCalculationQueue.Enqueue(new DamageCalculationBatch(requests));

        if (!isDamageCalculationPlaying)
        {
            PlayNextDamageCalculation();
        }
    }

    public void PlayRemoteDamageCalculationBatch(string payload)
    {
        NetworkDamageCalculationBatch networkBatch =
            DeserializeDamageCalculationBatch(payload);
        if (
            networkBatch == null ||
            networkBatch.sequences == null ||
            networkBatch.sequences.Count == 0
        )
        {
            return;
        }

        List<DamageCalculationRequest> requests =
            new List<DamageCalculationRequest>();
        foreach (NetworkDamageCalculationSequence networkSequence
            in networkBatch.sequences)
        {
            DamageCalculationSequence sequence =
                BuildRemoteDamageCalculationSequence(networkSequence);
            if (sequence == null)
            {
                continue;
            }

            LogRemoteDamageSequence(sequence);
            System.Action captureVisual = sequence.isCapture
                ? () => PlayRemoteCaptureVisual(sequence)
                : null;

            requests.Add(new DamageCalculationRequest(sequence, captureVisual, null));
        }

        if (requests.Count == 0)
        {
            return;
        }

        bool previous = isApplyingRemoteDamageCalculation;
        isApplyingRemoteDamageCalculation = true;
        try
        {
            EnqueueDamageBatch(requests);
        }
        finally
        {
            isApplyingRemoteDamageCalculation = previous;
        }
    }

    private void LogRemoteDamageSequence(DamageCalculationSequence sequence)
    {
        if (sequence == null || sequence.finalDamage <= 0)
        {
            return;
        }

        if (sequence.isHealing)
        {
            OperateLogUI.LogHeal(
                sequence.damagedWhitePlayer,
                sequence.finalDamage
            );
        }
        else
        {
            OperateLogUI.LogDamage(
                sequence.damagedWhitePlayer,
                sequence.finalDamage,
                sequence.damageContext
            );
        }
    }

    private void BroadcastDamageCalculationBatchIfAuthority(
        List<DamageCalculationRequest> requests
    )
    {
        if (
            isApplyingRemoteDamageCalculation ||
            Multiplayer == null ||
            !Multiplayer.IsServerAuthority
        )
        {
            return;
        }

        string payload = SerializeDamageCalculationBatch(requests);
        if (string.IsNullOrEmpty(payload))
        {
            return;
        }

        Multiplayer.BroadcastDamageCalculationBatch(payload);
    }

    private string SerializeDamageCalculationBatch(
        List<DamageCalculationRequest> requests
    )
    {
        NetworkDamageCalculationBatch batch =
            new NetworkDamageCalculationBatch();

        foreach (DamageCalculationRequest request in requests)
        {
            DamageCalculationSequence sequence = request.sequence;
            if (sequence == null)
            {
                continue;
            }

            NetworkDamageCalculationSequence networkSequence =
                new NetworkDamageCalculationSequence
                {
                    damagedWhitePlayer = sequence.damagedWhitePlayer,
                    isHealing = sequence.isHealing,
                    isCapture = sequence.isCapture,
                    finalDamage = sequence.finalDamage,
                    resultStartWorldPosition = sequence.resultStartWorldPosition,
                    attacker = GetPieceCoordinateOrInvalid(sequence.attacker),
                    target = GetPieceCoordinateOrInvalid(sequence.target)
                };

            foreach (DamageCalculationStep step in sequence.steps)
            {
                if (step == null)
                {
                    continue;
                }

                networkSequence.steps.Add(
                    new NetworkDamageCalculationStep
                    {
                        displayText = step.displayText,
                        worldPosition = step.worldPosition,
                        color = step.color,
                        usePieceIcon = step.usePieceIcon,
                        useTargetPieceIcon = step.useTargetPieceIcon,
                        iconPiece = GetPieceCoordinateOrInvalid(step.iconPiece),
                        countingIcon = (int)step.countingIcon,
                        side = (int)step.side
                    }
                );
            }

            batch.sequences.Add(networkSequence);
        }

        return batch.sequences.Count > 0 ? JsonUtility.ToJson(batch) : string.Empty;
    }

    private NetworkDamageCalculationBatch DeserializeDamageCalculationBatch(
        string payload
    )
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<NetworkDamageCalculationBatch>(payload);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"[NetworkGame][DamageVisual] Failed to parse payload: {exception.Message}"
            );
            return null;
        }
    }

    private DamageCalculationSequence BuildRemoteDamageCalculationSequence(
        NetworkDamageCalculationSequence networkSequence
    )
    {
        if (networkSequence == null)
        {
            return null;
        }

        DamageCalculationSequence sequence = new DamageCalculationSequence
        {
            damagedWhitePlayer = networkSequence.damagedWhitePlayer,
            isHealing = networkSequence.isHealing,
            isCapture = networkSequence.isCapture,
            finalDamage = networkSequence.finalDamage,
            resultStartWorldPosition = networkSequence.resultStartWorldPosition,
            attacker = FindPieceAt(networkSequence.attacker),
            target = FindPieceAt(networkSequence.target)
        };

        if (networkSequence.steps == null)
        {
            return sequence;
        }

        foreach (NetworkDamageCalculationStep networkStep
            in networkSequence.steps)
        {
            if (networkStep == null)
            {
                continue;
            }

            sequence.steps.Add(
                new DamageCalculationStep
                {
                    displayText = networkStep.displayText,
                    worldPosition = networkStep.worldPosition,
                    color = networkStep.color,
                    usePieceIcon = networkStep.usePieceIcon,
                    useTargetPieceIcon = networkStep.useTargetPieceIcon,
                    iconPiece = FindPieceAt(networkStep.iconPiece),
                    countingIcon =
                        (DamageCountingIcon)networkStep.countingIcon,
                    side = (DamageStepSide)networkStep.side
                }
            );
        }

        return sequence;
    }

    private BoardCoordinate GetPieceCoordinateOrInvalid(Piece piece)
    {
        if (piece == null)
        {
            return new BoardCoordinate(-1, -1);
        }

        Vector2 coordinates = piece.GetCoordinates();
        BoardCoordinate boardCoordinate =
            BoardCoordinate.FromVector2(coordinates);
        if (
            boardCoordinate.IsValid &&
            boardMap[boardCoordinate.x, boardCoordinate.y] == piece
        )
        {
            return boardCoordinate;
        }

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (boardMap[x, y] == piece)
                {
                    return new BoardCoordinate(x, y);
                }
            }
        }

        return new BoardCoordinate(-1, -1);
    }

    private Piece FindPieceAt(BoardCoordinate coordinate)
    {
        if (!coordinate.IsValid)
        {
            return null;
        }

        return boardMap[coordinate.x, coordinate.y];
    }

    private Piece FindCapturedPieceForMove(Piece mover, BoardCoordinate to)
    {
        if (mover == null || !to.IsValid)
        {
            return null;
        }

        Piece destinationPiece = boardMap[to.x, to.y];
        if (destinationPiece != null && destinationPiece.IsWhite != mover.IsWhite)
        {
            return destinationPiece;
        }

        if (mover is Pawn)
        {
            Vector2 from = mover.GetCoordinates();
            bool diagonalPawnMove =
                Mathf.Abs(to.x - Mathf.RoundToInt(from.x)) == 1 &&
                Mathf.Abs(to.y - Mathf.RoundToInt(from.y)) == 1;
            if (diagonalPawnMove)
            {
                int capturedY = Mathf.RoundToInt(from.y);
                Piece enPassantPiece = boardMap[to.x, capturedY];
                if (
                    enPassantPiece is Pawn &&
                    enPassantPiece.IsWhite != mover.IsWhite
                )
                {
                    return enPassantPiece;
                }
            }
        }

        return null;
    }

    private IEnumerator EnsureRemoteCapturedPieceRemoved(Piece capturedPiece)
    {
        int capturedInstanceId = capturedPiece != null
            ? capturedPiece.GetInstanceID()
            : 0;

        yield return WaitForDamageCalculations();

        if (capturedPiece == null)
        {
            yield break;
        }

        if (remoteCaptureVisualizedPieceIds.Remove(capturedInstanceId))
        {
            yield break;
        }

        Vector2 coordinates = capturedPiece.GetCoordinates();
        BoardCoordinate boardCoordinate =
            BoardCoordinate.FromVector2(coordinates);
        if (
            boardCoordinate.IsValid &&
            boardMap[boardCoordinate.x, boardCoordinate.y] == capturedPiece
        )
        {
            boardMap[boardCoordinate.x, boardCoordinate.y] = null;
        }

        capturedPiece.PlayCapturedAnimation();
        UpdatePiecesOnBoard();
        RefreshBoardFieldEffects();

        Debug.Log(
            $"[NetworkGame][RemoteCaptureCleanup] Removed={capturedPiece.name} | " +
            $"At={boardCoordinate}"
        );
    }

    private void PlayRemoteCaptureVisual(DamageCalculationSequence sequence)
    {
        if (sequence == null)
        {
            return;
        }

        if (sequence.attacker != null)
        {
            sequence.attacker.PlayCaptureAnimation();
        }

        if (
            sequence.target != null &&
            sequence.target != sequence.attacker
        )
        {
            remoteCaptureVisualizedPieceIds.Add(sequence.target.GetInstanceID());
            sequence.target.PlayCapturedAnimation();
        }
    }

    private void PlayNextDamageCalculation()
    {
        if (damageVisualizer == null)
        {
            damageVisualizer =
                Object.FindFirstObjectByType<DamageCalculationVisualizer>();
        }

        if (damageVisualizer == null)
        {
            Debug.LogError(
                "LogicManager requires a DamageCalculationVisualizer in scene. " +
                "Put it on DMGSystem and assign it before playing."
            );
            while (damageCalculationQueue.Count > 0)
            {
                DamageCalculationBatch skippedBatch =
                    damageCalculationQueue.Dequeue();
                skippedBatch.InvokeCaptureVisuals();
                skippedBatch.InvokeDamageApplied();
            }

            return;
        }

        if (damageCalculationQueue.Count == 0)
        {
            isDamageCalculationPlaying = false;
            if (EnsureCardHandManager())
            {
                cardHandManager.Refresh(isWhiteTurn);
            }
            return;
        }

        DamageCalculationBatch batch =
            damageCalculationQueue.Dequeue();

        isDamageCalculationPlaying = true;
        if (batch.Count <= 1)
        {
            DamageCalculationRequest request = batch.Requests[0];
            damageVisualizer.Play(
                request.sequence,
                request.onCaptureVisual,
                () =>
                {
                    request.onDamageApplied?.Invoke();
                    isDamageCalculationPlaying = false;
                    PlayNextDamageCalculation();
                }
            );
        }
        else
        {
            damageVisualizer.PlayBatch(
                batch.Sequences,
                batch.CaptureVisuals,
                batch.DamageApplied,
                () =>
                {
                    isDamageCalculationPlaying = false;
                    PlayNextDamageCalculation();
                }
            );
        }
    }

    private class DamageCalculationRequest
    {
        public readonly DamageCalculationSequence sequence;
        public readonly System.Action onCaptureVisual;
        public readonly System.Action onDamageApplied;

        public DamageCalculationRequest(
            DamageCalculationSequence sequence,
            System.Action onCaptureVisual,
            System.Action onDamageApplied
        )
        {
            this.sequence = sequence;
            this.onCaptureVisual = onCaptureVisual;
            this.onDamageApplied = onDamageApplied;
        }
    }

    private class DamageCalculationBatch
    {
        public readonly List<DamageCalculationRequest> Requests;
        public readonly List<DamageCalculationSequence> Sequences =
            new List<DamageCalculationSequence>();
        public readonly List<System.Action> CaptureVisuals =
            new List<System.Action>();
        public readonly List<System.Action> DamageApplied =
            new List<System.Action>();

        public DamageCalculationBatch(List<DamageCalculationRequest> requests)
        {
            Requests = requests;
            foreach (DamageCalculationRequest request in requests)
            {
                Sequences.Add(request.sequence);
                CaptureVisuals.Add(request.onCaptureVisual);
                DamageApplied.Add(request.onDamageApplied);
            }
        }

        public int Count
        {
            get { return Requests != null ? Requests.Count : 0; }
        }

        public void InvokeCaptureVisuals()
        {
            foreach (System.Action action in CaptureVisuals)
            {
                action?.Invoke();
            }
        }

        public void InvokeDamageApplied()
        {
            foreach (System.Action action in DamageApplied)
            {
                action?.Invoke();
            }
        }
    }

    public void DealDamageToPiece(
        Piece source,
        Piece target,
        int baseDamage,
        CardEffectTrigger sourceTrigger,
        DamageTag damageTags = DamageTag.None
    )
    {
        CardBattle.DealDamageToPiece(
            source,
            target,
            baseDamage,
            sourceTrigger,
            damageTags
        );
    }

    public void DealFixedDamageToPiece(
        Piece source,
        Piece target,
        int damage,
        CardEffectTrigger sourceTrigger,
        DamageTag damageTags = DamageTag.None
    )
    {
        CardBattle.DealFixedDamageToPiece(
            source,
            target,
            damage,
            sourceTrigger,
            damageTags
        );
    }

    public void DealEventDamageToPiece(
        CardDefinition card,
        Piece target,
        int damage
    )
    {
        CardBattle.DealEventDamageToPiece(card, target, damage);
    }

    public void HealPlayerFromEvent(
        CardDefinition card,
        Piece target,
        int amount
    )
    {
        CardBattle.HealPlayerFromEvent(card, target, amount);
    }

    public void HealPlayer(bool isWhitePlayer, int amount)
    {
        CardBattle.HealPlayer(isWhitePlayer, amount);
    }

    public void DamagePlayer(bool isWhitePlayer, int amount)
    {
        CardBattle.DamagePlayer(isWhitePlayer, amount);
    }

    public void PayHealthCost(
        bool isWhitePlayer,
        int amount,
        CardDefinition sourceCard = null,
        Piece visualSource = null
    )
    {
        CardBattle.PayHealthCost(
            isWhitePlayer,
            amount,
            sourceCard,
            visualSource
        );
    }

    public bool QueueCardForPlayer(
        bool isWhitePlayer,
        CardDefinition card,
        int ownerTurnsDelay
    )
    {
        return EnsureCardHandManager() &&
            cardHandManager.QueueCardForPlayer(
                isWhitePlayer,
                card,
                ownerTurnsDelay
            );
    }

    public int GetCapturedPieceCount(bool isWhitePlayer)
    {
        return isWhitePlayer
            ? whiteCapturedPieceCount
            : blackCapturedPieceCount;
    }

    public void RegisterPieceCaptured(Piece capturedPiece)
    {
        if (capturedPiece == null)
        {
            return;
        }

        if (capturedPiece.IsWhite)
        {
            whiteCapturedPieceCount++;
        }
        else
        {
            blackCapturedPieceCount++;
        }

        Debug.Log(
            $"[CardDebug][CapturedCount] Side=" +
            $"{(capturedPiece.IsWhite ? "White" : "Black")} | " +
            $"White={whiteCapturedPieceCount} | Black={blackCapturedPieceCount}"
        );
    }

    public void RemoveKingForCard(bool isWhitePlayer, string reason)
    {
        UpdatePiecesOnBoard();

        foreach (Piece piece in piecesOnBoard.ToArray())
        {
            if (piece is King && piece.IsWhite == isWhitePlayer)
            {
                Vector2 position = piece.GetCoordinates();
                boardMap[
                    Mathf.RoundToInt(position.x),
                    Mathf.RoundToInt(position.y)
                ] = null;

                Debug.Log(
                    $"[CardDebug][RemoveKing] Side=" +
                    $"{(isWhitePlayer ? "White" : "Black")} | " +
                    $"Reason={reason}"
                );

                Destroy(piece.gameObject);
                UpdatePiecesOnBoard();
                UpdateCheckMap();
                return;
            }
        }
    }

    public Piece GetRandomPiece(bool isWhitePlayer)
    {
        return CardBattle.GetRandomPiece(isWhitePlayer);
    }

    public bool TryPlayFieldCard(CardDefinition card)
    {
        Debug.Log(
            $"[CardDebug][FieldRejected] Card=" +
            $"{(card != null ? card.id + " " + card.cardName : "None")} | " +
            "Reason=Field card must be dropped on FieldCardPlace"
        );
        return false;
    }

    public bool TryPlayFieldCard(CardDefinition card, FieldCardPlace place)
    {
        if (card == null || card.cardType != CardType.Field)
        {
            GameFlowUI.Show("???臬?啣");
            return false;
        }

        ResolveFieldCardPlaces();

        if (place == null || !IsKnownFieldPlace(place))
        {
            Debug.Log(
                $"[CardDebug][FieldRejected] Card={card.id} {card.cardName} | " +
                "Reason=Invalid field place"
            );
            GameFlowUI.Show("?游?∪???啣?啣?");
            return false;
        }

        if (isFieldFusionPlaying)
        {
            Debug.Log(
                $"[CardDebug][FieldRejected] Card={card.id} {card.cardName} | " +
                "Reason=Field fusion animation playing"
            );
            GameFlowUI.Show("場地合成中，暫時不能放");
            return false;
        }

        if (place.ActiveCard != null)
        {
            Debug.Log(
                $"[CardDebug][FieldRejected] Card={card.id} {card.cardName} | " +
                $"Reason=Field place occupied | Place={place.name}"
            );
            GameFlowUI.Show("??啣?撌脫??∠?");
            return false;
        }

        int cardSlotCost = GetFieldSlotCost(card);
        if (cardSlotCost > GetEmptyFieldPlaceCount())
        {
            Debug.Log(
                $"[CardDebug][FieldRejected] Card={card.id} {card.cardName} | " +
                "Reason=Field limit reached"
            );
            GameFlowUI.Show("場地卡已達上限");
            return false;
        }

        if (cardSlotCost > 1)
        {
            foreach (FieldCardPlace fieldPlace in fieldCardPlaces)
            {
                if (fieldPlace != null)
                {
                    fieldPlace.SetCard(card);
                }
            }
        }
        else
        {
            place.SetCard(card);
        }

        activeFieldCards.Add(card);
        bool fused = ResolveFieldFusion();
        Debug.Log(
            $"[CardDebug][FieldPlayed] Card={card.id} {card.cardName} | " +
            $"Place={place.name} | " +
            $"ActiveFields={GetActiveFieldSlotCount()}/2"
        );

        if (!fused && GetActiveFieldCount("F12") > 0)
        {
            Debug.Log("[CardDebug][FieldReset] Field=F12 摰踹蝯?銋 | Reload ChessScene");
            RequestFullGameRestart();
        }

        return true;
    }

    public int GetActiveFieldCount(string cardId)
    {
        if (string.IsNullOrEmpty(cardId))
        {
            return 0;
        }

        int count = 0;
        foreach (CardDefinition field in activeFieldCards)
        {
            if (field != null && field.id == cardId)
            {
                count++;
            }
        }

        return count;
    }

    public int GetFieldAttackBonus()
    {
        return GetActiveFieldCount("F07") + GetActiveFieldCount("F08") * 5;
    }

    public int GetFieldValueModifier()
    {
        return GetActiveFieldCount("F04") * -1;
    }

    public int ApplyFieldDamageDealtModifiers(
        DamageContext damageContext,
        int baseDamage,
        DamageCalculationSequence sequence
    )
    {
        if (damageContext == null)
        {
            return Mathf.Max(0, baseDamage);
        }

        int result = Mathf.Max(0, baseDamage);
        foreach (CardDefinition field in activeFieldCards)
        {
            if (field == null) continue;

            int modifier = GetFieldDamageDealtModifier(field, damageContext);
            bool applies = modifier != 0;

            if (!applies) continue;

            int previous = result;
            result = Mathf.Max(0, result + modifier);
            AddFieldCalculationStep(
                sequence,
                field,
                DamageCountingIcon.Attack,
                DamageStepSide.Attack,
                Mathf.Abs(result - previous),
                GetFieldStepPosition(sequence),
                result >= previous
                    ? new Color(0.45f, 1f, 0.55f, 1f)
                    : new Color(1f, 0.45f, 0.35f, 1f)
            );

            Debug.Log(
                $"[CardDebug][FieldDamageDealt] Field={field.id} {field.cardName} | " +
                $"Tags={damageContext.tags} | Damage={previous}->{result}"
            );
        }

        return result;
    }

    public int ApplyFieldHealModifiers(
        bool healedWhitePlayer,
        int baseHeal,
        DamageCalculationSequence sequence
    )
    {
        int result = Mathf.Max(0, baseHeal);
        foreach (CardDefinition field in activeFieldCards)
        {
            if (field == null || field.id != "F06") continue;

            int previous = result;
            result = Mathf.Max(0, result + 5);
            AddFieldCalculationStep(
                sequence,
                field,
                DamageCountingIcon.Heal,
                DamageStepSide.Attack,
                result - previous,
                GetFieldStepPosition(sequence),
                new Color(0.45f, 1f, 0.55f, 1f)
            );

            Debug.Log(
                $"[CardDebug][FieldHeal] Field={field.id} {field.cardName} | " +
                $"Player={(healedWhitePlayer ? "White" : "Black")} | " +
                $"Heal={previous}->{result}"
            );
        }

        return result;
    }

    public int ApplyFieldPlayerDamageTakenModifiers(
        bool damagedWhitePlayer,
        int baseDamage,
        DamageCalculationSequence sequence
    )
    {
        int result = Mathf.Max(0, baseDamage);
        foreach (CardDefinition field in activeFieldCards)
        {
            if (field == null || field.id != "F05") continue;

            int previous = result;
            result = Mathf.Max(0, result - 1);
            AddFieldCalculationStep(
                sequence,
                field,
                DamageCountingIcon.Defense,
                DamageStepSide.Defense,
                previous - result,
                GetFieldStepPosition(sequence),
                new Color(0.45f, 0.75f, 1f, 1f)
            );

            Debug.Log(
                $"[CardDebug][FieldDamageTaken] Field={field.id} {field.cardName} | " +
                $"Player={(damagedWhitePlayer ? "White" : "Black")} | " +
                $"Damage={previous}->{result}"
            );
        }

        return result;
    }

    public int GetEffectiveAttack(Piece piece)
    {
        return CardBattle.GetEffectiveAttack(piece);
    }

    public int GetEffectiveValue(Piece piece)
    {
        return CardBattle.GetEffectiveValue(piece);
    }

    public Square GetSquareAtPosition(Vector2 position)
    {
        int x = Mathf.RoundToInt(position.x);
        int y = Mathf.RoundToInt(position.y);

        if (x < 0 || x >= 8 || y < 0 || y >= 8)
        {
            return null;
        }

        return squares[x, y];
    }

    /// <summary>
    /// ?撱箇???餅??啣???    /// 隞颱?璉?蝘餃???霈霈宏????嚗?閬??啗?蝞?    /// </summary>
    public void UpdateCheckMap()
    {
        ResetCheckMap();
        UpdatePiecesOnBoard();

        foreach (Piece piece in piecesOnBoard)
        {
            if (piece == null)
            {
                continue;
            }

            List<Vector2> attackedFields = piece.GetAttackedFields();
            foreach (Vector2 field in attackedFields)
            {
                int x = Mathf.RoundToInt(field.x);
                int y = Mathf.RoundToInt(field.y);

                if (x < 0 || x >= 8 || y < 0 || y >= 8)
                {
                    continue;
                }

                if (piece.IsWhite)
                {
                    whiteCheckMap[x, y] = true;
                }
                else
                {
                    blackCheckMap[x, y] = true;
                }
            }
        }
    }

    public void ResetCheckMap()
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                whiteCheckMap[x, y] = false;
                blackCheckMap[x, y] = false;
            }
        }
    }

    public bool CheckKingStatus()
    {
        UpdatePiecesOnBoard();

        foreach (Piece piece in piecesOnBoard)
        {
            if (piece is King king && piece.IsWhite == isWhiteTurn)
            {
                return king.CheckForChecks();
            }
        }

        return false;
    }

    public void UpdatePiecesOnBoard()
    {
        piecesOnBoard.Clear();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (boardMap[x, y] != null)
                {
                    piecesOnBoard.Add(boardMap[x, y]);
                }
            }
        }
    }

    /// <summary>
    /// 镼踵?璉?鞎炎?乓?    /// EndTurn ?銝?雿摰嗅??澆嚗?甇斗炎?亦??舐?摰嗆?西◤撠香?澆???    /// </summary>
    public void CheckGameOver()
    {
        if (IsNetworkClientOnly)
        {
            return;
        }

        if (CheckHealthGameOver())
        {
            return;
        }

        UpdateCheckMap();
        UpdatePiecesOnBoard();

        bool hasLegalMove = false;
        bool currentKingInCheck = CheckKingStatus();
        SetCheckAlarm(currentKingInCheck);

        Piece[] pieceSnapshot = piecesOnBoard.ToArray();

        foreach (Piece piece in pieceSnapshot)
        {
            if (piece == null || piece.IsWhite != isWhiteTurn)
            {
                continue;
            }

            if (piece.GetLegalMoves().Count > 0)
            {
                hasLegalMove = true;
                break;
            }
        }

        if (hasLegalMove)
        {
            CheckInsufficientMaterial();
            return;
        }

        ShowGameOver(currentKingInCheck
            ? (isWhiteTurn ? "Black Wins" : "White Wins")
            : "Draw");
    }

    private void SetCheckAlarm(bool currentKingInCheck)
    {
        string side = isWhiteTurn ? "白方" : "黑方";
        GameFlowUI.SetPersistent(
            "Check",
            $"{side}被將軍",
            currentKingInCheck
        );
    }

    public void HandlePromotion(Pawn pawn)
    {
        if (promotionUI == null)
        {
            Debug.LogError("LogicManager requires PromotionUI to be assigned.");
            return;
        }

        isPromotionActive = true;

        if (multiplayerGameController == null)
        {
            multiplayerGameController =
                FindFirstObjectByType<MultiplayerGameController>();
        }

        if (
            multiplayerGameController == null ||
            !multiplayerGameController.IsOnline ||
            multiplayerGameController.ShouldLocalChoosePromotion(pawn.IsWhite)
        )
        {
            promotionUI.Show(pawn);
        }
        else
        {
            promotionUI.Hide();
            Debug.Log(
                $"[NetworkGame][PromotionPending] Waiting for " +
                $"{(pawn.IsWhite ? "White" : "Black")} player choice."
            );
        }
    }

    public bool ApplyPromotionChoice(
        BoardCoordinate coordinate,
        bool isWhitePlayer,
        string pieceName,
        bool endTurnAfterPromotion
    )
    {
        if (!coordinate.IsValid)
        {
            Debug.Log(
                $"[NetworkGame][PromotionRejected] Invalid coordinate {coordinate}"
            );
            return false;
        }

        Pawn pawn = boardMap[coordinate.x, coordinate.y] as Pawn;
        if (pawn == null || pawn.IsWhite != isWhitePlayer)
        {
            Debug.Log(
                $"[NetworkGame][PromotionRejected] No matching pawn | " +
                $"Cell={coordinate} | Piece={boardMap[coordinate.x, coordinate.y]}"
            );
            return false;
        }

        if (!TryGetPromotionPrefabIndex(
            pieceName,
            isWhitePlayer,
            out int prefabIndex,
            out string pieceType
        ))
        {
            Debug.Log(
                $"[NetworkGame][PromotionRejected] Invalid piece type {pieceName}"
            );
            return false;
        }

        if (board == null)
        {
            board = FindFirstObjectByType<Board>();
        }

        if (board == null)
        {
            Debug.LogError("LogicManager requires Board for promotion.");
            return false;
        }

        Vector3 pawnPosition = pawn.transform.position;
        Material material = isWhitePlayer
            ? board.PieceMaterials[0]
            : board.PieceMaterials[1];

        boardMap[coordinate.x, coordinate.y] = null;
        Destroy(pawn.gameObject);

        Piece promotedPiece = board.InstantiatePiece(
            board.PiecePrefabs[prefabIndex],
            pawnPosition,
            material,
            pieceType,
            isWhitePlayer
        );

        if (promotedPiece != null)
        {
            boardMap[coordinate.x, coordinate.y] = promotedPiece;
        }

        isPromotionActive = false;
        if (promotionUI != null)
        {
            promotionUI.Hide();
        }

        UpdatePiecesOnBoard();
        UpdateCheckMap();

        if (endTurnAfterPromotion)
        {
            EndTurn();
        }

        Debug.Log(
            $"[NetworkGame][PromotionApplied] Player=" +
            $"{(isWhitePlayer ? "White" : "Black")} | " +
            $"Cell={coordinate} | Piece={pieceType}"
        );
        return true;
    }

    private bool TryGetPromotionPrefabIndex(
        string pieceName,
        bool isWhitePlayer,
        out int prefabIndex,
        out string pieceType
    )
    {
        switch (pieceName)
        {
            case "Queen":
                prefabIndex = isWhitePlayer ? 8 : 9;
                pieceType = "Queen";
                return true;
            case "Rook":
                prefabIndex = isWhitePlayer ? 2 : 3;
                pieceType = "Rook";
                return true;
            case "Bishop":
                prefabIndex = isWhitePlayer ? 6 : 7;
                pieceType = "Bishop";
                return true;
            case "Knight":
                prefabIndex = isWhitePlayer ? 4 : 5;
                pieceType = "Knight";
                return true;
            default:
                prefabIndex = -1;
                pieceType = string.Empty;
                return false;
        }
    }

    public void ToggleCameraRotation(bool enabled)
    {
        isCameraRotationEnabled = enabled;
    }

    public void ToggleSound(bool enabled)
    {
        isSoundEnabled = enabled;

        if (moveSound != null)
        {
            moveSound.mute = !enabled;
        }

        if (captureSound != null)
        {
            captureSound.mute = !enabled;
        }
    }

    public void SetSoundVolume(float volume)
    {
        soundVolume = Mathf.Clamp01(volume);

        if (moveSound != null)
        {
            moveSound.volume = soundVolume;
        }

        if (captureSound != null)
        {
            captureSound.volume = soundVolume;
        }
    }

    public void RefreshHealthUi()
    {
        if (EnsureCardHandManager())
        {
            cardHandManager.RefreshHealth();
        }

        BroadcastNetworkStateIfAuthority();
    }

    public bool CheckHealthGameOver()
    {
        if (IsNetworkClientOnly)
        {
            return false;
        }

        if (whiteHealth <= 0)
        {
            ShowGameOver("Black Wins");
            return true;
        }

        if (blackHealth <= 0)
        {
            ShowGameOver("White Wins");
            return true;
        }

        return false;
    }

    public void EndGameByCard(string result, string reason)
    {
        Debug.Log(
            $"[CardDebug][CardGameOver] Result={result} | Reason={reason}"
        );
        ShowGameOver(result);
    }

    private void CheckInsufficientMaterial()
    {
        int nonKingPieces = 0;

        foreach (Piece piece in piecesOnBoard)
        {
            if (piece != null && !(piece is King))
            {
                nonKingPieces++;
            }
        }

        if (nonKingPieces == 0)
        {
            ShowGameOver("Draw");
        }
    }

    private void ShowGameOver(string result)
    {
        if (IsNetworkClientOnly)
        {
            return;
        }

        Multiplayer?.BroadcastGameOver(result);
        ShowGameOverLocal(result);
    }

    private void ShowGameOverLocal(string result)
    {
        Debug.Log($"[CardDebug][GameOver] Result={result}");

        if (gameOverUI != null)
        {
            gameOverUI.ShowGameOver(result);
        }
        else
        {
            Debug.LogError("LogicManager requires GameOverUI to be assigned.");
        }

        Time.timeScale = 0f;
    }

    private void BroadcastNetworkStateIfAuthority()
    {
        Multiplayer?.BroadcastState(isWhiteTurn, whiteHealth, blackHealth);
    }

    private void RotateCameraForCurrentTurn()
    {
        if (!isCameraRotationEnabled || cameraController == null)
        {
            return;
        }

        if (isWhiteTurn)
        {
            cameraController.WhitePerspective();
        }
        else
        {
            cameraController.BlackPerspective();
        }
    }

    private bool EnsureCardHandManager()
    {
        if (cardHandManager == null)
        {
            Debug.LogError(
                "LogicManager requires CardHandManager to be assigned."
            );
            return false;
        }

        if (!isCardHandManagerInitialized)
        {
            cardHandManager.Initialize(this);
            isCardHandManagerInitialized = true;
        }

        SubscribeCardHandEvents();

        return true;
    }

    private void SubscribeCardHandEvents()
    {
        if (cardHandManager == null || isSubscribedToCardHandEvents)
        {
            return;
        }

        cardHandManager.CardRecycled += OnCardRecycled;
        cardHandManager.CardsDrawn += OnCardsDrawn;
        isSubscribedToCardHandEvents = true;
    }

    private void OnDestroy()
    {
        if (cardHandManager != null && isSubscribedToCardHandEvents)
        {
            cardHandManager.CardRecycled -= OnCardRecycled;
            cardHandManager.CardsDrawn -= OnCardsDrawn;
        }
    }

    private void OnCardRecycled(CardRecycleEvent recycleEvent)
    {
        if (recycleEvent == null || GetActiveFieldCount("F09") <= 0)
        {
            return;
        }

        Piece target = GetRandomPiece(recycleEvent.isWhitePlayer);
        if (target == null)
        {
            return;
        }

        target.AddPermanentStats(1, -1);
        Debug.Log(
            $"[CardDebug][FieldRecycle] Field=F09 暺? | " +
            $"Player={(recycleEvent.isWhitePlayer ? "White" : "Black")} | " +
            $"Recycled={recycleEvent.card.id} {recycleEvent.card.cardName} | " +
            $"Target={target.name} | Cell={target.GetCoordinates()} | " +
            "ATK+1 Value-1"
        );
    }

    private void OnCardsDrawn(CardDrawEvent drawEvent)
    {
        if (drawEvent == null || GetActiveFieldCount("F10") <= 0)
        {
            return;
        }

        CardDefinition field = GetActiveField("F10");
        List<Piece> candidates = GetAllPiecesSnapshot();
        if (field == null || candidates.Count == 0)
        {
            return;
        }

        int hitCount = Random.Range(1, Mathf.Min(6, candidates.Count) + 1);
        Debug.Log(
            $"[CardDebug][FieldDraw] Field=F10 蝵芷?銋? | " +
            $"Player={(drawEvent.isWhitePlayer ? "White" : "Black")} | " +
            $"CardsDrawn={drawEvent.cardsDrawn} | HitCount={hitCount}"
        );

        BeginDamageCalculationBatch();
        try
        {
        for (int i = 0; i < hitCount; i++)
        {
            int index = Random.Range(0, candidates.Count);
            Piece target = candidates[index];
            candidates.RemoveAt(index);
            int damage = Random.Range(1, 7);

            Debug.Log(
                $"[CardDebug][FieldDrawDamage] Field=F10 蝵芷?銋? | " +
                $"Target={target.name} | Cell={target.GetCoordinates()} | " +
                $"Damage={damage}"
            );
            DealEventDamageToPiece(field, target, damage);
        }
        }
        finally
        {
            EndDamageCalculationBatch();
        }
    }

    private void ResolveFieldTurnEndedEffects(bool endingWhiteTurn)
    {
        if (GetActiveFieldCount("F02") > 0)
        {
            CardDefinition field = GetActiveField("F02");
            foreach (Piece target in GetAllPiecesSnapshot())
            {
                DealEventDamageToPiece(field, target, 1);
            }
        }

        if (GetActiveFieldCount("F11") > 0)
        {
            CardDefinition field = GetActiveField("F11");
            Piece center = GetRandomPiece(!endingWhiteTurn);
            if (field == null || center == null)
            {
                return;
            }

            foreach (Piece target in GetPiecesInNineGrid(center))
            {
                DealEventDamageToPiece(field, target, 1);
            }
        }
    }

    private bool ResolveFieldFusion()
    {
        if (activeFieldCards.Count != 2)
        {
            return false;
        }

        string first = activeFieldCards[0] != null ? activeFieldCards[0].id : "";
        string second = activeFieldCards[1] != null ? activeFieldCards[1].id : "";
        string fusedId = GetFieldFusionId(first, second);
        if (string.IsNullOrEmpty(fusedId))
        {
            return false;
        }

        CardDefinition fused = cardHandManager != null &&
            cardHandManager.cardLibrary != null
                ? cardHandManager.cardLibrary.GetCard(fusedId)
                : null;
        if (fused == null)
        {
            Debug.LogWarning(
                $"[CardDebug][FieldFusionFailed] From={first}+{second} | " +
                $"Missing={fusedId}"
            );
            return false;
        }

        activeFieldCards.Clear();
        activeFieldCards.Add(fused);
        StartCoroutine(PlayFieldFusionRoutine(fused));
        Debug.Log(
            $"[CardDebug][FieldFusion] From={first}+{second} | " +
            $"To={fused.id} {fused.cardName} | ActiveFields=2/2"
        );
        return true;
    }

    private IEnumerator PlayFieldFusionRoutine(CardDefinition fused)
    {
        isFieldFusionPlaying = true;
        ResolveFieldCardPlaces();

        float duration = 0f;
        Animation animation = ResolveBoardAnimation();
        if (animation != null &&
            !string.IsNullOrEmpty(fieldFusionAnimationName) &&
            animation.GetClip(fieldFusionAnimationName) != null)
        {
            AnimationClip clip = animation.GetClip(fieldFusionAnimationName);
            duration = clip.length;
            animation.Play(fieldFusionAnimationName);
            Debug.Log(
                $"[CardDebug][FieldFusionAnimation] Clip=" +
                $"{fieldFusionAnimationName} | Duration={duration}"
            );
        }
        else
        {
            Debug.LogWarning(
                "[CardDebug][FieldFusionAnimationMissing] " +
                $"Clip={fieldFusionAnimationName}"
            );
        }

        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        foreach (FieldCardPlace place in fieldCardPlaces)
        {
            if (place != null)
            {
                place.SetCard(fused);
            }
        }

        isFieldFusionPlaying = false;

        if (fused != null && fused.id == "F12")
        {
            Debug.Log("[CardDebug][FieldReset] Field=F12 | Reload ChessScene");
            RequestFullGameRestart();
        }
    }

    public void PushOperationLock(string reason)
    {
        externalOperationLockCount++;
        Debug.Log(
            $"[CardDebug][OperationLock] Push={reason} | Count={externalOperationLockCount}"
        );
    }

    public void PopOperationLock(string reason)
    {
        externalOperationLockCount =
            Mathf.Max(0, externalOperationLockCount - 1);
        Debug.Log(
            $"[CardDebug][OperationLock] Pop={reason} | Count={externalOperationLockCount}"
        );
    }

    public void DeferFullGameRestartUntilUsingCard()
    {
        deferFullGameRestartUntilUsingCard = true;
    }

    public void ClearFullGameRestartDeferral()
    {
        deferFullGameRestartUntilUsingCard = false;
    }

    public void CompleteDeferredFullGameRestart()
    {
        if (!deferredFullGameRestartPending)
        {
            deferFullGameRestartUntilUsingCard = false;
            return;
        }

        deferredFullGameRestartPending = false;
        deferFullGameRestartUntilUsingCard = false;
        hideCardGameUiAfterFullRestart = true;
        if (EnsureCardHandManager())
        {
            cardHandManager.SetCardGameUiActive(false);
        }
        RequestFullGameRestart();
    }

    public void RequestFullGameRestart()
    {
        if (deferFullGameRestartUntilUsingCard)
        {
            deferredFullGameRestartPending = true;
            Debug.Log(
                "[CardDebug][FieldResetDeferred] Waiting for UsingCard animation"
            );
            return;
        }

        if (GetActiveFieldCount("F12") > 0)
        {
            hideCardGameUiAfterFullRestart = true;
            if (EnsureCardHandManager())
            {
                cardHandManager.SetCardGameUiActive(false);
            }
        }

        if (Multiplayer != null)
        {
            Multiplayer.RequestRestartGame();
            return;
        }

        SceneManager.LoadScene("ChessScene");
    }

    private void ResolveFieldCardPlaces()
    {
        if (fieldCardPlaces == null || fieldCardPlaces.Length == 0)
        {
            fieldCardPlaces = Object.FindObjectsByType<FieldCardPlace>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        }

        if (boardAnimation == null)
        {
            ResolveBoardAnimation();
        }
    }

    private Animation ResolveBoardAnimation()
    {
        if (boardAnimation != null)
        {
            return boardAnimation;
        }

        Board board = Object.FindFirstObjectByType<Board>();
        if (board != null)
        {
            boardAnimation = board.GetComponent<Animation>();
        }

        return boardAnimation;
    }

    private void ClearFieldCardPlaces()
    {
        if (fieldCardPlaces == null)
        {
            return;
        }

        foreach (FieldCardPlace place in fieldCardPlaces)
        {
            if (place != null)
            {
                place.Clear();
            }
        }
    }

    private bool IsKnownFieldPlace(FieldCardPlace place)
    {
        if (fieldCardPlaces == null)
        {
            return false;
        }

        foreach (FieldCardPlace candidate in fieldCardPlaces)
        {
            if (candidate == place)
            {
                return true;
            }
        }

        return false;
    }

    private int GetEmptyFieldPlaceCount()
    {
        int count = 0;
        if (fieldCardPlaces == null)
        {
            return count;
        }

        foreach (FieldCardPlace place in fieldCardPlaces)
        {
            if (place != null && place.ActiveCard == null)
            {
                count++;
            }
        }

        return count;
    }

    private string GetFieldFusionId(string first, string second)
    {
        if (IsFieldPair(first, second, "F01", "F01")) return "F02";
        if (IsFieldPair(first, second, "F03", "F03")) return "F04";
        if (IsFieldPair(first, second, "F05", "F05")) return "F06";
        if (IsFieldPair(first, second, "F07", "F07")) return "F08";
        if (IsFieldPair(first, second, "F09", "F09")) return "F10";
        if (IsFieldPair(first, second, "F01", "F03")) return "F11";
        if (IsFieldPair(first, second, "F05", "F07")) return "F12";
        return null;
    }

    private bool IsFieldPair(
        string first,
        string second,
        string requiredA,
        string requiredB
    )
    {
        return first == requiredA && second == requiredB ||
            first == requiredB && second == requiredA;
    }

    private int GetActiveFieldSlotCount()
    {
        int slots = 0;
        foreach (CardDefinition field in activeFieldCards)
        {
            if (field == null) continue;
            slots += GetFieldSlotCost(field);
        }

        return slots;
    }

    private int GetFieldSlotCost(CardDefinition field)
    {
        return field != null && IsAdvancedField(field.id) ? 2 : 1;
    }

    private bool IsAdvancedField(string fieldId)
    {
        switch (fieldId)
        {
            case "F02":
            case "F04":
            case "F06":
            case "F08":
            case "F10":
            case "F11":
            case "F12":
                return true;
            default:
                return false;
        }
    }

    private int GetFieldDamageDealtModifier(
        CardDefinition field,
        DamageContext damageContext
    )
    {
        if (field == null || damageContext == null)
        {
            return 0;
        }

        switch (field.id)
        {
            case "F01":
            case "F02":
                return damageContext.HasTag(DamageTag.Fire) ? 1 : 0;

            case "F03":
                return damageContext.HasTag(DamageTag.Poison) ? 1 : 0;

            case "F04":
                return damageContext.HasTag(DamageTag.Poison) ? 5 : 0;

            case "F11":
                int modifier = 0;
                if (damageContext.HasTag(DamageTag.Fire)) modifier++;
                if (damageContext.HasTag(DamageTag.Poison)) modifier++;
                return modifier;

            default:
                return 0;
        }
    }

    private CardDefinition GetActiveField(string cardId)
    {
        foreach (CardDefinition field in activeFieldCards)
        {
            if (field != null && field.id == cardId)
            {
                return field;
            }
        }

        return null;
    }

    private List<Piece> GetAllPiecesSnapshot()
    {
        UpdatePiecesOnBoard();
        List<Piece> result = new List<Piece>();
        foreach (Piece piece in piecesOnBoard.ToArray())
        {
            if (piece != null)
            {
                result.Add(piece);
            }
        }

        return result;
    }

    private List<Piece> GetPiecesInNineGrid(Piece center)
    {
        List<Piece> result = new List<Piece>();
        if (center == null)
        {
            return result;
        }

        Vector2 coordinates = center.GetCoordinates();
        int centerX = Mathf.RoundToInt(coordinates.x);
        int centerY = Mathf.RoundToInt(coordinates.y);

        for (int x = centerX - 1; x <= centerX + 1; x++)
        {
            for (int y = centerY - 1; y <= centerY + 1; y++)
            {
                if (x < 0 || x >= 8 || y < 0 || y >= 8)
                {
                    continue;
                }

                Piece target = boardMap[x, y];
                if (target != null)
                {
                    result.Add(target);
                }
            }
        }

        return result;
    }

    private void AddFieldCalculationStep(
        DamageCalculationSequence sequence,
        CardDefinition field,
        DamageCountingIcon countingIcon,
        DamageStepSide side,
        int amount,
        Vector3 worldPosition,
        Color color
    )
    {
        if (sequence == null || field == null || amount <= 0)
        {
            return;
        }

        Sprite icon = field.skillImage != null
            ? field.skillImage
            : field.cardImage;
        sequence.steps.Add(new DamageCalculationStep
        {
            icon = icon,
            usePieceIcon = false,
            countingIcon = countingIcon,
            side = side,
            displayText = amount.ToString(),
            worldPosition = worldPosition,
            color = color
        });
    }

    private Vector3 GetFieldStepPosition(DamageCalculationSequence sequence)
    {
        if (sequence != null)
        {
            if (sequence.target != null)
            {
                return sequence.target.transform.position;
            }

            if (sequence.attacker != null)
            {
                return sequence.attacker.transform.position;
            }
        }

        return Vector3.zero;
    }
}
