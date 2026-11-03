using UnityEngine;

public enum DamageCuePhase { AttackSources, AttackEffects, DefenseSources, DefenseEffects }

/// <summary>依來源、攻守角色與箭頭方向呈現修正，不改變傷害計算。</summary>
[CreateAssetMenu(menuName = "Chess RPG/Damage Modifier Feedback")]
public sealed class DamageModifierFeedback : ScriptableObject
{
    public Sprite increaseIcon;
    public Sprite decreaseIcon;
    public Sprite defenseIcon;
    public Sprite arrowUp;
    public Sprite arrowDown;
    private static DamageModifierFeedback cached;
    public static DamageModifierFeedback Settings => cached != null ? cached :
        (cached = Resources.Load<DamageModifierFeedback>("DamageModifierFeedback"));

    public static int Direction(DamageCalculationStep step, bool healing)
    {
        if (healing || step == null || !step.hasContribution ||
            (step.modifierPhase != DamageModifierPhase.Increase && step.modifierPhase != DamageModifierPhase.Decrease)) return 0;
        return System.Math.Sign(step.contribution);
    }
    public Sprite Icon(int direction) => direction > 0 ? increaseIcon : decreaseIcon;
    public static bool IsDefense(DamageCalculationStep step) => step.side == DamageStepSide.Defense || step.countingIcon == DamageCountingIcon.Defense;
    public static bool PointsUp(DamageCalculationStep step) => IsDefense(step) ? step.contribution < 0 : step.contribution > 0;
    public static Piece Recipient(DamageCalculationStep step, DamageCalculationSequence sequence) =>
        step.iconPiece != null ? step.iconPiece : IsDefense(step) ? sequence.target : sequence.attacker != null ? sequence.attacker : sequence.damageContext?.source;
    public static Piece ActivationSource(DamageContext context) => context.sourceStatus?.owner != null ? context.sourceStatus.owner : context.source;
    public static bool IsTargetPresentation(DamageCalculationStep step, DamageCalculationSequence sequence) =>
        IsDefense(step) || (sequence.presentationOnly && step.modifierPhase != DamageModifierPhase.Activation);
    public Sprite Symbol(DamageCalculationStep step, DamageCalculationSequence sequence) => StyleSymbol(step);
    public static Sprite StyleSymbol(DamageCalculationStep step) => BattleFeedback.GetStyle(IsDefense(step) ? BattlePresentationType.Defense : SymbolType(step))?.icon;
    public static BattlePresentationType SymbolType(DamageCalculationStep step) =>
        BattlePresentationStyle.Resolve(new DamageCalculationSequence { presentationTags = step.modifierTags });
    public static BattlePresentationType PresentationType(DamageCalculationStep step) => PointsUp(step) ? BattlePresentationType.ModifierUp : BattlePresentationType.ModifierDown;
    public static float Play(DamageCalculationStep step, Vector3 position, bool playSound)
    {
        var type = PresentationType(step);
        BattleFeedback.PlayType(type, position, playSound);
        var style = BattleFeedback.GetStyle(type);
        return style != null && style.effectPrefab != null ? style.PlaybackLifetime : 0f;
    }
    public static bool BelongsToPhase(DamageCalculationStep step, DamageCalculationSequence sequence, DamageCuePhase phase)
    {
        if (step == null || step.modifierPhase == DamageModifierPhase.None) return false;
        if (IsDefense(step) != (phase == DamageCuePhase.DefenseSources || phase == DamageCuePhase.DefenseEffects)) return false;
        if (phase == DamageCuePhase.AttackEffects || phase == DamageCuePhase.DefenseEffects)
            return Direction(step, sequence.isHealing) != 0;
        return !sequence.presentationOnly || step.modifierPhase == DamageModifierPhase.Activation ||
            !sequence.steps.Exists(other => other != null && other.modifierPhase == DamageModifierPhase.Activation && other.sourceCardId == step.sourceCardId);
    }
}

public static class CardPresentationPolicy
{
    public static void RemoveIndividualEffects(CardDefinition card)
    {
        if (card == null || card.cardType == CardType.Field) return;
        card.skillEffectPrefab = null;
        card.skillSound = null;
        foreach (var animation in card.animations)
        {
            if (animation == null) continue;
            animation.effectPrefab = null; animation.sound = null;
        }
        foreach (var status in card.statusesToApply)
        {
            if (status == null) continue;
            status.triggerEffectPrefab = null;
            status.triggerSound = null;
        }
    }
}
