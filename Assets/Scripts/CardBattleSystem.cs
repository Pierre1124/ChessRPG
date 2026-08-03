using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ?葉蝞∠??∠??????圈洛蝯???/// 銋?閬??∠????uff?ebuff????摰喉??芸???研?/// </summary>
public class CardBattleSystem
{
    private readonly LogicManager logic;

    public CardBattleSystem(LogicManager logicManager)
    {
        logic = logicManager;
    }

    /// <summary>
    /// ?∠????璇辣?文???    /// CardDefinition ??恍ㄐ嚗?璇辣?摩銝???亦? Resolver??    /// </summary>
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
    /// 璉?蝘餃?敺??∠??亙??    /// ?桀??其???擳?撣怒??瑁??撌梁宏?????賬????    /// </summary>
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
    /// ?啣???憪?嚗???摰嗆???摮?????∠?????    /// </summary>
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
    /// Resolves end-of-turn effects for the side that just completed its turn,
    /// then advances finite statuses owned by that side.
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
    /// Sends one castling event after both the king and rook have moved.
    /// Each participant can own a card or status that reacts to castling.
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
    /// ???瑕拿嚗◤??摮? Value + ?餅?????ATK嚗?憟鋡怠??寧??靽格迤??    /// </summary>
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
    /// ?∠?????摰???摮??瑟?雿輻??    /// ?桀??瑕拿隞閰脫?摮?撅祉摰?HP??    /// </summary>
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

    //?冽??訾葉?格?
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
    /// 璉??桀????餅???= ?芾澈 ATK + ??? ATK??    /// </summary>
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

    public int GetEffectiveValue(Piece piece)
    {
        return ResolveEffectiveValue(piece, null);
    }

    private int GetEffectiveValueWithSteps(
        Piece piece,
        DamageCalculationSequence sequence
    )
    {
        return ResolveEffectiveValue(piece, sequence);
    }

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

    private string FormatPositiveValue(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

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

    private static float ApplyValueOperation(
        float currentValue,
        CardValueOperation operation,
        int modifier
    )
    {
        switch (operation)
        {
            case CardValueOperation.Set:
                return modifier;

            case CardValueOperation.Multiply:
                return currentValue * modifier;

            default:
                return currentValue + modifier;
        }
    }

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
