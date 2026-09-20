using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 棋子的共用座標、移動、卡牌裝備與狀態行為。
/// </summary>
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

    /// <summary>
    /// 將永久攻擊或價值修正累加到棋子。
    /// </summary>
    public void AddPermanentStats(int attackDelta, int valueDelta)
    {
        if (logicManager != null && logicManager.IsClassicChess) return;
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

    protected LogicManager logicManager;

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

    /// <summary>
    /// 將卡牌裝備到棋子，更新執行期資料並處理裝卸效果。
    /// </summary>
    public void ApplyCard(CardDefinition card)
    {
        if (logicManager != null && logicManager.IsClassicChess) return;
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

    /// <summary>
    /// 將狀態定義轉成棋子持有的執行期狀態。
    /// </summary>
    public void ApplyStatus(
        StatusDefinition statusDefinition,
        Piece source,
        bool fromCard,
        bool hasSourcePlayer = false,
        bool sourcePlayerIsWhite = false
    )
    {
        if (logicManager != null && logicManager.IsClassicChess) return;
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

    /// <summary>
    /// 移除棋子上與卡牌裝備相關的狀態。
    /// </summary>
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

    /// <summary>
    /// 移除指定來源卡號建立的狀態。
    /// </summary>
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

    /// <summary>
    /// 依指定觸發時機移除到期或應解除的狀態。
    /// </summary>
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
    /// 推進狀態持續回合數並移除到期項目。
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

    /// <summary>
    /// 產生狀態剩餘次數的紀錄文字。
    /// </summary>
    private string DescribeStatusCharges(StatusRuntime status)
    {
        if (status == null || !status.UsesCharges)
        {
            return "Unlimited";
        }

        return $"{status.charges}/{status.definition.maxCharges}";
    }

    /// <summary>
    /// 產生目前棋子的名稱、陣營與位置描述。
    /// </summary>
    private string DescribeSelf()
    {
        string side = IsWhite ? "White" : "Black";
        string type = string.IsNullOrEmpty(PieceType)
            ? GetType().Name
            : PieceType;
        Vector2 coordinates = GetCoordinates();

        return $"{side} {type} ({coordinates.x:0},{coordinates.y:0})";
    }

    /// <summary>
    /// 依加算、指定或乘算方式套用數值效果。
    /// </summary>
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

    public bool IsWhite { get; private set; }

    public string PieceType { get; private set; }

    public int HasMoved { get; private set; }

    protected bool UsesDefinitionRules
    {
        get { return (logicManager == null || !logicManager.IsClassicChess) && ActiveDefinition != null; }
    }

    /// <summary>
    /// 移除轉職、狀態與永久加成，不觸發任何卸裝技能；保留棋子陣營及移動紀錄。
    /// </summary>
    public void ClearRpgState()
    {
        cardDefinition = null;
        cardRuntime = null;
        statuses.Clear();
        attack = 0;
        extraAttack = 0;
        extraValue = 0;
        value = GetDefaultValue();
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

    /// <summary>
    /// 列出棋子攻擊的格子，供將軍與王車易位判定使用；攻擊線保留第一個阻擋格。
    /// </summary>
    public abstract List<Vector2> GetAttackedFields();

    /// <summary>
    /// 依棋子的基本走法列出候選目的地；王受將軍的限制由合法走法檢查處理。
    /// </summary>
    protected abstract List<Vector2> GetPotentialMoves();

    /// <summary>
    /// 篩選棋子的合法走法，排除移動後己方國王仍受攻擊的位置。
    /// </summary>
    public virtual List<Vector2> GetLegalMoves()
    {

        if (UsesDefinitionRules)
        {
            return logicManager.ApplyBoardFieldEffects(
                this,
                GetDefinitionLegalMoves()
            );
        }

        List<Vector2> legalMoves =
            new List<Vector2>();

        bool isKingInCheck =
            logicManager.CheckKingStatus();

        foreach (Vector2 move in GetPotentialMoves())
        {

            if (WillMoveEndCheck(move))
            {
                legalMoves.Add(move);
            }
        }

        // Debug.Log($"Legal moves for {PieceType}: {legalMoves.Count}");

        return logicManager.ApplyBoardFieldEffects(this, legalMoves);
    }

    /// <summary>
    /// 依轉職或自訂移動定義取得攻擊格。
    /// </summary>
    protected List<Vector2> GetDefinitionAttackedFields()
    {
        if (!UsesDefinitionRules)
        {
            return new List<Vector2>();
        }

        return GetDefinitionFields(includeEmpty: true);
    }

    /// <summary>
    /// 依自訂移動定義取得候選格並檢查國王安全。
    /// </summary>
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

    /// <summary>
    /// 依單步或射線移動規則建立指定用途的格子清單。
    /// </summary>
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
    /// <summary>
    /// 依自訂走法的跳躍與路徑設定檢查阻擋。
    /// </summary>
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

    /// <summary>
    /// 依棋子朝向解析自訂移動規則的方向。
    /// </summary>
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
    /// <summary>
    /// 暫時模擬移動並檢查己方國王安全，完成後還原棋盤。
    /// </summary>
    protected bool WillMoveEndCheck(Vector2 move)
    {
        Piece originalPiece =
            logicManager.boardMap[
                (int)move.x,
                (int)move.y
            ];

        Vector2 originalPosition =
            GetCoordinates();

        logicManager.boardMap[
            (int)originalPosition.x,
            (int)originalPosition.y
        ] = null;

        logicManager.boardMap[
            (int)move.x,
            (int)move.y
        ] = this;

        logicManager.UpdateCheckMap();

        bool isKingInCheck =
            logicManager.CheckKingStatus();

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

        logicManager.boardMap[
            (int)originalPosition.x,
            (int)originalPosition.y
        ] = this;

        logicManager.boardMap[
            (int)move.x,
            (int)move.y
        ] = originalPiece;

        logicManager.UpdateCheckMap();

        return !isKingInCheck;
    }

    /// <summary>
    /// 設定棋子種類、陣營與初始屬性，並重設移動紀錄。
    /// </summary>
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

        HasMoved = 0;
        if (logicManager != null && logicManager.IsClassicChess) ClearRpgState();
    }

    /// <summary>
    /// 依棋子種類取得原始價值。
    /// </summary>
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

    /// <summary>
    /// 取得對局控制器、套用預設裝備並登錄棋盤位置。
    /// </summary>
    private void Start()
    {

        logicManager =
            Object.FindFirstObjectByType<LogicManager>();

        if (logicManager != null && logicManager.IsClassicChess)
        {
            ClearRpgState();
        }
        else if (cardDefinition != null && cardRuntime == null)
        {
            ApplyCard(cardDefinition);
        }

        UpdateBoardMap();
    }

    /// <summary>
    /// 取得棋子的目前棋盤座標。
    /// </summary>
    public Vector2 GetCoordinates()
    {
        return new Vector2(
            transform.position.x,
            transform.position.z
        );
    }

    /// <summary>
    /// 將棋子目前位置登錄到棋盤索引。
    /// </summary>
    public void UpdateBoardMap()
    {
        Vector2 coordinates =
            GetCoordinates();

        logicManager.boardMap[
            (int)coordinates.x,
            (int)coordinates.y
        ] = this;
    }

    /// <summary>
    /// 更新棋盤位置、處理吃子與移動演出，再通知對局系統觸發移動效果。
    /// </summary>
    public virtual void Move(Vector2 newPosition)
    {

        Vector2 currentCoordinates =
            GetCoordinates();
        Vector3 startWorldPosition = transform.position;
        Vector3 endWorldPosition =
            new Vector3(
                newPosition.x,
                transform.position.y,
                newPosition.y
            );

        logicManager.boardMap[
            (int)currentCoordinates.x,
            (int)currentCoordinates.y
        ] = null;

        HasMoved = 1;

        Take(newPosition);

        transform.position = endWorldPosition;

        PlayMoveAnimation(startWorldPosition, endWorldPosition);

        UpdateBoardMap();

        logicManager.OnPieceMoved(this);
    }

    /// <summary>
    /// 處理目的格上的吃子事件、清空該格索引，並回傳被吃的棋子。
    /// </summary>
    public Piece Take(Vector2 targetPosition)
    {

        Piece targetPiece =
            logicManager.boardMap[
                (int)targetPosition.x,
                (int)targetPosition.y
            ];

        if (targetPiece != null)
        {
            logicManager.OnPieceCaptured(this, targetPiece);
        }

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

    /// <summary>
    /// 播放棋子被吃的動畫，並處理後續移除。
    /// </summary>
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

    /// <summary>
    /// 播放棋子的移動動畫。
    /// </summary>
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

    /// <summary>
    /// 播放棋子吃子的動畫。
    /// </summary>
    public void PlayCaptureAnimation()
    {
        PieceAnimationPlayer animationPlayer =
            GetComponent<PieceAnimationPlayer>();

        if (animationPlayer != null)
        {
            animationPlayer.PlayCapture();
        }
    }

    /// <summary>
    /// 判斷座標是否位於既有的 8×8 棋盤範圍。
    /// </summary>
    public bool IsPositionWithinBoard(Vector2 position)
    {
        return
            position.x >= 0 &&
            position.x < 8 &&
            position.y >= 0 &&
            position.y < 8;
    }

    /// <summary>
    /// 依傳入方向順序收集射線格子，遇到第一枚棋子即停止；攻擊圖可包含友方阻擋格。
    /// </summary>
    protected List<Vector2> CollectRayFields(
        int[] directionsX,
        int[] directionsY,
        bool includeFriendlyBlocker
    )
    {
        List<Vector2> fields = new List<Vector2>();
        Vector2 currentCoordinates = GetCoordinates();

        for (int i = 0; i < directionsX.Length; i++)
        {
            for (int step = 1; ; step++)
            {
                Vector2 position = new Vector2(
                    currentCoordinates.x + step * directionsX[i],
                    currentCoordinates.y + step * directionsY[i]
                );
                if (!IsPositionWithinBoard(position))
                {
                    break;
                }

                Piece occupant = logicManager.boardMap[(int)position.x, (int)position.y];
                // 攻擊圖包含第一個友方阻擋格；移動候選格則排除友方棋子。
                if (includeFriendlyBlocker || occupant == null || occupant.IsWhite != IsWhite)
                {
                    fields.Add(position);
                }

                if (occupant != null)
                {
                    break;
                }
            }
        }

        return fields;
    }
}
