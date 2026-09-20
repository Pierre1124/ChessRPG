using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主教沿四個對角線方向移動。
/// </summary>
public class Bishop : Piece
{
    // 保留原本的方向順序；共用唯讀陣列避免每次查詢走法時重建。
    private static readonly int[] RayDirectionsX = { 1, 1, -1, -1 };
    private static readonly int[] RayDirectionsY = { 1, -1, 1, -1 };

    /// <summary>
    /// 依棋子的基本走法列出候選目的地；王受將軍的限制由合法走法檢查處理。
    /// </summary>
    protected override List<Vector2> GetPotentialMoves()
    {
        return CollectRayFields(RayDirectionsX, RayDirectionsY, false);
    }

    /// <summary>
    /// 列出棋子攻擊的格子，供將軍與王車易位判定使用；攻擊線保留第一個阻擋格。
    /// </summary>
    public override List<Vector2> GetAttackedFields()
    {
        if (UsesDefinitionRules)
        {
            return GetDefinitionAttackedFields();
        }

        return CollectRayFields(RayDirectionsX, RayDirectionsY, true);
    }
}
