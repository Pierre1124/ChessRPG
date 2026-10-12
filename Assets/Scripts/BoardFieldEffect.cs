using System.Collections.Generic;
using UnityEngine;

public enum BoardFieldEffectType
{
    None,
    Moat,
    ElectricNet
}

public class BoardFieldEffectZone
{
    public BoardFieldEffectType type;
    public Piece firstSource;
    public Piece secondSource;
    public List<Vector2Int> cells = new List<Vector2Int>();

    /// <summary>
    /// 判斷指定棋盤座標是否位於此場地效果區域。
    /// </summary>
    public bool Contains(Vector2Int cell)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] == cell)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>移動前保存 Token 與卡牌來源，避免吃掉電網端點後遺失穿越資訊。</summary>
public sealed class ElectricNetCrossing
{
    public Vector2Int cell;
    public CardDefinition card;

    /// <summary>列出實際進入的電網格；起點不重算，跳躍只算落點，同格不重複觸發。</summary>
    public static List<ElectricNetCrossing> Collect(Vector2Int start, Vector2Int end, bool jumping,
        List<BoardFieldEffectZone> zones)
    {
        var hits = new List<ElectricNetCrossing>();
        if (start == end || zones == null) return hits;
        Vector2Int delta = end - start;
        bool linear = delta.x == 0 || delta.y == 0 || Mathf.Abs(delta.x) == Mathf.Abs(delta.y);
        int length = jumping || !linear ? 1 : Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
        Vector2Int direction = new Vector2Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
        for (int i = 1; i <= length; i++)
        {
            Vector2Int cell = jumping || !linear ? end : start + direction * i;
            foreach (var zone in zones)
            {
                if (zone.type != BoardFieldEffectType.ElectricNet || !zone.Contains(cell)) continue;
                hits.Add(new ElectricNetCrossing { cell = cell, card = zone.firstSource != null ? zone.firstSource.cardDefinition : null });
                break;
            }
        }
        return hits;
    }
}
