using UnityEngine;

/// <summary>以實際傷害增減判斷呈現；易傷雖屬承傷階段，仍播放傷害上升。</summary>
[CreateAssetMenu(menuName = "Chess RPG/Damage Modifier Feedback")]
public sealed class DamageModifierFeedback : ScriptableObject
{
    public Sprite increaseIcon;
    public Sprite decreaseIcon;
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
    public void PlaySound(int direction) => BattleFeedback.PlayClip(direction > 0 ? increaseSound : decreaseSound, volume);
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
