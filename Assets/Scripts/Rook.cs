using System.Collections.Generic;
using UnityEngine;

//==============================
// 城堡（Rook）
// 水平 / 垂直無限移動
//==============================
public class Rook : Piece
{
    //==============================
    // 取得合法移動位置
    //==============================
    protected override List<Vector2> GetPotentialMoves()
    {
        // 合法移動列表
        List<Vector2> legalMoves =
            new List<Vector2>();

        //==============================
        // 四個方向
        // 右、左、上、下
        //==============================
        int[] directionsX = { 1, -1, 0, 0 };
        int[] directionsY = { 0, 0, 1, -1 };

        // 目前座標
        Vector2 currentCoordinates =
            GetCoordinates();

        //==============================
        // 檢查四個方向
        //==============================
        for (int i = 0; i < 4; i++)
        {
            // 往外延伸距離
            int step = 1;

            while (true)
            {
                //==============================
                // 計算新位置
                //==============================
                Vector2 newPosition =
                    new Vector2(
                        currentCoordinates.x +
                        step * directionsX[i],

                        currentCoordinates.y +
                        step * directionsY[i]
                    );

                //==============================
                // 超出棋盤就停止
                //==============================
                if (!IsPositionWithinBoard(newPosition))
                    break;

                // 該位置上的棋子
                Piece pieceAtNewPosition =
                    logicManager.boardMap[
                        (int)newPosition.x,
                        (int)newPosition.y
                    ];

                //==============================
                // 空格 → 可移動
                //==============================
                if (pieceAtNewPosition == null)
                {
                    legalMoves.Add(newPosition);
                }
                else
                {
                    //==============================
                    // 敵方棋子 → 可吃
                    //==============================
                    if (
                        pieceAtNewPosition.IsWhite
                        != IsWhite
                    )
                    {
                        legalMoves.Add(newPosition);
                    }

                    //==============================
                    // 被棋子擋住
                    // 不可繼續往前
                    //==============================
                    break;
                }

                // 繼續往前延伸
                step++;
            }
        }

        return legalMoves;
    }

    //==============================
    // 取得攻擊範圍
    // 用於將軍判定
    //==============================
    public override List<Vector2> GetAttackedFields()
    {
        
        if (UsesDefinitionRules)
        {
            return GetDefinitionAttackedFields();
        }

// 攻擊範圍列表
        List<Vector2> attackedFields =
            new List<Vector2>();

        //==============================
        // 四個方向
        //==============================
        int[] directionsX = { 1, -1, 0, 0 };
        int[] directionsY = { 0, 0, 1, -1 };

        // 目前座標
        Vector2 currentCoordinates =
            GetCoordinates();

        //==============================
        // 檢查四個方向
        //==============================
        for (int i = 0; i < 4; i++)
        {
            int step = 1;

            while (true)
            {
                //==============================
                // 計算攻擊位置
                //==============================
                Vector2 attackedPosition =
                    new Vector2(
                        currentCoordinates.x +
                        step * directionsX[i],

                        currentCoordinates.y +
                        step * directionsY[i]
                    );

                //==============================
                // 超出棋盤
                //==============================
                if (
                    !IsPositionWithinBoard(
                        attackedPosition
                    )
                )
                    break;

                // 加入攻擊範圍
                attackedFields.Add(attackedPosition);

                //==============================
                // 如果有棋子阻擋
                // 攻擊線停止
                //==============================
                Piece pieceAtAttackedPosition =
                    logicManager.boardMap[
                        (int)attackedPosition.x,
                        (int)attackedPosition.y
                    ];

                if (pieceAtAttackedPosition != null)
                    break;

                // 繼續往外延伸
                step++;
            }
        }

        return attackedFields;
    }
}