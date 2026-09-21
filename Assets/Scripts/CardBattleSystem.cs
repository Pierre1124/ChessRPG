using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 統一處理卡牌觸發、狀態修正與傷害計算，並建立結算顯示資料。
/// </summary>
public class CardBattleSystem
{
    private readonly LogicManager logic;

    /// <summary>
    /// 保存對局邏輯引用，供卡牌效果與傷害計算使用。
    /// </summary>
    public CardBattleSystem(LogicManager logicManager)
    {
        logic = logicManager;
    }

    /// <summary>
    /// 檢查卡牌在指定時機的條件是否全部成立。
    /// </summary>
    public static bool ConditionsPass(
        CardDefinition card,
        Piece owner,
        LogicManager logicManager,
        CardConditionTiming timing
    )
    {
        if (card == null || owner == null)
        {
            return false;
        }

        foreach (CardConditionData condition in card.conditions)
        {
            if (condition == null || condition.timing != timing)
            {
                continue;
            }

            bool passed = EvaluateCondition(
                condition.condition,
                owner,
                logicManager
            );

            if (condition.invert)
            {
                passed = !passed;
            }

            if (!passed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 處理棋子移動後的卡牌、狀態與相關效果。
    /// </summary>
    public void OnPieceMoved(Piece piece)
    {
        Debug.Log(
            $"[CardDebug][Move] Piece={Describe(piece)} | " +
            "Trigger=AfterOwnerMoves"
        );

        RechargeStatuses(piece, CardEffectTrigger.AfterOwnerMoves);
        TriggerStatusEffects(piece, CardEffectTrigger.AfterOwnerMoves);
        TriggerCardEffects(piece, CardEffectTrigger.AfterOwnerMoves);
        piece.RemoveStatusesOnTrigger(CardEffectTrigger.AfterOwnerMoves);

        if (piece.CardRuntime != null &&
            piece.CardRuntime.definition != null)
        {
            piece.CardRuntime.definition.Api.skill.OnOwnerMoved(
                new CardSkillContext(
                    logic,
                    piece,
                    piece.CardRuntime.definition
                )
                {
                    trigger = CardEffectTrigger.AfterOwnerMoves
                }
            );
        }

        CardAnimationEvents.Play(
            piece,
            piece,
            GetAnimationCard(piece, null),
            CardAnimationTiming.OnOwnerMoved
        );
    }

    /// <summary>
    /// 處理指定陣營回合開始時的卡牌與狀態效果。
    /// </summary>
    public void OnTurnStarted(bool isWhiteTurn)
    {
        logic.UpdatePiecesOnBoard();

        foreach (Piece piece in logic.piecesOnBoard.ToArray())
        {
            if (piece == null || piece.IsWhite != isWhiteTurn)
            {
                continue;
            }

            Debug.Log(
                $"[CardDebug][TurnEvent] Owner={Describe(piece)} | " +
                "Trigger=TurnStarted"
            );

            if (piece.CardRuntime != null)
            {
                piece.CardRuntime.turnsActive++;
            }

            CardAnimationEvents.Play(
                piece,
                piece,
                GetAnimationCard(piece, null),
                CardAnimationTiming.OnTurnStarted
            );

            TriggerStatusEffects(piece, CardEffectTrigger.TurnStarted);
            TriggerCardEffects(piece, CardEffectTrigger.TurnStarted);

            if (piece.CardRuntime != null &&
                piece.CardRuntime.definition != null)
            {
                piece.CardRuntime.definition.Api.skill.OnTurnStarted(
                    new CardSkillContext(
                        logic,
                        piece,
                        piece.CardRuntime.definition
                    )
                    {
                        trigger = CardEffectTrigger.TurnStarted
                    }
                );
            }

            if (Time.timeScale == 0f)
            {
                return;
            }
        }
    }

    /// <summary>
    /// 處理指定陣營回合結束時的卡牌與狀態效果。
    /// </summary>
    public void OnTurnEnded(bool endingWhiteTurn)
    {
        logic.UpdatePiecesOnBoard();

        foreach (Piece piece in logic.piecesOnBoard.ToArray())
        {
            if (piece == null || piece.IsWhite != endingWhiteTurn)
            {
                continue;
            }

            Debug.Log(
                $"[CardDebug][TurnEvent] Owner={Describe(piece)} | " +
                "Trigger=TurnEnded"
            );

            CardAnimationEvents.Play(
                piece,
                piece,
                GetAnimationCard(piece, null),
                CardAnimationTiming.OnTurnEnded
            );

            TriggerStatusEffects(piece, CardEffectTrigger.TurnEnded);
            TriggerCardEffects(piece, CardEffectTrigger.TurnEnded);

            if (piece.CardRuntime != null &&
                piece.CardRuntime.definition != null)
            {
                piece.CardRuntime.definition.Api.skill.OnTurnEnded(
                    new CardSkillContext(
                        logic,
                        piece,
                        piece.CardRuntime.definition
                    )
                    {
                        trigger = CardEffectTrigger.TurnEnded
                    }
                );
            }

            piece.AdvanceStatusDurations();

            if (Time.timeScale == 0f)
            {
                return;
            }
        }
    }

    /// <summary>
    /// 處理國王與城堡完成王車易位後的效果。
    /// </summary>
    public void OnPiecesCastled(King king, Rook rook)
    {
        if (king == null || rook == null)
        {
            return;
        }

        Debug.Log(
            $"[CardDebug][Castle] King={Describe(king)} | Rook={Describe(rook)} | " +
            "Trigger=AfterOwnerCastles"
        );

        TriggerCastlingEffects(king, rook);
        TriggerCastlingEffects(rook, king);
    }

    /// <summary>
    /// 對參與王車易位的棋子觸發相關狀態與卡牌技能。
    /// </summary>
    private void TriggerCastlingEffects(Piece owner, Piece partner)
    {
        TriggerStatusEffects(owner, CardEffectTrigger.AfterOwnerCastles);
        TriggerCardEffects(owner, CardEffectTrigger.AfterOwnerCastles);

        CardAnimationEvents.Play(
            owner,
            partner,
            GetAnimationCard(owner, null),
            CardAnimationTiming.OnOwnerCastled
        );

        if (owner.CardRuntime == null ||
            owner.CardRuntime.definition == null)
        {
            return;
        }

        owner.CardRuntime.definition.Api.skill.OnOwnerCastled(
            new CardSkillContext(
                logic,
                owner,
                owner.CardRuntime.definition
            )
            {
                target = partner,
                trigger = CardEffectTrigger.AfterOwnerCastles
            }
        );
    }

    /// <summary>
    /// 處理吃子事件，計算相關效果並更新對局狀態。
    /// </summary>
    public void OnPieceCaptured(Piece attacker, Piece target)
    {
        if (target == null)
        {
            return;
        }

        DamageContext damageContext = new DamageContext
        {
            source = attacker,
            target = target,
            sourceCard = attacker != null && attacker.CardRuntime != null
                ? attacker.CardRuntime.definition
                : null,
            trigger = CardEffectTrigger.AfterOwnerCaptures,
            tags = DamageTag.Physical | DamageTag.Capture
        };
        DamageCalculationSequence sequence =
            BuildCaptureDamageSequence(damageContext);

        Debug.Log(
            $"[CardDebug][Capture] Attacker={Describe(attacker)} | " +
            $"Target={Describe(target)} | Tags={damageContext.tags} | " +
            $"FinalDamage={sequence.finalDamage}"
        );

        Debug.Log(
            $"[CardDebug][CaptureFinal] TargetSide=" +
            $"{(target.IsWhite ? "White" : "Black")} | " +
            $"FinalDamage={sequence.finalDamage}"
        );

        logic.PlayDamageCalculation(
            sequence,
            () =>
            {
                ResolveCaptureSkillEvents(attacker, target);

                if (attacker != null)
                {
                    attacker.PlayCaptureAnimation();
                }

                if (target != null)
                {
                    target.PlayCapturedAnimation();
                }

                CardAnimationEvents.Play(
                    attacker,
                    target,
                    GetAnimationCard(attacker, null),
                    CardAnimationTiming.OnDamageDealt,
                    null,
                    sequence.finalDamage
                );
            },
            () =>
            {
                DamagePlayer(
                    sequence.damagedWhitePlayer,
                    sequence.finalDamage,
                    false,
                    sequence.damageContext
                );
            }
        );
    }

    /// <summary>
    /// 依吃子雙方與友軍的技能設定，處理吃子觸發事件。
    /// </summary>
    private void ResolveCaptureSkillEvents(Piece attacker, Piece target)
    {
        if (target == null)
        {
            return;
        }

        logic.RegisterPieceCaptured(target);
        CardSkill.Shared.TransferEventStatusesOnCapture(attacker, target);

        if (
            attacker != null &&
            attacker.CardRuntime != null &&
            attacker.CardRuntime.definition != null
        )
        {
            CardSkillContext attackerContext = new CardSkillContext(
                logic,
                attacker,
                attacker.CardRuntime.definition
            )
            {
                target = target,
                trigger = CardEffectTrigger.AfterOwnerCaptures
            };

            attacker.CardRuntime.definition.Api.skill.OnOwnerCaptures(
                attackerContext,
                target
            );
        }

        if (target.CardRuntime != null && target.CardRuntime.definition != null)
        {
            CardSkillContext targetContext = new CardSkillContext(
                logic,
                target,
                target.CardRuntime.definition
            )
            {
                target = attacker,
                trigger = CardEffectTrigger.OnOwnerDestroyed
            };

            target.CardRuntime.definition.Api.skill.OnOwnerCaptured(
                targetContext,
                attacker
            );
        }

        logic.UpdatePiecesOnBoard();
        foreach (Piece piece in logic.piecesOnBoard.ToArray())
        {
            if (
                piece == null ||
                piece.IsWhite != target.IsWhite ||
                piece.CardRuntime == null ||
                piece.CardRuntime.definition == null
            )
            {
                continue;
            }

            CardSkillContext friendlyContext = new CardSkillContext(
                logic,
                piece,
                piece.CardRuntime.definition
            )
            {
                target = target,
                trigger = CardEffectTrigger.OnOwnerDestroyed
            };

            piece.CardRuntime.definition.Api.skill.OnFriendlyPieceCaptured(
                friendlyContext,
                target,
                attacker
            );
        }
    }
    /// <summary>
    /// 結算對棋子的傷害及相關技能，建立對應的傷害演出。
    /// </summary>
    public void DealDamageToPiece(
        Piece source,
        Piece target,
        int baseDamage,
        CardEffectTrigger sourceTrigger,
        DamageTag damageTags = DamageTag.None
    )
    {
        DealDamageToPiece(
            source,
            target,
            baseDamage,
            sourceTrigger,
            GetAnimationCard(source, null),
            null,
            null,
            damageTags
        );
    }

    /// <summary>
    /// 以固定基礎傷害建立結算，依現有規則處理後續效果。
    /// </summary>
    public void DealFixedDamageToPiece(
        Piece source,
        Piece target,
        int damage,
        CardEffectTrigger sourceTrigger,
        DamageTag damageTags = DamageTag.None
    )
    {
        if (target == null)
        {
            return;
        }

        CardDefinition sourceCard = source != null && source.CardRuntime != null
            ? source.CardRuntime.definition
            : null;
        DamageContext damageContext = new DamageContext
        {
            baseDamage = damage,
            source = source,
            target = target,
            sourceCard = sourceCard,
            trigger = sourceTrigger,
            tags = damageTags != DamageTag.None
                ? damageTags
                : sourceCard != null ? sourceCard.damageTags : DamageTag.None
        };
        DamageCalculationSequence sequence =
            BuildFixedDamageSequence(damageContext);

        Debug.Log(
            $"[CardDebug][FixedPieceDamage] Source={Describe(source)} | " +
            $"Target={Describe(target)} | SourceTrigger={sourceTrigger} | " +
            $"Tags={damageContext.tags} | Damage={damage} | " +
            $"Final={sequence.finalDamage}"
        );

        logic.PlayDamageCalculation(
            sequence,
            null,
            () => DamagePlayer(
                sequence.damagedWhitePlayer,
                sequence.finalDamage,
                false,
                sequence.damageContext
            )
        );
    }

    /// <summary>
    /// 結算對棋子的傷害及相關技能，建立對應的傷害演出。
    /// </summary>
    private void DealDamageToPiece(
        Piece source,
        Piece target,
        int baseDamage,
        CardEffectTrigger sourceTrigger,
        CardDefinition animationCard,
        Sprite visualIcon,
        CardEffectData effect,
        DamageTag damageTags = DamageTag.None,
        StatusRuntime sourceStatus = null
    )
    {
        if (target == null)
        {
            return;
        }

        CardDefinition sourceCard = animationCard;
        if (sourceCard == null && source != null && source.CardRuntime != null)
        {
            sourceCard = source.CardRuntime.definition;
        }

        DamageTag resolvedTags = damageTags;
        if (resolvedTags == DamageTag.None && effect != null)
        {
            resolvedTags = effect.damageTags;
        }
        if (resolvedTags == DamageTag.None && sourceCard != null)
        {
            resolvedTags = sourceCard.damageTags;
        }

        DamageContext damageContext = new DamageContext
        {
            baseDamage = baseDamage,
            source = source,
            target = target,
            sourceCard = sourceCard,
            sourceEffect = effect,
            sourceStatus = sourceStatus,
            trigger = sourceTrigger,
            tags = resolvedTags,
            visualIcon = visualIcon
        };
        DamageCalculationSequence sequence =
            BuildEffectDamageSequence(damageContext);
        int damage = sequence.finalDamage;

        Debug.Log(
            $"[CardDebug][PieceDamage] Source={Describe(source)} | " +
            $"Target={Describe(target)} | SourceTrigger={sourceTrigger} | " +
            $"Tags={damageContext.tags} | Base={baseDamage} | Final={damage}"
        );

        logic.PlayDamageCalculation(
            sequence,
            () =>
            {
                CardAnimationEvents.Play(
                    source,
                    target,
                    animationCard,
                    CardAnimationTiming.OnDamageDealt,
                    effect,
                    damage
                );
            },
            () => DamagePlayer(
                sequence.damagedWhitePlayer,
                damage,
                false,
                sequence.damageContext
            )
        );
    }

    /// <summary>
    /// 處理事件卡對棋子造成的傷害。
    /// </summary>
    public void DealEventDamageToPiece(
        CardDefinition card,
        Piece target,
        int damage
    )
    {
        if (card == null || target == null) return;

        Sprite icon = card.skillImage != null
            ? card.skillImage
            : card.cardImage;
        DamageContext damageContext = new DamageContext
        {
            baseDamage = damage,
            source = null,
            target = target,
            sourceCard = card,
            trigger = CardEffectTrigger.OnApplied,
            tags = card.damageTags,
            visualIcon = icon
        };
        DamageCalculationSequence sequence =
            BuildEffectDamageSequence(damageContext);

        Debug.Log(
            $"[CardDebug][EventDamage] Card={card.id} {card.cardName} | " +
            $"Target={Describe(target)} | Tags={damageContext.tags} | " +
            $"Base={damage} | " +
            $"Final={sequence.finalDamage}"
        );

        logic.PlayDamageCalculation(
            sequence,
            () => CardAnimationEvents.Play(
                target,
                target,
                card,
                CardAnimationTiming.OnDamageDealt,
                null,
                sequence.finalDamage
            ),
            () => DamagePlayer(
                sequence.damagedWhitePlayer,
                sequence.finalDamage,
                false,
                sequence.damageContext
            )
        );
    }

    /// <summary>
    /// 處理事件卡對玩家的治療與相關修正。
    /// </summary>
    public void HealPlayerFromEvent(
        CardDefinition card,
        Piece target,
        int amount
    )
    {
        if (card == null || target == null) return;

        int resolvedHeal = ResolveHealAmount(target.IsWhite, amount);
        Sprite icon = card.skillImage != null
            ? card.skillImage
            : card.cardImage;
        Vector3 position = target.transform.position;
        DamageCalculationSequence sequence = new DamageCalculationSequence
        {
            attacker = target,
            target = target,
            damagedWhitePlayer = target.IsWhite,
            isHealing = true,
            finalDamage = resolvedHeal,
            resultStartWorldPosition = position
        };
        sequence.steps.Add(new DamageCalculationStep
        {
            icon = icon,
            usePieceIcon = icon == null,
            iconPiece = icon == null ? target : null,
            countingIcon = DamageCountingIcon.Heal,
            side = DamageStepSide.Attack,
            displayText = amount.ToString(),
            worldPosition = position,
            color = new Color(0.45f, 1f, 0.55f, 1f)
        });
        AddHealModifierSteps(target.IsWhite, amount, sequence);

        Debug.Log(
            $"[CardDebug][EventHeal] Card={card.id} {card.cardName} | " +
            $"Target={Describe(target)} | Base={amount} | " +
            $"Final={resolvedHeal}"
        );

        logic.PlayDamageCalculation(
            sequence,
            null,
            () => HealPlayer(target.IsWhite, resolvedHeal, false)
        );
    }

    /// <summary>
    /// 處理指定玩家的治療，套用相關效果並更新血量呈現。
    /// </summary>
    public void HealPlayer(
        bool isWhitePlayer,
        int amount,
        bool resolveModifiers = true
    )
    {
        int healAmount = resolveModifiers
            ? ResolveHealAmount(isWhitePlayer, amount)
            : Mathf.Max(0, amount);

        if (!logic.ShouldApplyLocalHealthChange)
        {
            Debug.Log(
                $"[NetworkGame][ClientHealthSkipped] Heal | " +
                $"Player={(isWhitePlayer ? "White" : "Black")} | Amount={healAmount}"
            );
            return;
        }

        int previousHealth = isWhitePlayer ? logic.whiteHealth : logic.blackHealth;
        int healedHealth = Mathf.Min(LogicManager.MaxHealth, previousHealth + healAmount);

        if (isWhitePlayer)
        {
            logic.whiteHealth = healedHealth;
        }
        else
        {
            logic.blackHealth = healedHealth;
        }

        Debug.Log(
            $"[CardDebug][Heal] Player={(isWhitePlayer ? "White" : "Black")} | " +
            $"Amount={healAmount} | Base={amount} | {previousHealth}->{healedHealth}"
        );
        OperateLogUI.LogHeal(isWhitePlayer, healAmount);

        logic.RefreshHealthUi();
    }

    /// <summary>
    /// 從指定陣營目前可用的棋子中隨機選取一枚。
    /// </summary>
    public Piece GetRandomPiece(bool isWhitePlayer)
    {
        List<Piece> candidates = new List<Piece>();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Piece piece = logic.boardMap[x, y];
                if (piece != null && piece.IsWhite == isWhitePlayer)
                {
                    candidates.Add(piece);
                }
            }
        }

        if (candidates.Count == 0)
        {
            Debug.Log(
                $"[CardDebug][RandomTarget] Side=" +
                $"{(isWhitePlayer ? "White" : "Black")} | Result=None"
            );
            return null;
        }

        Piece result = candidates[Random.Range(0, candidates.Count)];

        Debug.Log(
            $"[CardDebug][RandomTarget] Side=" +
            $"{(isWhitePlayer ? "White" : "Black")} | " +
            $"Result={Describe(result)}"
        );

        return result;
    }

    /// <summary>
    /// 取得棋子套用目前卡牌、狀態與場地修正後的攻擊力。
    /// </summary>
    public int GetEffectiveAttack(Piece piece)
    {
        if (piece == null)
        {
            return 0;
        }

        int selfAttack = ResolveStatusNumeric(
            piece,
            CardEffectTrigger.OnApplied,
            CardEffectType.ModifyAttack,
            piece.Attack
        );

        return Mathf.Max(
            0,
            selfAttack + GetFriendlyAttackBonus(piece.IsWhite)
        );
    }

    /// <summary>
    /// 取得棋子套用目前效果後的價值。
    /// </summary>
    public int GetEffectiveValue(Piece piece)
    {
        return ResolveEffectiveValue(piece, null);
    }

    /// <summary>
    /// 計算棋子的有效價值，並記錄供畫面顯示的計算步驟。
    /// </summary>
    private int GetEffectiveValueWithSteps(
        Piece piece,
        DamageCalculationSequence sequence
    )
    {
        return ResolveEffectiveValue(piece, sequence);
    }

    /// <summary>
    /// 依目前效果計算棋子價值，必要時附加計算步驟。
    /// </summary>
    private int ResolveEffectiveValue(
        Piece piece,
        DamageCalculationSequence sequence
    )
    {
        if (piece == null) return 0;

        int result = sequence == null
            ? ResolveStatusNumeric(
                piece,
                CardEffectTrigger.OnApplied,
                CardEffectType.ModifyValue,
                piece.Value)
            : ResolveStatusNumericWithSteps(
                piece,
                CardEffectTrigger.OnApplied,
                CardEffectType.ModifyValue,
                piece.Value,
                sequence);

        result = sequence == null
            ? ResolveCardNumeric(
                piece,
                CardEffectTrigger.OnApplied,
                CardEffectType.ModifyValue,
                result)
            : ResolveCardNumericWithSteps(
                piece,
                CardEffectTrigger.OnApplied,
                CardEffectType.ModifyValue,
                result,
                sequence);

        logic.UpdatePiecesOnBoard();
        foreach (Piece source in logic.piecesOnBoard)
        {
            if (source == null || source.IsWhite != piece.IsWhite) continue;

            result = sequence == null
                ? ResolveStatusNumeric(
                    source,
                    CardEffectTrigger.OnApplied,
                    CardEffectType.ModifyFriendlyValue,
                    result)
                : ResolveStatusNumericWithSteps(
                    source,
                    CardEffectTrigger.OnApplied,
                    CardEffectType.ModifyFriendlyValue,
                    result,
                    sequence);

            result = sequence == null
                ? ResolveCardNumeric(
                    source,
                    CardEffectTrigger.OnApplied,
                    CardEffectType.ModifyFriendlyValue,
                    result)
                : ResolveCardNumericWithSteps(
                    source,
                    CardEffectTrigger.OnApplied,
                    CardEffectType.ModifyFriendlyValue,
                result,
                sequence);
        }

        int fieldValueModifier = logic.GetFieldValueModifier();
        if (fieldValueModifier != 0)
        {
            int previous = result;
            result = Mathf.Max(0, result + fieldValueModifier);
            if (sequence != null && previous != result)
            {
                AddValueStep(
                    sequence,
                    null,
                    Mathf.Abs(previous - result),
                    piece.transform.position,
                    result >= previous
                        ? new Color(0.45f, 1f, 0.55f, 1f)
                        : new Color(1f, 0.45f, 0.35f, 1f),
                    piece
                );
            }
        }

        return Mathf.Max(0, result);
    }

    /// <summary>
    /// 結算指定玩家承受的傷害並更新血量。
    /// </summary>
    public void DamagePlayer(
        bool isWhitePlayer,
        int damage,
        bool resolveModifiers = true,
        DamageContext damageContext = null
    )
    {
        damage = Mathf.Max(0, damage);
        if (resolveModifiers)
        {
            damage = ResolvePlayerDamageTakenWithSteps(
                isWhitePlayer,
                damage,
                null
            );
        }

        if (!logic.ShouldApplyLocalHealthChange)
        {
            Debug.Log(
                $"[NetworkGame][ClientHealthSkipped] Damage | " +
                $"Player={(isWhitePlayer ? "White" : "Black")} | Damage={damage}"
            );
            return;
        }

        int previousHealth = isWhitePlayer ? logic.whiteHealth : logic.blackHealth;
        int newHealth = Mathf.Max(0, previousHealth - damage);

        if (isWhitePlayer)
        {
            logic.whiteHealth = newHealth;
        }
        else
        {
            logic.blackHealth = newHealth;
        }

        Debug.Log(
            $"[CardDebug][PlayerDamage] Player=" +
            $"{(isWhitePlayer ? "White" : "Black")} | " +
            $"Tags={(damageContext != null ? damageContext.tags : DamageTag.None)} | " +
            $"Damage={damage} | {previousHealth}->{newHealth}"
        );
        OperateLogUI.LogDamage(isWhitePlayer, damage, damageContext);

        logic.RefreshHealthUi();
        logic.CheckHealthGameOver();
    }

    /// <summary>
    /// 支付卡牌要求的血量代價，並處理對應的結算與勝負檢查。
    /// </summary>
    public void PayHealthCost(
        bool isWhitePlayer,
        int amount,
        CardDefinition sourceCard = null,
        Piece visualSource = null
    )
    {
        int cost = Mathf.Max(0, amount);
        DamageTag tags = sourceCard != null &&
            sourceCard.damageTags != DamageTag.None
                ? sourceCard.damageTags | DamageTag.Cost
                : DamageTag.Cost;
        DamageContext damageContext = new DamageContext
        {
            baseDamage = cost,
            resolvedDamage = cost,
            sourceCard = sourceCard,
            source = visualSource,
            target = visualSource,
            tags = tags
        };

        Debug.Log(
            $"[CardDebug][HealthCost] Player=" +
            $"{(isWhitePlayer ? "White" : "Black")} | " +
            $"Card={(sourceCard != null ? sourceCard.id : "None")} | " +
            $"Tags={damageContext.tags} | Cost={cost}"
        );

        Sprite icon = null;
        if (sourceCard != null)
        {
            icon = sourceCard.skillImage != null
                ? sourceCard.skillImage
                : sourceCard.cardImage;
        }

        Vector3 position = visualSource != null
            ? visualSource.transform.position
            : Vector3.zero;

        DamageCalculationSequence sequence = new DamageCalculationSequence
        {
            damageContext = damageContext,
            attacker = visualSource,
            target = visualSource,
            damagedWhitePlayer = isWhitePlayer,
            finalDamage = cost,
            resultStartWorldPosition = position
        };

        sequence.steps.Add(new DamageCalculationStep
        {
            icon = icon,
            iconPiece = visualSource,
            usePieceIcon = icon == null && visualSource != null,
            countingIcon = DamageCountingIcon.Cost,
            side = DamageStepSide.Attack,
            displayText = cost.ToString(),
            worldPosition = position,
            color = new Color(1f, 0.45f, 0.35f, 1f)
        });

        logic.PlayDamageCalculation(
            sequence,
            null,
            () => DamagePlayer(isWhitePlayer, cost, false, damageContext)
        );
    }

    /// <summary>
    /// 計算友軍技能提供給指定棋子的攻擊加成。
    /// </summary>
    private int GetFriendlyAttackBonus(bool isWhitePlayer)
    {
        int bonus = 0;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Piece source = logic.boardMap[x, y];
                if (source == null || source.IsWhite != isWhitePlayer)
                {
                    continue;
                }

                bonus = ResolveStatusNumeric(
                    source,
                    CardEffectTrigger.OnApplied,
                    CardEffectType.ModifyFriendlyAttack,
                    bonus
                );
                bonus = ResolveCardNumeric(
                    source,
                    CardEffectTrigger.OnApplied,
                    CardEffectType.ModifyFriendlyAttack,
                    bonus
                );
                bonus = ResolveSkillFriendlyAttack(
                    source,
                    bonus,
                    null
                );
            }
        }

