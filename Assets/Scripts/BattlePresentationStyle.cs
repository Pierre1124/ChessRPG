using System;
using TMPro;
using UnityEngine;

public enum BattlePresentationType { Neutral, Fire, Poison, Curse, Electric, TrueDamage, Cost, Heal, AntiHeal, Trigger }

/// <summary>每種戰鬥事件共用的數字外觀、圖示及觸發素材。</summary>
[Serializable]
public sealed class BattlePresentationStyle
{
    public BattlePresentationType type;
    public Sprite icon;
    public Color numberColor = Color.white;
    public FontStyles fontStyle = FontStyles.Bold;
    [Range(.5f, 2f)] public float numberScale = 1f;
    public GameObject effectPrefab;
    public Vector3 effectOffset = new Vector3(0, 1, 0);
    [Min(.05f)] public float effectLifetime = 2f;
    public AudioClip sound;
    [Range(0, 1)] public float soundVolume = 1f;

    /// <summary>分類只影響呈現，不修改傷害或屬性規則。</summary>
    public static BattlePresentationType Resolve(DamageCalculationSequence sequence)
    {
        if (sequence.isHealing)
        {
            if (sequence.finalDamage == 0)
                foreach (var step in sequence.steps)
                    if (step.countingIcon == DamageCountingIcon.AntiHeal) return BattlePresentationType.AntiHeal;
            return BattlePresentationType.Heal;
        }
        DamageTag tags = sequence.damageContext != null ? sequence.damageContext.tags : sequence.presentationTags;
        if ((tags & DamageTag.Cost) != 0) return BattlePresentationType.Cost;
        if ((tags & DamageTag.True) != 0) return BattlePresentationType.TrueDamage;
        if ((tags & DamageTag.Electric) != 0) return BattlePresentationType.Electric;
        if ((tags & DamageTag.Fire) != 0) return BattlePresentationType.Fire;
        if ((tags & DamageTag.Poison) != 0) return BattlePresentationType.Poison;
        if ((tags & DamageTag.Curse) != 0) return BattlePresentationType.Curse;
        return BattlePresentationType.Neutral;
    }
}
