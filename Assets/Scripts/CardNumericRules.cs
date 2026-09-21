/// <summary>統一處理卡牌數值運算，讓戰鬥流程與數學規則可分別驗證。</summary>
public static class CardNumericRules
{
    /// <summary>保持原本加算、指定、乘算的順序與浮點精度。</summary>
    public static float Apply(float currentValue, CardValueOperation operation, int modifier)
    {
        switch (operation)
        {
            case CardValueOperation.Set: return modifier;
            case CardValueOperation.Multiply: return currentValue * modifier;
            default: return currentValue + modifier;
        }
    }
}
