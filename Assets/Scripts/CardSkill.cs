using UnityEngine;

public class CardSkillContext
{
    public LogicManager logic;
    public Piece owner;
    public Piece target;
    public CardDefinition sourceCard;
    public StatusRuntime sourceStatus;
    public CardEffectTrigger trigger;
    public int amount;

    public CardSkillContext(
        LogicManager logic,
        Piece owner,
        CardDefinition sourceCard = null,
        StatusRuntime sourceStatus = null
    )
    {
        this.logic = logic;
        this.owner = owner;
        this.sourceCard = sourceCard;
        this.sourceStatus = sourceStatus;
    }
}

/// <summary>
/// 每張卡牌技能都對應一個單獨的實作。 CardDefinition 類別保存已撰寫的資料；
/// 此類別僅包含無法用數值效果表示的規則。
/// 每個卡牌定義都使用同一個共享實例。
/// </summary>
public sealed class CardSkill
{
    public static readonly CardSkill Shared = new CardSkill();
    public static CardSkill Empty { get { return Shared; } }

    private CardSkill() { }

    public void ConfigureCard(CardDefinition card)
    {
        if (card == null) return;

        card.effects.Clear();
        card.statusesToApply.Clear();
        card.animations.Clear();
        card.jobChangeDefinition = null;
        card.damageTags = DamageTag.None;

        switch (card.id)
        {
            case "J01": ConfigureJ01(card); break;
            case "J02": ConfigureJ02(card); break;
            case "J03": AddStatus(card, StatusKind.Debuff); break;
            case "J04": ConfigureJ04(card); break;
            case "J05": ConfigureJ05(card); break;
            case "J06": AddStatus(card, StatusKind.Aura); break;
            case "J07": ConfigureJ07(card); break;
            case "J08": ConfigureJ08(card); break;
            case "J09": AddStatus(card, StatusKind.Aura); break;
            case "J10": ConfigureJ10(card); break;
            case "J11": ConfigureJ11(card); break;
            case "J12": ConfigureJ12(card); break;
            case "E03": ConfigureE03(card); break;
            case "E04": ConfigureE04(card); break;
            case "E05": ConfigureE05(card); break;
            case "E06": ConfigureE06(card); break;
            case "E07": ConfigureE07(card); break;
            case "E08": ConfigureE08(card); break;
            case "E09": ConfigureE09(card); break;
            case "E10": ConfigureE10(card); break;
            case "E11": ConfigureE11(card); break;
            case "E12": ConfigureE12(card); break;
            case "F01": ConfigureFieldTag(card, DamageTag.Fire); break;
            case "F02": ConfigureFieldTag(card, DamageTag.Fire); break;
            case "F03": ConfigureFieldTag(card, DamageTag.Poison); break;
            case "F04": ConfigureFieldTag(card, DamageTag.Poison); break;
            case "F05": ConfigureFieldTag(card, DamageTag.Field); break;
            case "F06": ConfigureFieldTag(card, DamageTag.Field); break;
            case "F07": ConfigureFieldTag(card, DamageTag.Field); break;
            case "F08": ConfigureFieldTag(card, DamageTag.Field); break;
            case "F09": ConfigureFieldTag(card, DamageTag.Field); break;
            case "F10": ConfigureFieldTag(card, DamageTag.Curse); break;
            case "F11": ConfigureFieldTag(card, DamageTag.Fire | DamageTag.Poison); break;
            case "F12": ConfigureFieldTag(card, DamageTag.Field); break;
        }
    }

    private static void ConfigureFieldTag(
        CardDefinition card,
        DamageTag tag
    )
    {
        card.damageTags = tag | DamageTag.Field;
    }

    private static void ConfigureJ01(CardDefinition card)
    {
        AddStatus(card, StatusKind.Buff);
        card.jobChangeDefinition = new PieceDefinition
        {
            displayName = card.cardName,
            moveRules = new System.Collections.Generic.List<PieceMoveRule>
            {
                new PieceMoveRule
                {
                    label = "Capture Forward 2",
                    mode = PieceRuleMode.Single,
                    direction = Vector2Int.up,
                    distance = 2,
                    dependsOnFacing = true,
                    canMoveToEmpty = false,
                    canCaptureEnemy = true,
                    canJump = false
                },
                new PieceMoveRule
                {
                    label = "Move Forward 1",
                    mode = PieceRuleMode.Single,
                    direction = Vector2Int.up,
                    distance = 1,
                    dependsOnFacing = true,
                    canMoveToEmpty = true,
                    canCaptureEnemy = false,
                    canJump = false
                }
            }
        };
    }

