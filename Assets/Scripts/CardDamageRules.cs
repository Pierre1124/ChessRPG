/// <summary>定義 Excel 屬性矩陣，讓一般修正與屬性加成各自判斷。</summary>
public static class CardDamageRules
{
    /// <summary>真傷及代價排除全部一般與屬性修正。</summary>
    public static bool IsUnmodified(DamageTag tags) => (tags & (DamageTag.True | DamageTag.Cost)) != 0;

    /// <summary>電、毒、詛咒不接受一般增傷及弱化。</summary>
    public static bool AllowsOutgoing(DamageTag tags) => !IsUnmodified(tags) &&
        (tags & (DamageTag.Electric | DamageTag.Poison | DamageTag.Curse)) == 0;

    /// <summary>判斷數值效果是否符合本次傷害屬性的修正限制。</summary>
    public static bool AllowsEffect(DamageTag tags, CardEffectData effect)
    {
        if (effect.effectType == CardEffectType.ModifyDamageDealt)
            return effect.operation == CardValueOperation.Set || AllowsOutgoing(tags);
        if (effect.effectType == CardEffectType.ModifyDamageTaken || effect.effectType == CardEffectType.ModifyOwnerPlayerDamage)
            return !IsUnmodified(tags);
        return true;
    }
}
