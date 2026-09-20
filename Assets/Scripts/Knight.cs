using System.Collections.Generic;
using UnityEngine;

//==============================
// 騎士（Knight）
// 唯一可以跳過棋子的棋子
// 走法為「日」字型
//==============================
public class Knight : Piece
{
    //==============================
    // 騎士所有可能位移
    //==============================
    private static readonly Vector2[] MoveOffsets =
        new Vector2[]
    {
        // 右方向
        new Vector2(2, 1),
        new Vector2(2, -1),

        // 左方向
        new Vector2(-2, 1),
        new Vector2(-2, -1),

        // 上方向
        new Vector2(1, 2),
        new Vector2(1, -2),

        // 下方向
        new Vector2(-1, 2),
        new Vector2(-1, -2)
    };

    //==============================
    // 取得合法移動位置
    //==============================
    /// <summary>
    /// 依棋子的基本走法列出候選目的地；王受將軍的限制由合法走法檢查處理。
    /// </summary>
    protected override List<Vector2> GetPotentialMoves()
    {
        // 合法移動列表
        List<Vector2> legalMoves =
            new List<Vector2>();

        // 目前座標
        Vector2 currentCoordinates =
            GetCoordinates();

        //==============================
        // 檢查所有「日」字移動
        //==============================
        foreach (Vector2 offset in MoveOffsets)
        {
            // 計算目標位置
            Vector2 targetPosition =
                currentCoordinates + offset;

            //==============================
            // 是否在棋盤內
            //==============================
            if (IsPositionWithinBoard(targetPosition))
            {
                // 取得目標格棋子
                Piece pieceAtTarget =
                    logicManager.boardMap[
                        (int)targetPosition.x,
                        (int)targetPosition.y
                    ];

                //==============================
                // 空格 → 可移動
                // 敵方棋子 → 可吃
                //==============================
                if (
                    pieceAtTarget == null
                    ||
                    pieceAtTarget.IsWhite != IsWhite
                )
                {
                    legalMoves.Add(targetPosition);
                }
            }
        }

        return legalMoves;
    }

    //==============================
    // 取得攻擊範圍
    // 用於將軍判定
    //==============================
    /// <summary>
    /// 列出棋子攻擊的格子，供將軍與王車易位判定使用；攻擊線保留第一個阻擋格。
    /// </summary>
    public override List<Vector2> GetAttackedFields()
    {

        if (UsesDefinitionRules)
        {
            return GetDefinitionAttackedFields();
        }

// 攻擊範圍列表
        List<Vector2> attackedFields =
            new List<Vector2>();

        // 目前位置
        Vector2 currentCoordinates =
            GetCoordinates();

        //==============================
        // 檢查所有攻擊位置
        //==============================
        foreach (Vector2 offset in MoveOffsets)
        {
            // 計算攻擊位置
            Vector2 targetPosition =
                currentCoordinates + offset;

            //==============================
            // 在棋盤內才加入
            //==============================
            if (IsPositionWithinBoard(targetPosition))
            {
                attackedFields.Add(targetPosition);
            }
        }

        return attackedFields;
    }
}