    private static void ConfigureJ02(CardDefinition card)
    {
        StatusDefinition status = AddStatus(card, StatusKind.Buff);
        AddEffect(
            status,
            CardEffectTrigger.OnApplied,
            CardEffectType.ModifyValue,
            CardEffectTarget.OwnerPiece,
            -1
        );
    }

    private static void ConfigureJ04(CardDefinition card)
    {
        card.damageTags = DamageTag.Electric | DamageTag.Skill;
        StatusDefinition status = AddStatus(card, StatusKind.Debuff);
        AddEffect(
            status,
            CardEffectTrigger.OnOwnerDestroyed,
            CardEffectType.DamagePlayer,
            CardEffectTarget.OwnerPlayer,
            5,
            card.damageTags
        );
    }

    private static void ConfigureJ05(CardDefinition card)
    {
        StatusDefinition status = AddStatus(card, StatusKind.Buff);
        AddEffect(
            status,
            CardEffectTrigger.OnApplied,
            CardEffectType.ModifyAttack,
            CardEffectTarget.OwnerPiece,
            2
        );
        AddDamageAnimation(card, 1f);
    }

    private static void ConfigureJ07(CardDefinition card)
    {
        card.damageTags = DamageTag.Fire | DamageTag.Skill;
        StatusDefinition status = AddRechargeStatus(card, StatusKind.Buff);
        AddEffect(
            status,
            CardEffectTrigger.TurnStarted,
            CardEffectType.DamagePlayer,
            CardEffectTarget.RandomEnemyPiece,
            1,
            card.damageTags
        );
        AddDamageAnimation(card, 2f);
    }

    private static void ConfigureJ08(CardDefinition card)
    {
        StatusDefinition status = AddRechargeStatus(card, StatusKind.Buff);
        AddEffect(
            status,
            CardEffectTrigger.TurnStarted,
            CardEffectType.HealPlayer,
            CardEffectTarget.OwnerPlayer,
            1
        );
    }

    private static void ConfigureJ11(CardDefinition card)
    {
        StatusDefinition status = AddStatus(card, StatusKind.Aura);
        AddEffect(
            status,
            CardEffectTrigger.OnApplied,
            CardEffectType.ModifyFriendlyAttack,
            CardEffectTarget.OwnerPlayer,
            1
        );
    }

    private static void ConfigureJ10(CardDefinition card)
    {
        card.damageTags = DamageTag.LastWill | DamageTag.Skill;
        AddStatus(card, StatusKind.Aura);
    }

    private static void ConfigureE04(CardDefinition card)
    {
        card.damageTags = DamageTag.Fire | DamageTag.Event;
    }

    private static void ConfigureE03(CardDefinition card)
    {
        card.damageTags = DamageTag.Cost | DamageTag.Event;
    }

    private static void ConfigureJ12(CardDefinition card)
    {
        StatusDefinition status = AddStatus(card, StatusKind.Aura);
        AddEffect(
            status,
            CardEffectTrigger.OnApplied,
            CardEffectType.ModifyFriendlyValue,
            CardEffectTarget.OwnerPlayer,
            -1
        );
    }

    private static void ConfigureE12(CardDefinition card)
    {
        StatusDefinition status = AddStatus(card, StatusKind.Buff);
        status.removeOnTrigger = true;
        status.removeTrigger = CardEffectTrigger.AfterOwnerMoves;
        AddEffect(
            status,
            CardEffectTrigger.BeforeOwnerTakesDamage,
            CardEffectType.ModifyDamageTaken,
            CardEffectTarget.OwnerPiece,
            -5
        );
    }

