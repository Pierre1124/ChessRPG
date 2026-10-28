using UnityEngine;

/// <summary>以實際傷害增減判斷呈現；易傷雖屬承傷階段，仍播放傷害上升。</summary>
[CreateAssetMenu(menuName = "Chess RPG/Damage Modifier Feedback")]
public sealed class DamageModifierFeedback : ScriptableObject
{
    public Sprite increaseIcon;
    public Sprite decreaseIcon;
    public Sprite defenseIcon;
    public Sprite arrowUp;
    public Sprite arrowDown;
    public AudioClip increaseSound;
    public AudioClip decreaseSound;
    [Range(0, 1)] public float volume = 1f;
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
        IsDefense(step) ? sequence.target : sequence.attacker != null ? sequence.attacker : sequence.damageContext?.source;
    public Sprite Symbol(DamageCalculationStep step, DamageCalculationSequence sequence)
    {
        if (IsDefense(step)) return defenseIcon;
        var type = BattlePresentationStyle.Resolve(sequence);
        return type != BattlePresentationType.Neutral ? BattleFeedback.GetStyle(type)?.icon ?? increaseIcon : increaseIcon;
    }
    public AudioClip Sound(DamageCalculationStep step) => PointsUp(step) ? increaseSound : decreaseSound;
    public void PlaySound(DamageCalculationStep step) => BattleFeedback.PlayClip(Sound(step), volume);
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
            if (card.cardType != CardType.JobChange) status.idleEffectPrefab = null;
            status.triggerSound = null;
        }
    }
}
