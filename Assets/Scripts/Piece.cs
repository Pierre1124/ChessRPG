using System.Collections.Generic;
using UnityEngine;

//==============================
// ??????????????皜∪??
// Pawn??抬?牽k??抬?赯ht??拆??shop??抬?een??抬?謈經
// ????雓制??????????
//==============================
public abstract class Piece : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField] private int attack;
    [SerializeField] private int value;
    [SerializeField] private int extraAttack;
    [SerializeField] private int extraValue;

    public int Attack
    {
        get { return Mathf.Max(0, attack + extraAttack); }
    }

    public int Value
    {
        get { return Mathf.Max(0, value + extraValue); }
    }

    public void AddPermanentStats(int attackDelta, int valueDelta)
    {
        int previousAttack = Attack;
        int previousValue = Value;

        extraAttack += attackDelta;
        extraValue += valueDelta;

        Debug.Log(
            $"[CardDebug][PermanentStats] Owner={DescribeSelf()} | " +
            $"ATK={previousAttack}->{Attack} | " +
            $"Value={previousValue}->{Value}"
        );
    }

    //==============================
    // ??頩???????鞊????
    //==============================
    protected LogicManager logicManager;

    //==============================
    // ???????????
    // ???潸葭????豱???????????????雓?? / ???????
    //==============================
    [System.NonSerialized]
    public CardDefinition cardDefinition;

    [System.NonSerialized]
    private CardRuntimeState cardRuntime;

    [System.NonSerialized]
    private readonly List<StatusRuntime> statuses =
        new List<StatusRuntime>();

    public CardRuntimeState CardRuntime
    {
        get { return cardRuntime; }
    }

    public IReadOnlyList<StatusRuntime> Statuses
    {
        get { return statuses; }
    }

    public void ApplyCard(CardDefinition card)
    {
        CardDefinition previousCard = cardDefinition;
        if (previousCard != null)
        {
            previousCard.Api.skill.OnUnequip(
                new CardSkillContext(logicManager, this, previousCard)
            );
        }

        RemoveCardStatuses();

        cardDefinition = card;
        cardRuntime = card != null
            ? new CardRuntimeState(card)
            : null;

        attack = 0;

        if (card != null)
        {
            card.Api.skill.OnEquip(
                new CardSkillContext(logicManager, this, card)
            );

            foreach (CardEffectData effect in card.effects)
            {
                if (
                    effect == null ||
                    effect.effectType != CardEffectType.ModifyAttack
                )
                {
                    continue;
                }

                attack = ApplyValueOperation(
                    attack,
                    effect.operation,
                    effect.value
                );
            }

            foreach (StatusDefinition status in card.statusesToApply)
            {
                ApplyStatus(status, this, true);
            }
        }

        Debug.Log(
            $"裝備卡片" +
            $"{(IsWhite ? "白方" : "黑方")} " +
            $"{(string.IsNullOrEmpty(PieceType) ? GetType().Name : PieceType)} " +
            $"座標 {GetCoordinates()} | " +
            $"卡片={(card != null ? card.cardName : "None")} | " +
            $"ATK={attack} | Value={value}"
        );

        CardAnimationEvents.Play(
            this,
            this,
            card,
            CardAnimationTiming.OnCardApplied
        );

        if (logicManager != null)
        {
            logicManager.RefreshBoardFieldEffects();
        }
    }

    public void ApplyStatus(
        StatusDefinition statusDefinition,
        Piece source,
        bool fromCard,
        bool hasSourcePlayer = false,
        bool sourcePlayerIsWhite = false
    )
    {
        if (statusDefinition == null)
        {
            return;
        }

        StatusRuntime status = new StatusRuntime(
            statusDefinition,
            this,
            source,
            fromCard
        );
        status.skipNextDurationAdvance =
            statusDefinition.durationTurns > 0 &&
            !statusDefinition.HasTrigger(CardEffectTrigger.TurnEnded) &&
            logicManager != null &&
            logicManager.isWhiteTurn == IsWhite;
        status.hasSourcePlayer = hasSourcePlayer;
        status.sourcePlayerIsWhite = sourcePlayerIsWhite;

        statuses.Add(status);

        Debug.Log(
            $"狀態更新 持有者={DescribeSelf()} | " +
            $"狀態={statusDefinition.statusName} | " +
            $"狀態種類={statusDefinition.kind} | " +
            $"充能數量={DescribeStatusCharges(status)} | " +
            $"來自卡片={fromCard}"
        );
    }

    private void RemoveCardStatuses()
    {
        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            if (statuses[i].fromCard)
            {
                Debug.Log(
                    $"[CardDebug][StatusRemoved] Owner={DescribeSelf()} | " +
                    $"Status={statuses[i].definition.statusName} | " +
                    "Reason=Card replaced"
                );

                statuses.RemoveAt(i);
            }
        }
    }

    public int RemoveStatusesByCardId(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return 0;

        int removed = 0;
        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            StatusRuntime status = statuses[i];
            if (status == null || status.definition == null ||
                status.definition.sourceCardId != cardId)
            {
                continue;
            }

            Debug.Log(
                $"[CardDebug][StatusRemoved] Owner={DescribeSelf()} | " +
                $"Status={status.definition.statusName} | Reason=Replaced"
            );
            statuses.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    public int RemoveStatusesOnTrigger(CardEffectTrigger trigger)
    {
        int removed = 0;
        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            StatusRuntime status = statuses[i];
            if (status == null || status.definition == null ||
                !status.definition.removeOnTrigger ||
                status.definition.removeTrigger != trigger)
            {
                continue;
            }

            Debug.Log(
                $"[CardDebug][StatusRemoved] Owner={DescribeSelf()} | " +
                $"Status={status.definition.statusName} | Trigger={trigger}"
            );
            statuses.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    /// <summary>
    /// Advances finite statuses by one completed owner turn.
    /// A duration of zero means the status does not expire by time.
    /// </summary>
    public int AdvanceStatusDurations()
    {
        int removed = 0;

        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            StatusRuntime status = statuses[i];
            if (
                status == null ||
                status.definition == null ||
                status.definition.durationTurns <= 0
            )
            {
                continue;
            }

            if (status.skipNextDurationAdvance)
            {
                status.skipNextDurationAdvance = false;
                Debug.Log(
                    $"[CardDebug][StatusDurationSkipped] " +
                    $"Owner={DescribeSelf()} | " +
                    $"Status={status.definition.statusName} | " +
                    "Reason=AppliedDuringCurrentOwnerTurn"
                );
                continue;
            }

            int previousTurns = status.remainingTurns;
            status.remainingTurns = Mathf.Max(0, previousTurns - 1);

            Debug.Log(
                $"[CardDebug][StatusDuration] Owner={DescribeSelf()} | " +
                $"Status={status.definition.statusName} | " +
                $"Turns={previousTurns}->{status.remainingTurns}"
            );

            if (status.remainingTurns > 0)
            {
                continue;
            }

            Debug.Log(
                $"[CardDebug][StatusRemoved] Owner={DescribeSelf()} | " +
                $"Status={status.definition.statusName} | Reason=DurationExpired"
            );
            statuses.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    private string DescribeStatusCharges(StatusRuntime status)
    {
        if (status == null || !status.UsesCharges)
        {
            return "Unlimited";
        }

        return $"{status.charges}/{status.definition.maxCharges}";
    }

    private string DescribeSelf()
    {
        string side = IsWhite ? "White" : "Black";
        string type = string.IsNullOrEmpty(PieceType)
            ? GetType().Name
            : PieceType;
        Vector2 coordinates = GetCoordinates();

        return $"{side} {type} ({coordinates.x:0},{coordinates.y:0})";
    }

    private int ApplyValueOperation(
        int currentValue,
        CardValueOperation operation,
        int modifier
    )
    {
        switch (operation)
        {
            case CardValueOperation.Set:
                return Mathf.Max(0, modifier);

            case CardValueOperation.Multiply:
                return Mathf.Max(0, currentValue * modifier);

            default:
                return Mathf.Max(0, currentValue + modifier);
        }
    }

    //==============================
    // ??????豯殷???
    // true = ???
    // false = ??秋偃豱?
    //==============================
    public bool IsWhite { get; private set; }

    //==============================
    // ??????皜∪???雓?
    // ?雓????
    // Pawn??抬?牽k??抬?赯ht...
    //==============================
    public string PieceType { get; private set; }

    //==============================
    // ?????雓????
    // 0 = ??????
    // 1 = ??頦????
    // ??????
    // ????????拆??????
    //==============================
    public int HasMoved { get; private set; }

    protected bool UsesDefinitionRules
    {
        get { return ActiveDefinition != null; }
    }

    private PieceDefinition ActiveDefinition
    {
        get
        {
            if (cardDefinition == null)
            {
                return null;
            }

            CardDefinition cardApi = cardDefinition.Api;
            if (cardApi != null && cardApi.skill != null)
            {
                PieceDefinition skillDefinition =
                    cardApi.skill.GetMoveDefinition(
                        new CardSkillContext(
                            logicManager,
                            this,
                            cardDefinition
                        )
                    );

                if (skillDefinition != null)
                {
                    return skillDefinition;
                }
            }

            return cardDefinition.jobChangeDefinition;
        }
    }

    //==============================
    // ??????????
    // ?????頩??????
    //==============================
    public abstract List<Vector2> GetAttackedFields();

    //==============================
    // ???????????????
    // ?鞊???????
    //==============================
    protected abstract List<Vector2> GetPotentialMoves();

    //==============================
    // ???????雓??
    // ???????
    // ? ?鞊?????箇????????瘞???
    //==============================
    public virtual List<Vector2> GetLegalMoves()
    {
        
        if (UsesDefinitionRules)
        {
            return logicManager.ApplyBoardFieldEffects(
                this,
                GetDefinitionLegalMoves()
            );
        }

// ????雓????憌??
        List<Vector2> legalMoves =
            new List<Vector2>();

        // ???????怏???瘞???
        bool isKingInCheck =
            logicManager.CheckKingStatus();

        //==============================
        // ??朵???????????????
        //==============================
        foreach (Vector2 move in GetPotentialMoves())
        {
            // ?????雓????祈??????啣???頩???
            if (WillMoveEndCheck(move))
            {
                legalMoves.Add(move);
            }
        }

        // Debug??
        // Debug.Log($"Legal moves for {PieceType}: {legalMoves.Count}");

        return logicManager.ApplyBoardFieldEffects(this, legalMoves);
    }

    //==============================
    // ??蝛????????
    // ?????????????頩???頩???
    //==============================
    protected List<Vector2> GetDefinitionAttackedFields()
    {
        if (!UsesDefinitionRules)
        {
            return new List<Vector2>();
        }

        return GetDefinitionFields(includeEmpty: true);
    }

    private List<Vector2> GetDefinitionLegalMoves()
    {
        List<Vector2> legalMoves =
            new List<Vector2>();

        foreach (Vector2 move in GetDefinitionFields(includeEmpty: false))
        {
            if (WillMoveEndCheck(move))
            {
                legalMoves.Add(move);
            }
        }

        return legalMoves;
    }

    private List<Vector2> GetDefinitionFields(bool includeEmpty)
    {
        List<Vector2> fields =
            new List<Vector2>();

        PieceDefinition activeDefinition = ActiveDefinition;

        if (activeDefinition == null)
        {
            return fields;
        }

        Vector2 currentCoordinates =
            GetCoordinates();

        foreach (PieceMoveRule rule in activeDefinition.moveRules)
        {
            if (rule == null)
            {
                continue;
            }

            Vector2Int direction =
                ResolveRuleDirection(rule);

            int maxDistance =
                Mathf.Max(1, rule.distance);

            int steps =
                rule.mode == PieceRuleMode.Ray
                ? maxDistance
                : 1;

            for (int step = 1; step <= steps; step++)
            {
                int distance =
                    rule.mode == PieceRuleMode.Ray
                    ? step
                    : maxDistance;

                Vector2 targetPosition =
                    new Vector2(
                        currentCoordinates.x +
                        direction.x * distance,
                        currentCoordinates.y +
                        direction.y * distance
                    );

                if (!IsPositionWithinBoard(targetPosition))
                {
                    break;
                }

                if (
                    !rule.canJump &&
                    IsDefinitionPathBlocked(
                        currentCoordinates,
                        direction,
                        distance
                    )
                )
                {
                    break;
                }

                Piece targetPiece =
                    logicManager.boardMap[
                        (int)targetPosition.x,
                        (int)targetPosition.y
                    ];

                if (includeEmpty)
                {
                    fields.Add(targetPosition);

                    if (
                        targetPiece != null &&
                        !rule.canJump
                    )
                    {
                        break;
                    }

                    continue;
                }

                if (targetPiece == null)
                {
                    if (rule.canMoveToEmpty)
                    {
                        fields.Add(targetPosition);
                    }
                }
                else
                {
                    if (
                        targetPiece.IsWhite != IsWhite &&
                        rule.canCaptureEnemy
                    )
                    {
                        fields.Add(targetPosition);
                    }

                    if (!rule.canJump)
                    {
                        break;
                    }
                }
            }
        }

        return fields;
    }
    private bool IsDefinitionPathBlocked(
        Vector2 start,
        Vector2Int direction,
        int distance
    )
    {
        for (int step = 1; step < distance; step++)
        {
            Vector2 position =
                new Vector2(
                    start.x + direction.x * step,
                    start.y + direction.y * step
                );

            if (
                IsPositionWithinBoard(position) &&
                logicManager.boardMap[
                    (int)position.x,
                    (int)position.y
                ] != null
            )
            {
                return true;
            }
        }

        return false;
    }

    private Vector2Int ResolveRuleDirection(
        PieceMoveRule rule
    )
    {
        if (
            rule.dependsOnFacing &&
            !IsWhite
        )
        {
            return new Vector2Int(
                rule.direction.x,
                -rule.direction.y
            );
        }

        return rule.direction;
    }
    protected bool WillMoveEndCheck(Vector2 move)
    {
        //==============================
        // ??????蝮?????
        //==============================
        Piece originalPiece =
            logicManager.boardMap[
                (int)move.x,
                (int)move.y
            ];

        // ???蝮??????
        Vector2 originalPosition =
            GetCoordinates();

        //==============================
        // ??蝛??????
        //==============================
        logicManager.boardMap[
            (int)originalPosition.x,
            (int)originalPosition.y
        ] = null;

        logicManager.boardMap[
            (int)move.x,
            (int)move.y
        ] = this;

        // ??豲????????
        logicManager.UpdateCheckMap();

        // ??????瘞???
        bool isKingInCheck =
            logicManager.CheckKingStatus();

        //==============================
        // ?????雓?????? King
        // ????????????????????????瘞????
        //==============================
        if (this is King)
        {
            isKingInCheck =
                IsWhite
                ?
                logicManager.blackCheckMap[
                    (int)move.x,
                    (int)move.y
                ]
                :
                logicManager.whiteCheckMap[
                    (int)move.x,
                    (int)move.y
                ];
        }

        //==============================
        // ?????蝛???
        //==============================
        logicManager.boardMap[
            (int)originalPosition.x,
            (int)originalPosition.y
        ] = this;

        logicManager.boardMap[
            (int)move.x,
            (int)move.y
        ] = originalPiece;

        // ??豲????????
        logicManager.UpdateCheckMap();

        //==============================
        // true = ???
        // false = ???啣???頩???
        //==============================
        return !isKingInCheck;
    }

    //==============================
    // ????????
    //==============================
    public void Initialize(
        string pieceType,
        bool isWhite
    )
    {
        if (logicManager == null)
        {
            logicManager =
                Object.FindFirstObjectByType<LogicManager>();
        }

        PieceType = pieceType;

        IsWhite = isWhite;

        attack = 0;
        value = GetDefaultValue();

        // ?鞊啣???????????
        HasMoved = 0;
    }

    private int GetDefaultValue()
    {
        if (this is Queen)
        {
            return 9;
        }

        if (this is Rook)
        {
            return 5;
        }

        if (this is Knight || this is Bishop)
        {
            return 3;
        }

        if (this is Pawn)
        {
            return 1;
        }

        return 0;
    }

    //==============================
    // ?????????
    //==============================
    private void Start()
    {
        // ?頩???LogicManager
        logicManager =
            Object.FindFirstObjectByType<LogicManager>();

        if (cardDefinition != null && cardRuntime == null)
        {
            ApplyCard(cardDefinition);
        }

        // ??豲???????謑???
        UpdateBoardMap();
    }

    //==============================
    // ?????????頦?
    //==============================
    public Vector2 GetCoordinates()
    {
        return new Vector2(
            transform.position.x,
            transform.position.z
        );
    }

    //==============================
    // ??豲???????謑???
    // boardMap[x,y] = ????
    //==============================
    public void UpdateBoardMap()
    {
        Vector2 coordinates =
            GetCoordinates();

        logicManager.boardMap[
            (int)coordinates.x,
            (int)coordinates.y
        ] = this;
    }

    //==============================
    // ?雓??????
    //==============================
    public virtual void Move(Vector2 newPosition)
    {
        // ???蝮??????
        Vector2 currentCoordinates =
            GetCoordinates();
        Vector3 startWorldPosition = transform.position;
        Vector3 endWorldPosition =
            new Vector3(
                newPosition.x,
                transform.position.y,
                newPosition.y
            );

        // ?雓????????
        logicManager.boardMap[
            (int)currentCoordinates.x,
            (int)currentCoordinates.y
        ] = null;

        // ??橘擐???頦????
        HasMoved = 1;

        // ???
        Take(newPosition);

        // ??豲???雓雓??????
        transform.position = endWorldPosition;

        PlayMoveAnimation(startWorldPosition, endWorldPosition);

        // ??豲???????謑???
        UpdateBoardMap();

        logicManager.OnPieceMoved(this);
    }

    //==============================
    // ???
    //==============================
    public Piece Take(Vector2 targetPosition)
    {
        // ??????????
        Piece targetPiece =
            logicManager.boardMap[
                (int)targetPosition.x,
                (int)targetPosition.y
            ];

        // ?????????
        if (targetPiece != null)
        {
            logicManager.OnPieceCaptured(this, targetPiece);
        }

        // ?雓???蝬??鞈?????
        logicManager.boardMap[
            (int)targetPosition.x,
            (int)targetPosition.y
        ] = null;

        if (targetPiece != null)
        {
            logicManager.OnPieceRemovedFromBoard(targetPiece);
        }

        return targetPiece;
    }

    public void PlayCapturedAnimation()
    {
        PieceAnimationPlayer animationPlayer =
            GetComponent<PieceAnimationPlayer>();

        if (animationPlayer != null)
        {
            animationPlayer.PlayCapturedAndDestroy();
            return;
        }

        Destroy(gameObject);
    }

    private void PlayMoveAnimation(Vector3 from, Vector3 to)
    {
        PieceAnimationPlayer animationPlayer =
            GetComponent<PieceAnimationPlayer>();

        if (animationPlayer != null)
        {
            animationPlayer.PlayMove(from, to);
            return;
        }

        // 沒有 PieceAnimationPlayer 時不播放程式碼動畫。
        // 移動/吃子動畫一律交給 Unity Animation / Animator。
    }

    public void PlayCaptureAnimation()
    {
        PieceAnimationPlayer animationPlayer =
            GetComponent<PieceAnimationPlayer>();

        if (animationPlayer != null)
        {
            animationPlayer.PlayCapture();
        }
    }

    //==============================
    // ????????????
    //==============================
    public bool IsPositionWithinBoard(Vector2 position)
    {
        return
            position.x >= 0 &&
            position.x < 8 &&
            position.y >= 0 &&
            position.y < 8;
    }
}