    private static void ConfigureE06(CardDefinition card)
    {
        StatusDefinition status = AddStatus(card, StatusKind.Debuff);
        status.durationTurns = 2;
        AddEffect(
            status,
            CardEffectTrigger.OnApplied,
            CardEffectType.ModifyDamageDealt,
            CardEffectTarget.OwnerPiece,
            -1
        );
        AddEffect(
            status,
            CardEffectTrigger.BeforeOwnerTakesDamage,
            CardEffectType.ModifyDamageTaken,
            CardEffectTarget.OwnerPiece,
            1
        );
    }

    private static void ConfigureE05(CardDefinition card)
    {
        card.damageTags =
            DamageTag.Curse | DamageTag.Status | DamageTag.Event;
        StatusDefinition status = AddStatus(card, StatusKind.Debuff);
        status.durationTurns = 3;
        AddEffect(
            status,
            CardEffectTrigger.TurnEnded,
            CardEffectType.DamagePlayer,
            CardEffectTarget.OwnerPlayer,
            1,
            card.damageTags
        );
    }

    private static void ConfigureE07(CardDefinition card)
    {
        StatusDefinition status = AddStatus(card, StatusKind.Buff);
        status.durationTurns = 2;
        AddEffect(
            status,
            CardEffectTrigger.OnApplied,
            CardEffectType.ModifyDamageDealt,
            CardEffectTarget.OwnerPiece,
            1
        );
    }

    private static void ConfigureE08(CardDefinition card)
    {
        StatusDefinition status = AddStatus(card, StatusKind.Buff);
        status.durationTurns = 2;
        CardEffectData effect = AddEffect(
            status,
            CardEffectTrigger.OnApplied,
            CardEffectType.ModifyDamageDealt,
            CardEffectTarget.OwnerPiece,
            0
        );
        effect.operation = CardValueOperation.Set;
    }

    private static void ConfigureE09(CardDefinition card)
    {
        card.damageTags =
            DamageTag.Poison | DamageTag.Status | DamageTag.Event;
        StatusDefinition status = AddStatus(card, StatusKind.Debuff);
        status.durationTurns = 3;
        AddEffect(
            status,
            CardEffectTrigger.AfterOwnerMoves,
            CardEffectType.DamagePlayer,
            CardEffectTarget.OwnerPlayer,
            1,
            card.damageTags
        );
        AddEffect(
            status,
            CardEffectTrigger.TurnEnded,
            CardEffectType.DamagePlayer,
            CardEffectTarget.OwnerPlayer,
            1,
            card.damageTags
        );
    }

    private static void ConfigureE10(CardDefinition card)
    {
        card.damageTags = DamageTag.Cost | DamageTag.Event;
        StatusDefinition status = AddStatus(card, StatusKind.Buff);
        status.durationTurns = 5;
        AddEffect(
            status,
            CardEffectTrigger.TurnEnded,
            CardEffectType.HealPlayer,
            CardEffectTarget.OwnerPlayer,
            3
        );
    }

    private static void ConfigureE11(CardDefinition card)
    {
        card.damageTags =
            DamageTag.Cost | DamageTag.Status | DamageTag.Event;
        StatusDefinition status = AddStatus(card, StatusKind.Debuff);
        status.durationTurns = 10;
        AddEffect(
            status,
            CardEffectTrigger.TurnEnded,
            CardEffectType.DamagePlayer,
            CardEffectTarget.OwnerPlayer,
            5,
            card.damageTags
        );
    }

    private static StatusDefinition AddStatus(CardDefinition card, StatusKind kind)
    {
        StatusDefinition status = new StatusDefinition
        {
            statusName = card.cardName,
            sourceCardId = card.id,
            kind = kind,
            icon = card.skillImage
        };
        card.statusesToApply.Add(status);
        return status;
    }

    private static StatusDefinition AddRechargeStatus(
        CardDefinition card,
        StatusKind kind
    )
    {
        StatusDefinition status = AddStatus(card, kind);
        status.maxCharges = 1;
        status.initialCharges = 1;
        status.consumeChargeOnTrigger = true;
        status.rechargeTrigger = CardEffectTrigger.AfterOwnerMoves;
        status.rechargeAmount = 1;
        return status;
    }

