using System.Collections.Generic;
using UnityEngine;

//==============================
// 國王（King）
// 西洋棋最重要的棋子
// 功能：
// ? 一格移動
// ? 王車易位
// ? 將軍判定
//==============================
public class King : Piece
{
    //==============================
    // 取得合法移動
    //==============================
    public override List<Vector2> GetLegalMoves()
    {
        
        if (UsesDefinitionRules)
        {
            return base.GetLegalMoves();
        }

// 先取得一般合法移動
        List<Vector2> legalMoves =
            base.GetLegalMoves();

        //==============================
        // 王車易位判定
        //==============================
        // 必須：
        // ? King沒移動過
        // ? 目前沒被將軍
        if (
            HasMoved == 0 &&
            !logicManager.CheckKingStatus()
        )
        {
            //==============================
            // 短易位（KingSide）
            //==============================
            if (CanCastleKingside())
            {
                legalMoves.Add(
                    new Vector2(
                        6,
                        GetCoordinates().y
                    )
                );
            }

            //==============================
            // 長易位（QueenSide）
            //==============================
            if (CanCastleQueenside())
            {
                legalMoves.Add(
                    new Vector2(
                        2,
                        GetCoordinates().y
                    )
                );
            }
        }

        return logicManager.ApplyBoardFieldEffects(this, legalMoves);
    }

    //==============================
    // 是否可短易位（右側）
    //==============================
    private bool CanCastleKingside()
    {
        // 國王所在列
        int y = (int)GetCoordinates().y;

        // 右側城堡
        Piece rook =
            logicManager.boardMap[7, y];

        //==============================
        // 必須是未移動過的城堡
        //==============================
        if (
            rook is Rook &&
            rook.HasMoved == 0
        )
        {
            // 對方攻擊地圖
            bool[,] checkMap =
                IsWhite
                ?
                logicManager.blackCheckMap
                :
                logicManager.whiteCheckMap;

            //==============================
            // 條件：
            // ? 中間沒棋子
            // ? 經過格沒被攻擊
            //==============================
            bool canCastle =
                logicManager.boardMap[5, y] == null &&
                logicManager.boardMap[6, y] == null &&
                !checkMap[5, y] &&
                !checkMap[6, y];

            return canCastle;
        }

        return false;
    }

    //==============================
    // 是否可長易位（左側）
    //==============================
    private bool CanCastleQueenside()
    {
        int y = (int)GetCoordinates().y;

        // 左側城堡
        Piece rook =
            logicManager.boardMap[0, y];

        //==============================
        // 必須是未移動過的城堡
        //==============================
        if (
            rook is Rook &&
            rook.HasMoved == 0
        )
        {
            // 對方攻擊地圖
            bool[,] checkMap =
                IsWhite
                ?
                logicManager.blackCheckMap
                :
                logicManager.whiteCheckMap;

            //==============================
            // 條件：
            // ? 中間沒棋子
            // ? 經過格沒被攻擊
            //==============================
            bool canCastle =
                logicManager.boardMap[1, y] == null &&
                logicManager.boardMap[2, y] == null &&
                logicManager.boardMap[3, y] == null &&
                !checkMap[2, y] &&
                !checkMap[3, y];

            return canCastle;
        }

        return false;
    }

    //==============================
    // 移動國王
    //==============================
    public override void Move(Vector2 newPosition)
    {
        
        if (UsesDefinitionRules)
        {
            base.Move(newPosition);
            return;
        }

// 目前位置
        Vector2 currentPosition =
            GetCoordinates();
        Rook castlingRook = null;

        //==============================
        // 王車易位判定
        //==============================
        // King一次移動兩格
        if (
            HasMoved == 0 &&
            Mathf.Abs(
                newPosition.x -
                currentPosition.x
            ) == 2
        )
        {
            int y = (int)currentPosition.y;

            //==============================
            // 短易位
            //==============================
            if (newPosition.x == 6)
            {
                if (logicManager.boardMap[7, y] is Rook rook)
                {
                    castlingRook = rook;
                    // 城堡移動到F格
                    rook.Move(new Vector2(5, y));
                }
            }

            //==============================
            // 長易位
            //==============================
            else if (newPosition.x == 2)
            {
                if (logicManager.boardMap[0, y] is Rook rook)
                {
                    castlingRook = rook;
                    // 城堡移動到D格
                    rook.Move(new Vector2(3, y));
                }
            }
        }

        //==============================
        // 執行King移動
        //==============================
        base.Move(newPosition);

        if (castlingRook != null)
        {
            logicManager.OnPiecesCastled(this, castlingRook);
        }
    }

    //==============================
    // 檢查國王是否被將軍
    //==============================
    public bool CheckForChecks()
    {
        // 目前座標
        Vector2 currentCoordinates =
            GetCoordinates();

        // 對方攻擊範圍
        bool[,] checkMap =
            IsWhite
            ?
            logicManager.blackCheckMap
            :
            logicManager.whiteCheckMap;

        //==============================
        // 是否被攻擊
        //==============================
        bool isInCheck =
            checkMap[
                (int)currentCoordinates.x,
                (int)currentCoordinates.y
            ];

        // Debug用
        // Debug.Log($"Checking King: {isInCheck}");

        return isInCheck;
    }

    //==============================
    // 取得理論可移動位置
    //==============================
    protected override List<Vector2> GetPotentialMoves()
    {
        // 合法移動列表
        List<Vector2> legalMoves =
            new List<Vector2>();

        //==============================
        // 八方向
        //==============================
        int[] directionsX =
        {
            1, -1, 0, 0,
            1, -1, 1, -1
        };

        int[] directionsY =
        {
            0, 0, 1, -1,
            1, -1, -1, 1
        };

        // 目前位置
        Vector2 currentCoordinates =
            GetCoordinates();

        //==============================
        // 檢查八方向
        //==============================
        for (int i = 0; i < 8; i++)
        {
            // 新位置
            Vector2 newPosition =
                new Vector2(
                    currentCoordinates.x +
                    directionsX[i],

                    currentCoordinates.y +
                    directionsY[i]
                );

            // 超出棋盤
            if (!IsPositionWithinBoard(newPosition))
                continue;

            // 該格棋子
            Piece pieceAtNewPosition =
                logicManager.boardMap[
                    (int)newPosition.x,
                    (int)newPosition.y
                ];

            //==============================
            // 空格 或 敵人
            //==============================
            if (
                pieceAtNewPosition == null
                ||
                pieceAtNewPosition.IsWhite != IsWhite
            )
            {
                legalMoves.Add(newPosition);
            }
        }

        return legalMoves;
    }

    //==============================
    // 取得攻擊範圍
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
        // 八方向
        //==============================
        int[] directionsX =
        {
            1, -1, 0, 0,
            1, -1, 1, -1
        };

        int[] directionsY =
        {
            0, 0, 1, -1,
            1, -1, -1, 1
        };

        // 目前位置
        Vector2 currentCoordinates =
            GetCoordinates();

        //==============================
        // 檢查八方向
        //==============================
        for (int i = 0; i < 8; i++)
        {
            // 攻擊位置
            Vector2 attackedPosition =
                new Vector2(
                    currentCoordinates.x +
                    directionsX[i],

                    currentCoordinates.y +
                    directionsY[i]
                );

            // 在棋盤內才加入
            if (IsPositionWithinBoard(attackedPosition))
            {
                attackedFields.Add(attackedPosition);
            }
        }

        return attackedFields;
    }
}