        bonus += logic.GetFieldAttackBonus();
        return Mathf.Max(0, bonus);
    }

    /// <summary>
    /// 依技能與場地修正計算最終治療量。
    /// </summary>
    private int ResolveHealAmount(bool isWhitePlayer, int amount)
    {
        int result = Mathf.Max(0, amount);

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Piece source = logic.boardMap[x, y];
                if (
                    source == null ||
                    source.IsWhite != isWhitePlayer ||
                    source.CardRuntime == null ||
                    source.CardRuntime.definition == null
                )
                {
                    continue;
                }

                CardSkill skill = source.CardRuntime.definition.Api.skill;
                if (skill == null)
                {
                    continue;
                }

                int previous = result;
                result = Mathf.Max(
                    0,
                    skill.ModifyHealAmount(
                        new CardSkillContext(
                            logic,
                            source,
                            source.CardRuntime.definition
                        ),
                        result
                    )
                );

                if (previous != result)
                {
                    Debug.Log(
                        $"[CardDebug][HealModifier] Source={Describe(source)} | " +
                        $"Card={source.CardRuntime.definition.cardName} | " +
                        $"{previous}->{result}"
                    );
                }
            }
        }

        return logic.ApplyFieldHealModifiers(isWhitePlayer, result, null);
    }

    /// <summary>
    /// 將治療修正加入計算步驟，供傷害視覺化使用。
    /// </summary>
    private void AddHealModifierSteps(
        bool isWhitePlayer,
        int baseAmount,
        DamageCalculationSequence sequence
    )
    {
        if (sequence == null) return;

        int result = Mathf.Max(0, baseAmount);
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Piece source = logic.boardMap[x, y];
                if (source == null || source.IsWhite != isWhitePlayer ||
                    source.CardRuntime == null ||
                    source.CardRuntime.definition == null)
                {
                    continue;
                }

                CardSkill skill = source.CardRuntime.definition.Api.skill;
                if (skill == null) continue;

                int previous = result;
                result = Mathf.Max(
                    0,
                    skill.ModifyHealAmount(
                        new CardSkillContext(
                            logic,
                            source,
                            source.CardRuntime.definition
                        ),
                        result
                    )
                );

                if (previous == result) continue;
                CardDefinition sourceCard = source.CardRuntime.definition;
                Sprite sourceIcon = sourceCard.skillImage != null
                    ? sourceCard.skillImage
                    : sourceCard.cardImage;
                sequence.steps.Add(new DamageCalculationStep
                {
                    icon = sourceIcon,
                    iconPiece = source,
                    usePieceIcon = sourceIcon == null,
                    countingIcon = result < previous
                        ? DamageCountingIcon.AntiHeal
                        : DamageCountingIcon.Heal,
                    side = DamageStepSide.Defense,
                    displayText = Mathf.Abs(previous - result).ToString(),
                    worldPosition = source.transform.position,
                    color = result < previous
                        ? new Color(1f, 0.45f, 0.35f, 1f)
                        : new Color(0.45f, 1f, 0.55f, 1f)
                });
            }
        }

        logic.ApplyFieldHealModifiers(isWhitePlayer, result, sequence);
    }

    /// <summary>
    /// 建立吃子傷害的計算序列，包含攻擊與防禦修正。
    /// </summary>
    private DamageCalculationSequence BuildCaptureDamageSequence(
        DamageContext damageContext
    )
    {
        Piece attacker = damageContext.source;
        Piece target = damageContext.target;
        DamageCalculationSequence sequence =
            new DamageCalculationSequence
            {
                damageContext = damageContext,
                attacker = attacker,
                target = target,
                isCapture = true,
                damagedWhitePlayer = target != null && target.IsWhite,
                resultStartWorldPosition = target != null
                    ? target.transform.position
                    : Vector3.zero
            };

        int friendlyBonus = GetFriendlyAttackBonusWithSteps(
            attacker.IsWhite,
            null,
            attacker
        );
        int attack = GetAttackWithSteps(attacker, friendlyBonus, null);
        sequence.steps.Add(new DamageCalculationStep
        {
            iconPiece = attacker,
            usePieceIcon = true,
            countingIcon = DamageCountingIcon.Attack,
            side = DamageStepSide.Attack,
            displayText = attack.ToString(),
            worldPosition = attacker.transform.position,
            color = new Color(0.45f, 1f, 0.55f, 1f)
        });
        int attackDamage = ResolveDamageDealtWithSteps(
            attacker,
            attack,
            sequence,
            false
        );

        int valueDamage = GetEffectiveValue(target);
        sequence.steps.Add(new DamageCalculationStep
        {
            iconPiece = target,
            usePieceIcon = true,
            side = DamageStepSide.Defense,
            displayText = valueDamage.ToString(),
            worldPosition = target.transform.position,
            color = Color.white
        });
        valueDamage = ResolveCardNumericWithSteps(
            target,
            CardEffectTrigger.BeforeOwnerTakesDamage,
            CardEffectType.ModifyDamageTaken,
            valueDamage,
            sequence
        );
        valueDamage = ResolveStatusNumericWithSteps(
            target,
            CardEffectTrigger.BeforeOwnerTakesDamage,
            CardEffectType.ModifyDamageTaken,
            valueDamage,
            sequence
        );
        valueDamage = ResolveFriendlyDamageTakenSkillsWithSteps(
            target,
            valueDamage,
            sequence
        );

        int finalDamage = Mathf.Max(0, attackDamage + valueDamage);
        finalDamage = ResolvePlayerDamageTakenWithSteps(
            sequence.damagedWhitePlayer,
            finalDamage,
            sequence
        );
        finalDamage = ResolveDamageDealtWithSteps(
            attacker,
            finalDamage,
            sequence,
            true
        );

        Debug.Log(
            $"[CardDebug][DamageFormula] Attacker={Describe(attacker)} | " +
            $"Target={Describe(target)} | Value={valueDamage} | " +
            $"ATK={attackDamage} | " +
            $"FinalDamage={finalDamage}"
        );

        sequence.finalDamage = Mathf.Max(0, finalDamage);
        damageContext.resolvedDamage = sequence.finalDamage;
        return sequence;
    }

    /// <summary>
    /// 建立卡牌或狀態效果傷害的計算序列。
    /// </summary>
    private DamageCalculationSequence BuildEffectDamageSequence(
        DamageContext damageContext
    )
    {
        Piece source = damageContext.source;
        Piece target = damageContext.target;
        int baseDamage = damageContext.baseDamage;
        Sprite visualIcon = damageContext.visualIcon;
        DamageCalculationSequence sequence =
            new DamageCalculationSequence
            {
                damageContext = damageContext,
                attacker = source,
                target = target,
                damagedWhitePlayer = target != null && target.IsWhite,
                resultStartWorldPosition = target != null
                    ? target.transform.position
                    : Vector3.zero
            };

        int damage = Mathf.Max(0, baseDamage);

        sequence.steps.Add(new DamageCalculationStep
        {
            icon = visualIcon,
            iconPiece = source,
            usePieceIcon = visualIcon == null,
            countingIcon = GetCountingIcon(damageContext.tags),
            side = DamageStepSide.Attack,
            displayText = damage.ToString(),
            worldPosition = source != null
                ? source.transform.position
                : target != null ? target.transform.position : Vector3.zero,
            color = new Color(0.45f, 1f, 0.55f, 1f)
        });

        damage = ResolveDamageDealtWithSteps(
            source,
            damage,
            sequence,
            false
        );

        damage = ResolveCardNumericWithSteps(
            target,
            CardEffectTrigger.BeforeOwnerTakesDamage,
            CardEffectType.ModifyDamageTaken,
            damage,
            sequence
        );
        damage = ResolveStatusNumericWithSteps(
            target,
            CardEffectTrigger.BeforeOwnerTakesDamage,
            CardEffectType.ModifyDamageTaken,
            damage,
            sequence
        );
        damage = ResolveFriendlyDamageTakenSkillsWithSteps(
            target,
            damage,
            sequence
        );
        damage = ResolvePlayerDamageTakenWithSteps(
            sequence.damagedWhitePlayer,
            damage,
            sequence
        );
        damage = ResolveDamageDealtWithSteps(
            source,
            damage,
            sequence,
            true
        );

        sequence.finalDamage = Mathf.Max(0, damage);
        damageContext.resolvedDamage = sequence.finalDamage;
        return sequence;
    }

    /// <summary>
    /// 建立固定基礎傷害的計算序列。
    /// </summary>
    private DamageCalculationSequence BuildFixedDamageSequence(
        DamageContext damageContext
    )
    {
        Piece source = damageContext.source;
        Piece target = damageContext.target;
        int baseDamage = damageContext.baseDamage;
        DamageCalculationSequence sequence =
            new DamageCalculationSequence
            {
                damageContext = damageContext,
                attacker = source,
                target = target,
                damagedWhitePlayer = target != null && target.IsWhite,
                resultStartWorldPosition = target != null
                    ? target.transform.position
                    : Vector3.zero
            };

        int damage = Mathf.Max(0, baseDamage);

        sequence.steps.Add(new DamageCalculationStep
        {
            iconPiece = source,
            usePieceIcon = source != null,
            countingIcon = GetCountingIcon(damageContext.tags),
            side = DamageStepSide.Attack,
            displayText = damage.ToString(),
            worldPosition = source != null
                ? source.transform.position
                : target.transform.position,
            color = new Color(1f, 0.45f, 0.35f, 1f)
        });

        damage = ResolveDamageDealtWithSteps(
            source,
            damage,
            sequence,
            false
        );

        damage = ResolveCardNumericWithSteps(
            target,
            CardEffectTrigger.BeforeOwnerTakesDamage,
            CardEffectType.ModifyDamageTaken,
            damage,
            sequence
        );
        damage = ResolveStatusNumericWithSteps(
            target,
            CardEffectTrigger.BeforeOwnerTakesDamage,
            CardEffectType.ModifyDamageTaken,
            damage,
            sequence
        );
        damage = ResolveFriendlyDamageTakenSkillsWithSteps(
            target,
            damage,
            sequence
        );
        damage = ResolvePlayerDamageTakenWithSteps(
            sequence.damagedWhitePlayer,
            damage,
            sequence
        );
        damage = ResolveDamageDealtWithSteps(
            source,
            damage,
            sequence,
            true
        );

        sequence.finalDamage = Mathf.Max(0, damage);
        damageContext.resolvedDamage = sequence.finalDamage;
        return sequence;
    }

    /// <summary>
    /// 套用友軍提供的承傷技能，並記錄計算步驟。
    /// </summary>
    private int ResolveFriendlyDamageTakenSkillsWithSteps(
        Piece target,
        int baseDamage,
        DamageCalculationSequence sequence
    )
    {
        if (target == null)
        {
            return Mathf.Max(0, baseDamage);
        }

        int result = Mathf.Max(0, baseDamage);
        logic.UpdatePiecesOnBoard();

        foreach (Piece source in logic.piecesOnBoard)
        {
            if (
                source == null ||
                source.IsWhite != target.IsWhite ||
                source.CardRuntime == null ||
                source.CardRuntime.definition == null
            )
            {
                continue;
            }

            CardDefinition card = source.CardRuntime.definition;
            CardSkill skill = card.Api.skill;
            if (skill == null)
            {
                continue;
            }

            CardSkillContext context = new CardSkillContext(
                logic,
                source,
                card
            )
            {
                target = target,
                trigger = CardEffectTrigger.BeforeOwnerTakesDamage,
                amount = result
            };

            int previous = result;
            result = Mathf.Max(0, skill.ModifyDamageTaken(context, result));
            bool paladinTriggered = card.id == "J06" &&
                CardSkill.IsInNineGrid(source, target);
            if (result == previous && !paladinTriggered)
            {
                continue;
            }

            sequence.steps.Add(new DamageCalculationStep
            {
                icon = card.skillImage,
                iconPiece = source,
                usePieceIcon = card.skillImage == null,
                countingIcon = DamageCountingIcon.Defense,
                side = DamageStepSide.Defense,
                displayText = paladinTriggered
                    ? "1"
                    : Mathf.Abs(previous - result).ToString(),
                worldPosition = source.transform.position,
                color = new Color(0.45f, 0.75f, 1f, 1f)
            });
        }

        return result;
    }

    /// <summary>
    /// 套用玩家承傷修正，並記錄計算步驟。
    /// </summary>
    public int ResolvePlayerDamageTakenWithSteps(
        bool damagedWhitePlayer,
        int baseDamage,
        DamageCalculationSequence sequence
    )
    {
        int result = Mathf.Max(0, baseDamage);

        logic.UpdatePiecesOnBoard();
        foreach (Piece source in logic.piecesOnBoard)
        {
            if (source == null || source.IsWhite != damagedWhitePlayer)
            {
                continue;
            }

            result = ResolveStatusNumericWithSteps(
                source,
                CardEffectTrigger.OnApplied,
                CardEffectType.ModifyOwnerPlayerDamage,
                result,
                sequence
            );

            result = ResolveCardNumericWithSteps(
                source,
                CardEffectTrigger.OnApplied,
                CardEffectType.ModifyOwnerPlayerDamage,
                result,
                sequence
            );
        }

        result = logic.ApplyFieldPlayerDamageTakenModifiers(
            damagedWhitePlayer,
            result,
            sequence
        );

        return Mathf.Max(0, result);
    }

    /// <summary>
    /// 建立治療計算序列及畫面所需的步驟資料。
    /// </summary>
    private DamageCalculationSequence BuildHealSequence(
        Piece source,
        bool healedWhitePlayer,
        int baseAmount,
        int resolvedAmount,
        Sprite visualIcon
    )
    {
        DamageCalculationSequence sequence =
            new DamageCalculationSequence
            {
                attacker = source,
                target = source,
                damagedWhitePlayer = healedWhitePlayer,
                isHealing = true,
                finalDamage = Mathf.Max(0, resolvedAmount),
                resultStartWorldPosition = source != null
                    ? source.transform.position
                    : Vector3.zero
            };

        sequence.steps.Add(new DamageCalculationStep
        {
            icon = visualIcon,
            iconPiece = source,
            usePieceIcon = visualIcon == null,
            countingIcon = DamageCountingIcon.Heal,
            side = DamageStepSide.Attack,
            displayText = Mathf.Max(0, baseAmount).ToString(),
            worldPosition = source != null
                ? source.transform.position
                : Vector3.zero,
            color = new Color(0.45f, 1f, 0.55f, 1f)
        });

        AddHealModifierSteps(healedWhitePlayer, baseAmount, sequence);

        return sequence;
    }

    /// <summary>
    /// 計算棋子攻擊力並記錄各項加成來源。
    /// </summary>
    private int GetAttackWithSteps(
        Piece attacker,
        int friendlyBonus,
        DamageCalculationSequence sequence
    )
    {
        if (attacker == null)
        {
            return 0;
        }

        int attack = ResolveSelfAttackWithSteps(
            attacker,
            attacker.Attack,
            friendlyBonus,
            sequence
        );

        return Mathf.Max(0, attack);
    }

    /// <summary>
    /// 計算友軍攻擊加成並記錄其來源與數值。
    /// </summary>
    private int GetFriendlyAttackBonusWithSteps(
        bool isWhitePlayer,
        DamageCalculationSequence sequence,
        Piece additionalSource = null
    )
    {
        int bonus = 0;
        bool includedAdditionalSource = false;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                Piece source = logic.boardMap[x, y];
                if (source == null || source.IsWhite != isWhitePlayer)
                {
                    continue;
                }

                if (source == additionalSource)
                {
                    includedAdditionalSource = true;
                }

                bonus = ResolveFriendlyAttackSourceWithSteps(
                    source,
                    bonus,
                    sequence
                );
            }
        }

        if (
            additionalSource != null &&
            additionalSource.IsWhite == isWhitePlayer &&
            !includedAdditionalSource
        )
        {
            bonus = ResolveFriendlyAttackSourceWithSteps(
                additionalSource,
                bonus,
                sequence
            );
        }

        bonus += logic.GetFieldAttackBonus();
        return Mathf.Max(0, bonus);
    }

    /// <summary>
    /// 計算指定友軍來源提供的攻擊修正與顯示步驟。
    /// </summary>
    private int ResolveFriendlyAttackSourceWithSteps(
        Piece source,
        int baseBonus,
        DamageCalculationSequence sequence
    )
    {
        int bonus = ResolveStatusNumericWithSteps(
            source,
            CardEffectTrigger.OnApplied,
            CardEffectType.ModifyFriendlyAttack,
            baseBonus,
            sequence
        );

        bonus = ResolveCardNumericWithSteps(
            source,
            CardEffectTrigger.OnApplied,
            CardEffectType.ModifyFriendlyAttack,
            bonus,
            sequence
        );

        return ResolveSkillFriendlyAttack(source, bonus, sequence);
    }

    /// <summary>
    /// 依技能規則計算友軍攻擊修正。
    /// </summary>
    private int ResolveSkillFriendlyAttack(
        Piece source,
        int baseBonus,
        DamageCalculationSequence sequence
    )
    {
        if (
            source == null ||
            source.CardRuntime == null ||
            source.CardRuntime.definition == null
        )
        {
            return baseBonus;
        }

        CardSkill skill = source.CardRuntime.definition.Api.skill;
        if (skill == null)
        {
            return baseBonus;
        }

        int result = Mathf.Max(
            0,
            skill.ModifyFriendlyAttack(
                new CardSkillContext(
                    logic,
                    source,
                    source.CardRuntime.definition
                ),
                baseBonus
            )
        );

        if (sequence != null && result != baseBonus)
        {
            Sprite statusIcon =
                GetFirstStatusIcon(source.CardRuntime.definition);

            AddValueStep(
                sequence,
                statusIcon,
                result,
                source.transform.position,
                new Color(0.45f, 1f, 0.55f, 1f),
                statusIcon == null ? source : null
            );
        }

        return result;
    }

    /// <summary>
    /// 取得卡牌第一個可用狀態圖示，供提示或結算顯示。
    /// </summary>
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

    /// <summary>
    /// 計算棋子自身的攻擊修正並記錄步驟。
    /// </summary>
    private int ResolveSelfAttackWithSteps(
        Piece owner,
        int baseAttack,
        int friendlyBonus,
        DamageCalculationSequence sequence
    )
    {
        if (owner == null)
        {
            return 0;
        }

        float result = baseAttack;
        bool showedAttackSource = false;

        foreach (StatusRuntime status in owner.Statuses)
        {
            if (
                status == null ||
                status.definition == null ||
                !status.HasCharges
            )
            {
                continue;
            }

            foreach (CardEffectData effect in status.definition.effects)
            {
                if (
                    effect == null ||
                    effect.trigger != CardEffectTrigger.OnApplied ||
                    effect.effectType != CardEffectType.ModifyAttack
                )
                {
                    continue;
                }

                result = ApplyValueOperation(
                    result,
                    effect.operation,
                    effect.value
                );
                showedAttackSource = true;

                AddValueStep(
                    sequence,
                    status.definition.icon,
                    Mathf.RoundToInt(result) + friendlyBonus,
                    owner.transform.position,
                    new Color(0.45f, 1f, 0.55f, 1f)
                );
            }
        }

        int totalAttack = Mathf.Max(
            0,
            Mathf.RoundToInt(result) + friendlyBonus
        );

        if (!showedAttackSource)
        {
            AddPieceValueStep(
                sequence,
                owner,
                totalAttack,
                owner.transform.position,
                new Color(0.45f, 1f, 0.55f, 1f)
            );
        }

        return totalAttack;
    }

    /// <summary>
    /// 套用卡牌的數值效果，並記錄供畫面顯示的計算步驟。
    /// </summary>
    private int ResolveCardNumericWithSteps(
        Piece owner,
        CardEffectTrigger trigger,
        CardEffectType effectType,
        int baseValue,
        DamageCalculationSequence sequence
    )
    {
        if (owner == null || owner.CardRuntime == null)
        {
            return baseValue;
        }

        CardDefinition card = owner.CardRuntime.definition;
        if (card == null)
        {
            return baseValue;
        }

        if (!ConditionsPass(card, owner, logic, CardConditionTiming.WhileActive))
        {
            return baseValue;
        }

        float result = baseValue;

        foreach (CardEffectData effect in card.effects)
        {
            if (
                effect == null ||
                effect.trigger != trigger ||
                effect.effectType != effectType
            )
            {
                continue;
            }

            float previousValue = result;
            result = ApplyValueOperation(result, effect.operation, effect.value);
            owner.CardRuntime.hasTriggered = true;

            AddCalculationStep(
                sequence,
                null,
                card.cardName,
                effect,
                previousValue,
                result,
                owner.transform.position,
                owner
            );
        }

        return Mathf.Max(0, Mathf.RoundToInt(result));
    }

    /// <summary>
    /// 套用狀態的數值效果，並記錄供畫面顯示的計算步驟。
    /// </summary>
    private int ResolveStatusNumericWithSteps(
        Piece owner,
        CardEffectTrigger trigger,
        CardEffectType effectType,
        int baseValue,
        DamageCalculationSequence sequence
    )
    {
        if (owner == null)
        {
            return baseValue;
        }

        float result = baseValue;

        foreach (StatusRuntime status in owner.Statuses)
        {
            if (
                status == null ||
                status.definition == null ||
                !status.HasCharges
            )
            {
                continue;
            }

            foreach (CardEffectData effect in status.definition.effects)
            {
                if (
                    effect == null ||
                    effect.trigger != trigger ||
                    effect.effectType != effectType
                )
                {
                    continue;
                }

                float previousValue = result;
                result = ApplyValueOperation(result, effect.operation, effect.value);

                AddCalculationStep(
                    sequence,
                    status.definition.icon,
                    status.definition.statusName,
                    effect,
                    previousValue,
                    result,
                    owner.transform.position
                );
            }
        }

        return Mathf.Max(0, Mathf.RoundToInt(result));
    }

    /// <summary>
    /// 計算造成傷害的修正並記錄各項來源。
    /// </summary>
    public int ResolveDamageDealtWithSteps(
        Piece source,
        int baseValue,
        DamageCalculationSequence sequence,
        bool setOnly
    )
    {
        if (source == null)
        {
            return setOnly
                ? ResolveGlobalDamageSetWithSteps(baseValue, sequence)
                : logic.ApplyFieldDamageDealtModifiers(
                    sequence != null ? sequence.damageContext : null,
                    baseValue,
                    sequence
                );
        }

        float result = baseValue;
        CardDefinition card = source.CardRuntime != null
            ? source.CardRuntime.definition
            : null;

        if (card != null && ConditionsPass(
            card,
            source,
            logic,
            CardConditionTiming.WhileActive))
        {
            foreach (CardEffectData effect in card.effects)
            {
                if (!IsDamageDealtEffectForPass(effect, setOnly)) continue;

                float previousValue = result;
                result = ApplyValueOperation(
                    result,
                    effect.operation,
                    effect.value
                );
                source.CardRuntime.hasTriggered = true;
                AddCalculationStep(
                    sequence,
                    card.skillImage,
                    card.cardName,
                    effect,
                    previousValue,
                    result,
                    source.transform.position,
                    card.skillImage == null ? source : null
                );
            }
        }

        foreach (StatusRuntime status in source.Statuses)
        {
            if (status == null || status.definition == null ||
                !status.HasCharges)
            {
                continue;
            }

            foreach (CardEffectData effect in status.definition.effects)
            {
                if (!IsDamageDealtEffectForPass(effect, setOnly)) continue;

                float previousValue = result;
                result = ApplyValueOperation(
                    result,
                    effect.operation,
                    effect.value
                );
                AddCalculationStep(
                    sequence,
                    status.definition.icon,
                    status.definition.statusName,
                    effect,
                    previousValue,
                    result,
                    source.transform.position,
                    status.definition.icon == null ? source : null
                );
            }
        }

        if (!setOnly)
        {
            result = logic.ApplyFieldDamageDealtModifiers(
                sequence != null ? sequence.damageContext : null,
                Mathf.RoundToInt(result),
                sequence
            );
        }

        int resolved = Mathf.Max(0, Mathf.RoundToInt(result));
        if (resolved != baseValue)
        {
            Debug.Log(
                $"[CardDebug][DamageDealtModifier] " +
                $"Source={Describe(source)} | SetPass={setOnly} | " +
                $"Damage={baseValue}->{resolved}"
            );
        }
        return resolved;
    }

    /// <summary>
    /// 處理全域傷害設定效果，並記錄計算步驟。
    /// </summary>
    private int ResolveGlobalDamageSetWithSteps(
        int baseValue,
        DamageCalculationSequence sequence
    )
    {
        logic.UpdatePiecesOnBoard();

        foreach (Piece owner in logic.piecesOnBoard)
        {
            if (owner == null) continue;

            foreach (StatusRuntime status in owner.Statuses)
            {
                if (status == null || status.definition == null ||
                    !status.HasCharges)
                {
                    continue;
                }

                foreach (CardEffectData effect in status.definition.effects)
                {
                    if (!IsDamageDealtEffectForPass(effect, true)) continue;

                    float result = ApplyValueOperation(
                        baseValue,
                        effect.operation,
                        effect.value
                    );
                    AddCalculationStep(
                        sequence,
                        status.definition.icon,
                        status.definition.statusName,
                        effect,
                        baseValue,
                        result,
                        owner.transform.position,
                        status.definition.icon == null ? owner : null
                    );

                    int resolved = Mathf.Max(0, Mathf.RoundToInt(result));
                    Debug.Log(
                        $"[CardDebug][GlobalDamageSet] " +
                        $"Status={status.definition.statusName} | " +
                        $"Damage={baseValue}->{resolved}"
                    );
                    return resolved;
                }
            }
        }

        return Mathf.Max(0, baseValue);
    }

    /// <summary>
    /// 判斷傷害效果是否屬於目前的修正處理階段。
    /// </summary>
    private static bool IsDamageDealtEffectForPass(
        CardEffectData effect,
        bool setOnly
    )
    {
        return effect != null &&
            effect.trigger == CardEffectTrigger.OnApplied &&
            effect.effectType == CardEffectType.ModifyDamageDealt &&
            (effect.operation == CardValueOperation.Set) == setOnly;
    }

    /// <summary>
    /// 加入一筆傷害計算顯示步驟。
    /// </summary>
    private void AddCalculationStep(
        DamageCalculationSequence sequence,
        Sprite icon,
        string title,
        CardEffectData effect,
        float previousValue,
        float result,
        Vector3 worldPosition,
        Piece iconPiece = null
    )
    {
        if (sequence == null || effect == null)
        {
            return;
        }

        int change = Mathf.Abs(
            Mathf.RoundToInt(result) - Mathf.RoundToInt(previousValue)
        );
        int displayAmount = change > 0
            ? change
            : Mathf.Abs(effect.value);

        bool isDefense =
            effect.effectType == CardEffectType.ModifyDamageTaken ||
            effect.effectType == CardEffectType.ModifyOwnerPlayerDamage;
        bool isAttack =
            effect.effectType == CardEffectType.ModifyDamageDealt ||
            effect.effectType == CardEffectType.ModifyAttack ||
            effect.effectType == CardEffectType.ModifyFriendlyAttack;
        DamageCountingIcon countingIcon = DamageCountingIcon.None;
        if (isDefense)
        {
            countingIcon = DamageCountingIcon.Defense;
        }
        else if (isAttack && result < previousValue)
        {
            countingIcon = DamageCountingIcon.AntiAttack;
        }

        sequence.steps.Add(new DamageCalculationStep
        {
            icon = icon,
            iconPiece = iconPiece,
            usePieceIcon = icon == null && iconPiece != null,
            countingIcon = countingIcon,
            side = isDefense
                ? DamageStepSide.Defense
                : isAttack ? DamageStepSide.Attack : DamageStepSide.Neutral,
            displayText = displayAmount.ToString(),
            worldPosition = worldPosition,
            color = result > previousValue
                ? new Color(0.45f, 1f, 0.55f, 1f)
                : new Color(1f, 0.45f, 0.35f, 1f)
        });
    }

    /// <summary>
    /// 將指定數值與標籤加入計算步驟。
    /// </summary>
    private void AddValueStep(
        DamageCalculationSequence sequence,
        Sprite icon,
        int value,
        Vector3 worldPosition,
        Color color,
        Piece iconPiece = null
    )
    {
        if (sequence == null)
        {
            return;
        }

        sequence.steps.Add(new DamageCalculationStep
        {
            icon = icon,
            iconPiece = iconPiece,
            usePieceIcon = icon == null && iconPiece != null,
            title = string.Empty,
            detail = string.Empty,
            displayText = FormatPositiveValue(value),
            worldPosition = worldPosition,
            color = color
        });
    }

    /// <summary>
    /// 加入以棋子圖示表示的價值計算步驟。
    /// </summary>
    private void AddPieceValueStep(
        DamageCalculationSequence sequence,
        Piece iconPiece,
        int value,
        Vector3 worldPosition,
        Color color
    )
    {
        AddValueStep(
            sequence,
            null,
            value,
            worldPosition,
            color,
            iconPiece
        );
    }

    /// <summary>
    /// 依傷害標籤的既有優先順序取得數值圖示分類。
    /// </summary>
    private static DamageCountingIcon GetCountingIcon(DamageTag tags)
    {
        if ((tags & DamageTag.Cost) != 0)
        {
            return DamageCountingIcon.Cost;
        }

        if ((tags & DamageTag.Poison) != 0)
        {
            return DamageCountingIcon.Poison;
        }

        if ((tags & DamageTag.Fire) != 0)
        {
            return DamageCountingIcon.Fire;
        }

        if ((tags & DamageTag.Curse) != 0)
        {
            return DamageCountingIcon.Curse;
        }

        return DamageCountingIcon.None;
    }

    /// <summary>
    /// 將數值格式化成正值效果的顯示文字。
    /// </summary>
    private string FormatPositiveValue(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

    /// <summary>
    /// 依觸發時機補充棋子狀態的可用次數。
    /// </summary>
    private void RechargeStatuses(Piece owner, CardEffectTrigger trigger)
    {
        if (owner == null)
        {
            return;
        }

        bool rechargedAny = false;

        foreach (StatusRuntime status in owner.Statuses)
        {
            if (
                status == null ||
                status.definition == null ||
                !status.UsesCharges ||
                status.definition.rechargeTrigger != trigger
            )
            {
                continue;
            }

            int previousCharges = status.charges;
            status.Recharge(trigger);
            rechargedAny = true;

            Debug.Log(
                $"[CardDebug][StatusRecharge] Owner={Describe(owner)} | " +
                $"Status={status.definition.statusName} | Trigger={trigger} | " +
                $"Charges={previousCharges}->{status.charges}/" +
                $"{status.definition.maxCharges}"
            );
        }

        if (!rechargedAny)
        {
            Debug.Log(
                $"[CardDebug][StatusRechargeSkipped] Owner={Describe(owner)} | " +
                $"Trigger={trigger} | Reason=No rechargeable status"
            );
        }
    }

    /// <summary>
    /// 觸發棋子在指定時機可用的狀態效果。
    /// </summary>
    private void TriggerStatusEffects(Piece owner, CardEffectTrigger trigger)
    {
        if (owner == null)
        {
            return;
        }

        foreach (StatusRuntime status in owner.Statuses)
        {
            if (
                status == null ||
                status.definition == null ||
                !status.definition.HasTrigger(trigger)
            )
            {
                continue;
            }

            if (!status.HasCharges)
            {
                Debug.Log(
                    $"[CardDebug][StatusSkipped] Owner={Describe(owner)} | " +
                    $"Status={status.definition.statusName} | " +
                    $"Trigger={trigger} | Reason=No charges"
                );
                continue;
            }

            bool triggeredAny = false;

            foreach (CardEffectData effect in status.definition.effects)
            {
                if (effect == null || effect.trigger != trigger)
                {
                    continue;
                }

                triggeredAny = true;
                TriggerSingleEffect(owner, status, effect, trigger);
            }

            if (!triggeredAny)
            {
                continue;
            }

            int previousCharges = status.charges;
            status.ConsumeIfNeeded();

            if (status.UsesCharges)
            {
                Debug.Log(
                    $"[CardDebug][StatusConsumed] Owner={Describe(owner)} | " +
                    $"Status={status.definition.statusName} | " +
                    $"Trigger={trigger} | Charges={previousCharges}->" +
                    $"{status.charges}/{status.definition.maxCharges}"
                );
            }
        }
    }

    /// <summary>
    /// 觸發棋子裝備卡牌在指定時機的效果。
    /// </summary>
    private void TriggerCardEffects(Piece owner, CardEffectTrigger trigger)
    {
        if (owner == null || owner.CardRuntime == null)
        {
            return;
        }

        CardDefinition card = owner.CardRuntime.definition;
        if (card == null)
        {
            return;
        }

        bool conditionsPass = ConditionsPass(
            card,
            owner,
            logic,
            CardConditionTiming.WhileActive
        );

        Debug.Log(
            $"[CardDebug][Event] {Describe(owner)} | " +
            $"Card={card.cardName} | Trigger={trigger} | " +
            $"Conditions={(conditionsPass ? "Pass" : "Fail")}"
        );

        if (!conditionsPass)
        {
            return;
        }

        bool matchedEffect = false;

        foreach (CardEffectData effect in card.effects)
        {
            if (effect == null || effect.trigger != trigger)
            {
                continue;
            }

            matchedEffect = true;
            TriggerSingleEffect(owner, null, effect, trigger);
            owner.CardRuntime.hasTriggered = true;
        }

        if (!matchedEffect)
        {
            Debug.Log(
                $"[CardDebug][Effect] Card={card.cardName} | " +
                $"Owner={Describe(owner)} | Trigger={trigger} | " +
                "No matching effect"
            );
        }
    }

    /// <summary>
    /// 依卡牌效果與條件套用數值修正。
    /// </summary>
    private int ResolveCardNumeric(
        Piece owner,
        CardEffectTrigger trigger,
        CardEffectType effectType,
        int baseValue
    )
    {
        if (owner == null || owner.CardRuntime == null)
        {
            return baseValue;
        }

        CardDefinition card = owner.CardRuntime.definition;
        if (card == null)
        {
            return baseValue;
        }

        bool conditionsPass = ConditionsPass(
            card,
            owner,
            logic,
            CardConditionTiming.WhileActive
        );

        if (!conditionsPass)
        {
            Debug.Log(
                $"[CardDebug][Numeric] Card={card.cardName} | " +
                $"Owner={Describe(owner)} | Trigger={trigger} | " +
                $"Type={effectType} | Conditions=Fail | Result={baseValue}"
            );
            return baseValue;
        }

        float result = baseValue;
        bool matchedEffect = false;

        foreach (CardEffectData effect in card.effects)
        {
            if (
                effect == null ||
                effect.trigger != trigger ||
                effect.effectType != effectType
            )
            {
                continue;
            }

            matchedEffect = true;
            float previousValue = result;
            result = ApplyValueOperation(
                result,
                effect.operation,
                effect.value
            );
            owner.CardRuntime.hasTriggered = true;

            Debug.Log(
                $"[CardDebug][Numeric] Card={card.cardName} | " +
                $"Owner={Describe(owner)} | Trigger={trigger} | " +
                $"Type={effectType} | Target={effect.target} | " +
                $"Operation={effect.operation} | Value={effect.value} | " +
                $"{previousValue}->{result}"
            );
        }

        if (!matchedEffect)
        {
            Debug.Log(
                $"[CardDebug][Numeric] Card={card.cardName} | " +
                $"Owner={Describe(owner)} | Trigger={trigger} | " +
                $"Type={effectType} | No matching effect | Result={baseValue}"
            );
        }

        return Mathf.Max(0, Mathf.RoundToInt(result));
    }

    /// <summary>
    /// 依狀態效果與條件套用數值修正。
    /// </summary>
    private int ResolveStatusNumeric(
        Piece owner,
        CardEffectTrigger trigger,
        CardEffectType effectType,
        int baseValue
    )
    {
        if (owner == null)
        {
            return baseValue;
        }

        float result = baseValue;
        bool matchedEffect = false;

        foreach (StatusRuntime status in owner.Statuses)
        {
            if (
                status == null ||
                status.definition == null ||
                !status.HasCharges
            )
            {
                continue;
            }

            foreach (CardEffectData effect in status.definition.effects)
            {
                if (
                    effect == null ||
                    effect.trigger != trigger ||
                    effect.effectType != effectType
                )
                {
                    continue;
                }

                matchedEffect = true;
                float previousValue = result;
                result = ApplyValueOperation(
                    result,
                    effect.operation,
                    effect.value
                );

                //Debug.Log(
                //    $"[CardDebug][StatusNumeric] Owner={Describe(owner)} | " +
                //    $"Status={status.definition.statusName} | " +
                //    $"Trigger={trigger} | Type={effectType} | " +
                //    $"Target={effect.target} | Operation={effect.operation} | " +
                //    $"Value={effect.value} | {previousValue}->{result}"
                //);
            }
        }

        if (!matchedEffect)
        {
            //Debug.Log(
            //    $"[CardDebug][StatusNumeric] Owner={Describe(owner)} | " +
            //    $"Trigger={trigger} | Type={effectType} | " +
            //    $"No matching status effect | Result={baseValue}"
            //);
        }

        return Mathf.Max(0, Mathf.RoundToInt(result));
    }

    /// <summary>
    /// 執行單一效果，依效果類型處理數值、狀態或對局變化。
    /// </summary>
    private void TriggerSingleEffect(
        Piece owner,
        StatusRuntime status,
        CardEffectData effect,
        CardEffectTrigger trigger
    )
    {
        int amount = Mathf.Max(0, effect.value);
        string sourceName = status != null
            ? $"Status={status.definition.statusName}"
            : $"Card={owner.CardRuntime.definition.cardName}";

        Debug.Log(
            $"[CardDebug][Effect] {sourceName} | " +
            $"Owner={Describe(owner)} | Trigger={trigger} | " +
            $"Type={effect.effectType} | Target={effect.target} | " +
            $"Operation={effect.operation} | Value={effect.value}"
        );

        CardDefinition animationCard =
            status != null && status.hasSourcePlayer
                ? null
                : GetAnimationCard(owner, status);
        Sprite visualIcon = status != null && status.definition != null
            ? status.definition.icon
            : null;
        bool waitsForRandomTarget =
            effect.effectType == CardEffectType.DamagePlayer &&
            effect.target == CardEffectTarget.RandomEnemyPiece;

        if (!waitsForRandomTarget)
        {
            CardAnimationEvents.Play(
                owner,
                owner,
                animationCard,
                CardAnimationTiming.OnEffectTriggered,
                effect,
                amount
            );
        }

        if (effect.effectType == CardEffectType.HealPlayer)
        {
            int finalHealAmount = ResolveHealAmount(owner.IsWhite, amount);

            Debug.Log(
                $"[CardDebug][Target] {sourceName} | " +
                $"Target={(owner.IsWhite ? "White Player" : "Black Player")} | " +
                $"Heal={amount}->{finalHealAmount}"
            );
            DamageCalculationSequence sequence =
                BuildHealSequence(
                    owner,
                    owner.IsWhite,
                    amount,
                    finalHealAmount,
                    visualIcon
                );

            logic.PlayDamageCalculation(
                sequence,
                () =>
                {
                    CardAnimationEvents.Play(
                        owner,
                        owner,
                        animationCard,
                        CardAnimationTiming.OnHealed,
                        effect,
                        amount
                    );
                },
                () => HealPlayer(owner.IsWhite, amount)
            );
        }
        else if (
            effect.effectType == CardEffectType.DamagePlayer &&
            effect.target == CardEffectTarget.RandomEnemyPiece
        )
        {
            Piece target = GetRandomPiece(!owner.IsWhite);

            if (target != null)
            {
                Debug.Log(
                    $"[CardDebug][Target] {sourceName} | " +
                    $"RandomTarget={Describe(target)}"
                );
                CardAnimationEvents.Play(
                    owner,
                    target,
                    animationCard,
                    CardAnimationTiming.OnEffectTriggered,
                    effect,
                    amount
                );
                DealDamageToPiece(
                    owner,
                    target,
                    amount,
                    trigger,
                    animationCard,
                    visualIcon,
                    effect,
                    DamageTag.None,
                    status
                );
            }
            else
            {
                Debug.Log(
                    $"[CardDebug][Target] {sourceName} | " +
                    "No valid enemy piece found"
                );
            }
        }
        else if (
            effect.effectType == CardEffectType.DamagePlayer &&
            effect.target == CardEffectTarget.OwnerPlayer
        )
        {
            Piece damageSource = status != null ? status.source : owner;
            CardDefinition damageAnimationCard =
                status != null && status.hasSourcePlayer
                    ? null
                    : animationCard;

            Debug.Log(
                $"[CardDebug][Target] {sourceName} | " +
                $"Target={Describe(owner)} | Damage={amount}"
            );
            DealDamageToPiece(
                damageSource,
                owner,
                amount,
                trigger,
                damageAnimationCard,
                visualIcon,
                effect,
                DamageTag.None,
                status
            );
        }
    }

    /// <summary>
    /// 依目前棋盤與玩家狀態判斷單一效果條件。
    /// </summary>
    private static bool EvaluateCondition(
        CardConditionType condition,
        Piece owner,
        LogicManager logicManager
    )
    {
        switch (condition)
        {
            case CardConditionType.OwnerHasNotMoved:
                return owner.HasMoved == 0;

            case CardConditionType.OwnerHasMoved:
                return owner.HasMoved > 0;

            case CardConditionType.OwnerPlayerHealthFull:
                if (logicManager == null)
                {
                    return false;
                }

                return owner.IsWhite
                    ? logicManager.whiteHealth >= LogicManager.MaxHealth
                    : logicManager.blackHealth >= LogicManager.MaxHealth;

            case CardConditionType.OwnerHasNoCard:
                return owner.cardDefinition == null;

            default:
                return true;
        }
    }

    /// <summary>
    /// 依加算、指定或乘算方式套用數值效果。
    /// </summary>
    private static float ApplyValueOperation(
        float currentValue,
        CardValueOperation operation,
        int modifier
    )
    {
        return CardNumericRules.Apply(currentValue, operation, modifier);
    }

    /// <summary>
    /// 選取目前效果對應的卡牌，供動畫資源查詢使用。
    /// </summary>
    private static CardDefinition GetAnimationCard(
        Piece owner,
        StatusRuntime status
    )
    {
        if (
            status != null &&
            status.fromCard &&
            status.source != null &&
            status.source.CardRuntime != null
        )
        {
            return status.source.CardRuntime.definition;
        }

        if (owner != null && owner.CardRuntime != null)
        {
            return owner.CardRuntime.definition;
        }

        return null;
    }

    /// <summary>
    /// 產生物件的辨識文字，供紀錄或偵錯訊息使用。
    /// </summary>
    private static string Describe(Piece piece)
    {
        if (piece == null)
        {
            return "None";
        }

        Vector2 coordinates = piece.GetCoordinates();
        string side = piece.IsWhite ? "White" : "Black";
        string type = string.IsNullOrEmpty(piece.PieceType)
            ? piece.GetType().Name
            : piece.PieceType;

        return $"{side} {type} ({coordinates.x:0},{coordinates.y:0})";
    }
}