    private static CardEffectData AddEffect(
        StatusDefinition status,
        CardEffectTrigger trigger,
        CardEffectType effectType,
        CardEffectTarget target,
        int value,
        DamageTag damageTags = DamageTag.None
    )
    {
        CardEffectData effect =
            new CardEffectData(trigger, effectType, target, value);
        effect.damageTags = damageTags;
        status.effects.Add(effect);
        return effect;
    }

    private static void AddDamageAnimation(CardDefinition card, float lifetime)
    {
        card.animations.Add(new CardAnimationData
        {
            timing = CardAnimationTiming.OnDamageDealt,
            recipient = CardAnimationRecipient.Target,
            effectPrefab = card.skillEffectPrefab,
            effectPositionOffset = new Vector3(0f, 0.5f, 0f),
            effectRotationEuler = new Vector3(-45f, 0f, 0f),
            effectScale = Vector3.one * 0.15f,
            effectLifetime = lifetime,
            sound = card.skillSound
        });
    }

    public void OnEquip(CardSkillContext context)
    {
        switch (GetId(context))
        {
            case "J03":
                BoardFieldCardSkillUtility.EquipAlignedRookPartner(context, "J03 護城河");
                break;

            case "J04":
                BoardFieldCardSkillUtility.EquipAlignedRookPartner(context, "J04 電網");
                break;

            case "J09":
                if (IsValid(context))
                    context.logic.RemoveKingForCard(context.owner.IsWhite, "J09 血腥瑪麗");
                break;
        }
    }

    public void OnUnequip(CardSkillContext context) { }

    public bool ResolveEvent(CardSkillContext context)
    {
        if (!IsValid(context) || context.sourceCard == null ||
            context.sourceCard.cardType != CardType.Event)
        {
            return false;
        }

        CardDefinition card = context.sourceCard;
        Piece target = context.owner;

        switch (card.id)
        {
            case "E01":
                CardDefinition equippedCard = target.cardDefinition;
                if (equippedCard == null) return false;

                context.logic.BreakBoardFieldCard(
                    target,
                    "E01 Dismissal",
                    true
                );
                if (target.cardDefinition == equippedCard)
                {
                    target.ApplyCard(null);
                }
                break;

            case "E02":
                context.logic.HealPlayerFromEvent(card, target, 5);
                break;

            case "E03":
                CardDefinition stolenCard = target.cardDefinition;
                if (stolenCard == null ||
                    !context.logic.QueueCardForPlayer(
                        context.logic.isWhiteTurn,
                        stolenCard,
                        1)) return false;

                context.logic.PayHealthCost(
                    context.logic.isWhiteTurn,
                    5,
                    card,
                    target
                );
                context.logic.BreakBoardFieldCard(
                    target,
                    "E03 Seize",
                    false
                );
                if (target.cardDefinition == stolenCard)
                {
                    target.ApplyCard(null);
                }
                break;

            case "E04":
                context.logic.DealEventDamageToPiece(card, target, 5);
                break;

            case "E05":
            case "E09":
                if (!ApplyEventStatus(
                    card,
                    target,
                    context.logic.isWhiteTurn)) return false;
                break;

            case "E10":
                if (!ApplyEventStatus(
                    card,
                    target,
                    context.logic.isWhiteTurn)) return false;
                context.logic.DealEventDamageToPiece(card, target, 10);
                break;

            case "E11":
                if (!ApplyEventStatus(
                    card,
                    target,
                    context.logic.isWhiteTurn)) return false;
                context.logic.HealPlayerFromEvent(card, target, 40);
                break;

            case "E06":
                if (!ApplyEventStatus(
                    card,
                    target,
                    context.logic.isWhiteTurn)) return false;
                break;

            case "E07":
            case "E08":
                if (ApplyEventStatusToBoard(context.logic, card) == 0)
                {
                    return false;
                }
                break;

            case "E12":
                if (!ApplyEventStatus(
                    card,
                    target,
                    context.logic.isWhiteTurn)) return false;
                break;

            default:
                return false;
        }

        Debug.Log(
            $"[CardDebug][EventCard] Card={card.id} {card.cardName} | " +
            $"Target={DescribePiece(target)} | Resolved=true"
        );
        return true;
    }

