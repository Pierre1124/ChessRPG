using UnityEngine;

/// <summary>棋盤指示物只提供查看資訊，不成為可移動、吃子或出牌的目標。</summary>
public sealed class BoardFieldTokenInfo : MonoBehaviour
{
    public BoardFieldEffectType type;
    public Vector2Int cell;
    public CardKeywordEntry Entry
    {
        get
        {
            string key = type == BoardFieldEffectType.Moat ? "kw:moat-token" :
                type == BoardFieldEffectType.ElectricNet ? "kw:net-token" : null;
            return CardKeywordGlossary.Formatter.TryGet(key, out var entry) ? entry : null;
        }
    }
}