    private static bool ApplyEventStatus(
        CardDefinition card,
        Piece target,
        bool sourcePlayerIsWhite
    )
    {
        if (card == null || target == null ||
            card.statusesToApply.Count == 0)
        {
            return false;
        }

        target.RemoveStatusesByCardId(card.id);
        target.ApplyStatus(
            card.statusesToApply[0],
            null,
            false,
            true,
            sourcePlayerIsWhite
        );
        return true;
    }

    private static int ApplyEventStatusToBoard(
        LogicManager logic,
        CardDefinition card
    )
    {
        if (logic == null || card == null ||
            card.statusesToApply.Count == 0)
        {
            return 0;
        }

        logic.UpdatePiecesOnBoard();
        int appliedCount = 0;
        foreach (Piece piece in logic.piecesOnBoard.ToArray())
        {
            if (!ApplyEventStatus(card, piece, logic.isWhiteTurn)) continue;
            appliedCount++;
        }

        Debug.Log(
            $"[CardDebug][BoardEvent] Card={card.id} {card.cardName} | " +
            $"AppliedPieces={appliedCount}"
        );
        return appliedCount;
    }

    public bool CanApply(CardSkillContext context)
    {
        switch (GetId(context))
        {
            case "E01":
                return IsValid(context) && context.owner.cardDefinition != null;

            case "E03":
                return IsValid(context) &&
                    context.owner.cardDefinition != null &&
                    GetCurrentPlayerHealth(context) >= 5;

            case "E10":
                return IsValid(context) &&
                    GetOwnerPlayerHealth(context) >= 50;

            case "E11":
                return IsValid(context) &&
                    GetOwnerPlayerHealth(context) <= 50;

            case "J03":
            case "J04":
                return BoardFieldCardSkillUtility.HasAlignedRookPartner(context);

            case "J09":
                return CanApplyBloodyMary(context);

            case "J10":
                return IsValid(context) &&
                    context.logic.GetCapturedPieceCount(context.owner.IsWhite) == 0;

            default:
                return true;
        }
    }

    private int GetOwnerPlayerHealth(CardSkillContext context)
    {
        if (!IsValid(context)) return 0;
        return context.owner.IsWhite
            ? context.logic.whiteHealth
            : context.logic.blackHealth;
    }

    private int GetCurrentPlayerHealth(CardSkillContext context)
    {
        if (!IsValid(context)) return 0;
        return context.logic.isWhiteTurn
            ? context.logic.whiteHealth
            : context.logic.blackHealth;
    }

    public void TransferEventStatusesOnCapture(Piece attacker, Piece target)
    {
        if (attacker == null || target == null) return;

        StatusRuntime curse = null;
        foreach (StatusRuntime status in target.Statuses)
        {
            if (status != null && status.definition != null &&
                status.definition.sourceCardId == "E05")
            {
                curse = status;
                break;
            }
        }

        if (curse == null) return;

        StatusDefinition definition = curse.definition;
        bool hasSourcePlayer = curse.hasSourcePlayer;
        bool sourcePlayerIsWhite = curse.sourcePlayerIsWhite;

        target.RemoveStatusesByCardId("E05");
        attacker.RemoveStatusesByCardId("E05");
        attacker.ApplyStatus(
            definition,
            null,
            false,
            hasSourcePlayer,
            sourcePlayerIsWhite
        );

        Debug.Log(
            $"[CardDebug][E05Transfer] From={DescribePiece(target)} | " +
            $"To={DescribePiece(attacker)} | DurationReset=" +
            $"{definition.durationTurns}"
        );
    }

    public void OnTurnStarted(CardSkillContext context) { }
    public void OnTurnEnded(CardSkillContext context) { }
    public void OnOwnerMoved(CardSkillContext context) { }
    public void OnOwnerCastled(CardSkillContext context) { }

    public void OnOwnerCaptured(CardSkillContext context, Piece attacker)
    {
        switch (GetId(context))
        {
            case "J09":
                if (!IsValid(context)) return;
                bool attackerWins = attacker != null && attacker.IsWhite;
                context.logic.EndGameByCard(
                    attackerWins ? "White Wins" : "Black Wins",
                    "J09 血腥瑪麗被吃子"
                );
                break;

            case "J10":
                if (!IsValid(context) || attacker == null) return;
                context.logic.DealFixedDamageToPiece(
                    context.owner,
                    attacker,
                    1,
                    CardEffectTrigger.OnOwnerDestroyed,
                    DamageTag.LastWill | DamageTag.Skill
                );
                break;
        }
    }

    public void OnOwnerCaptures(CardSkillContext context, Piece capturedPiece)
    {
        if (GetId(context) != "J10" || context.owner.CardRuntime == null) return;
        context.owner.CardRuntime.skillCounterA++;
        Debug.Log(
            $"[CardDebug][J10] 聖女貞德吃子 | " +
            $"BonusStack={context.owner.CardRuntime.skillCounterA}"
        );
    }

    public void OnFriendlyPieceCaptured(
        CardSkillContext context,
        Piece capturedPiece,
        Piece attacker
    )
    {
        if (GetId(context) != "J10" || context.owner.CardRuntime == null) return;
        context.owner.CardRuntime.skillCounterB++;
        Debug.Log(
            $"[CardDebug][J10] 我方棋子被吃 | " +
            $"PenaltyStack={context.owner.CardRuntime.skillCounterB}"
        );
    }

    public void OnOwnerDealsDamage(CardSkillContext context) { }

    public int ModifyHealAmount(CardSkillContext context, int currentValue)
    {
        return GetId(context) == "J09" ? 0 : currentValue;
    }

    public int ModifyAttack(CardSkillContext context, int currentValue)
    {
        return currentValue;
    }

    public int ModifyFriendlyAttack(CardSkillContext context, int currentValue)
    {
        switch (GetId(context))
        {
            case "J09":
                return Mathf.Max(0, currentValue + 2);

            case "J10":
                if (context == null || context.owner == null ||
                    context.owner.CardRuntime == null) return currentValue;
                int bonus = 1 +
                    context.owner.CardRuntime.skillCounterA -
                    context.owner.CardRuntime.skillCounterB;
                return Mathf.Max(0, currentValue + Mathf.Max(0, bonus));

            default:
                return currentValue;
        }
    }

    public int ModifyDamageTaken(CardSkillContext context, int currentValue)
    {
        if (GetId(context) != "J06" || context == null ||
            context.owner == null || context.target == null ||
            context.owner.IsWhite != context.target.IsWhite)
        {
            return currentValue;
        }

        bool inRange = IsInNineGrid(context.owner, context.target);
        int result = inRange
            ? Mathf.Max(0, currentValue - 1)
            : currentValue;

        Debug.Log(
            $"[CardDebug][J06Range] Paladin={DescribePiece(context.owner)} | " +
            $"Target={DescribePiece(context.target)} | " +
            $"InNineGrid={inRange} | Damage={currentValue}->{result}"
        );
        return result;
    }

    public static bool IsInNineGrid(Piece center, Piece target)
    {
        if (center == null || target == null || center.IsWhite != target.IsWhite)
        {
            return false;
        }

        Vector2 centerCell = center.GetCoordinates();
        Vector2 targetCell = target.GetCoordinates();
        int deltaX = Mathf.Abs(
            Mathf.RoundToInt(centerCell.x) - Mathf.RoundToInt(targetCell.x)
        );
        int deltaY = Mathf.Abs(
            Mathf.RoundToInt(centerCell.y) - Mathf.RoundToInt(targetCell.y)
        );
        return deltaX <= 1 && deltaY <= 1;
    }

    public int ModifyOwnerPlayerDamage(CardSkillContext context, int currentValue)
    {
        return currentValue;
    }

    public PieceDefinition GetMoveDefinition(CardSkillContext context)
    {
        return GetId(context) == "J01" && context.sourceCard != null
            ? context.sourceCard.jobChangeDefinition
            : null;
    }

    private bool CanApplyBloodyMary(CardSkillContext context)
    {
        if (!IsValid(context)) return false;
        bool fullHealth = context.owner.IsWhite
            ? context.logic.whiteHealth >= LogicManager.MaxHealth
            : context.logic.blackHealth >= LogicManager.MaxHealth;
        bool noFriendlyCaptured =
            context.logic.GetCapturedPieceCount(context.owner.IsWhite) == 0;
        return fullHealth && noFriendlyCaptured;
    }

    private bool IsValid(CardSkillContext context)
    {
        return context != null && context.logic != null && context.owner != null;
    }

    private string GetId(CardSkillContext context)
    {
        return context != null && context.sourceCard != null
            ? context.sourceCard.id
            : string.Empty;
    }

    private static string DescribePiece(Piece piece)
    {
        if (piece == null) return "None";
        Vector2 cell = piece.GetCoordinates();
        return $"{(piece.IsWhite ? "White" : "Black")} " +
            $"{piece.GetType().Name} ({cell.x:0},{cell.y:0})";
    }
}

public static class BoardFieldCardSkillUtility
{
    public static bool HasAlignedRookPartner(CardSkillContext context)
    {
        return FindAlignedRookPartner(context) != null;
    }

    public static void EquipAlignedRookPartner(CardSkillContext context, string logName)
    {
        if (!IsValid(context)) return;
        Piece partner = FindAlignedRookPartner(context);
        if (partner == null) return;

        if (partner.cardDefinition != context.sourceCard)
        {
            Debug.Log(
                $"[CardDebug][FieldAutoEquip] Card={logName} | " +
                $"Owner={Describe(context.owner)} | Partner={Describe(partner)}"
            );
            partner.ApplyCard(context.sourceCard);
        }

        context.logic.RefreshBoardFieldEffects();
    }

    private static Piece FindAlignedRookPartner(CardSkillContext context)
    {
        if (!IsValid(context)) return null;

        Piece bestPartner = null;
        int bestDistance = int.MaxValue;
        Vector2 ownerPosition = context.owner.GetCoordinates();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Piece candidate = context.logic.boardMap[x, y];
                if (candidate == null || candidate == context.owner ||
                    !(candidate is Rook) ||
                    candidate.IsWhite != context.owner.IsWhite) continue;

                Vector2 candidatePosition = candidate.GetCoordinates();
                bool aligned =
                    Mathf.RoundToInt(ownerPosition.x) == Mathf.RoundToInt(candidatePosition.x) ||
                    Mathf.RoundToInt(ownerPosition.y) == Mathf.RoundToInt(candidatePosition.y);
                if (!aligned) continue;

                int distance =
                    Mathf.Abs(Mathf.RoundToInt(ownerPosition.x - candidatePosition.x)) +
                    Mathf.Abs(Mathf.RoundToInt(ownerPosition.y - candidatePosition.y));
                if (distance <= 1 ||
                    !IsPathClear(context.logic, ownerPosition, candidatePosition)) continue;

                bool paired = candidate.cardDefinition == context.sourceCard;
                bool bestPaired = bestPartner != null &&
                    bestPartner.cardDefinition == context.sourceCard;
                if (bestPartner == null || (paired && !bestPaired) ||
                    (paired == bestPaired && distance < bestDistance))
                {
                    bestPartner = candidate;
                    bestDistance = distance;
                }
            }
        }

        return bestPartner;
    }

    private static bool IsPathClear(LogicManager logic, Vector2 start, Vector2 end)
    {
        Vector2Int startCell = Vector2Int.RoundToInt(start);
        Vector2Int endCell = Vector2Int.RoundToInt(end);
        Vector2Int direction = new Vector2Int(
            startCell.x == endCell.x ? 0 : startCell.x < endCell.x ? 1 : -1,
            startCell.y == endCell.y ? 0 : startCell.y < endCell.y ? 1 : -1
        );
        Vector2Int cursor = startCell + direction;
        while (cursor != endCell)
        {
            if (logic.boardMap[cursor.x, cursor.y] != null) return false;
            cursor += direction;
        }
        return true;
    }

    private static bool IsValid(CardSkillContext context)
    {
        return context != null && context.logic != null &&
            context.owner is Rook && context.sourceCard != null;
    }

    private static string Describe(Piece piece)
    {
        if (piece == null) return "None";
        Vector2 position = piece.GetCoordinates();
        return $"{(piece.IsWhite ? "White" : "Black")} " +
            $"{(string.IsNullOrEmpty(piece.PieceType) ? piece.GetType().Name : piece.PieceType)} " +
            $"({position.x:0},{position.y:0})";
    }
}
