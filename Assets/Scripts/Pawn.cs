using System.Collections.Generic;
using UnityEngine;

//==============================
// ???Pawn??
// ?撖??秋????叟垓??????
// ? ???
// ? ???
// ? ????謕?
// ? ?????
// ? ???
//==============================
public class Pawn : Piece
{
    //==============================
    // ??????
    //==============================
    public override void Move(Vector2 newPosition)
    {
        
        if (UsesDefinitionRules)
        {
            base.Move(newPosition);

            if (ShouldPromoteAt(newPosition))
            {
                logicManager.HandlePromotion(this);
            }

            return;
        }

//==============================
        // ????????
        //==============================
        // ?????剖??????
        // ?選?謆?謕僱?蝞?
        // ?????迎???????
        if (
            Mathf.Abs(newPosition.x - GetCoordinates().x) == 1 &&
            Mathf.Abs(newPosition.y - GetCoordinates().y) == 1
        )
        {
            // ?謘??⊥???
            Piece capturedPiece =
                logicManager.boardMap[
                    (int)newPosition.x,
                    (int)GetCoordinates().y
                ];

            //==============================
            // ?????Pawn
            // ??謢???豯??謜???? Pawn
            //==============================
            if (
                capturedPiece is Pawn &&
                capturedPiece == logicManager.lastMovedPiece
            )
            {
                
                logicManager.OnPieceCaptured(this, capturedPiece);
// ?謒?????
                logicManager.boardMap[
                    (int)newPosition.x,
                    (int)GetCoordinates().y
                ] = null;

            }
        }

        //==============================
        // ?????蟡???
        //==============================
        base.Move(newPosition);

        //==============================
        // ??????
        //==============================
        // ?鞈ｇ??斯8??
        // ?綜???斯1??
        if (ShouldPromoteAt(newPosition))
        {
            // ?瞉???UI
            logicManager.HandlePromotion(this);
        }
    }
    private bool ShouldPromoteAt(Vector2 position)
    {
        bool reachesPromotionRank =
            (IsWhite && position.y == 7) ||
            (!IsWhite && position.y == 0);

        if (!reachesPromotionRank)
        {
            return false;
        }

        if (cardDefinition == null)
        {
            return true;
        }

        return cardDefinition.CanPromote(this);
    }

    //==============================
    // ?謘????????????
    //==============================
    protected override List<Vector2> GetPotentialMoves()
    {
        // ???????謅?
        List<Vector2> legalMoves =
            new List<Vector2>();

        // ?獢??冽?
        Vector2 currentCoordinates =
            GetCoordinates();

        //==============================
        // ?鞈??甄??
        // ?綜??甄??
        //==============================
        int direction = IsWhite ? 1 : -1;

        //==============================
        // ????????
        //==============================
        if (logicManager.lastMovedPiece is Pawn lastMovedPawn)
        {
            // ???漸蝯脫?
            Vector2 lastMoveStart =
                logicManager.lastMovedPieceStartPosition;

            // ???漲???
            Vector2 lastMoveEnd =
                logicManager.lastMovedPieceEndPosition;

            //==============================
            // ?????謘?????蝎??僱
            //==============================
            if (
                Mathf.Abs(
                    lastMoveStart.y -
                    lastMoveEnd.y
                ) == 2
            )
            {
                Vector2 ourPosition =
                    GetCoordinates();

                //==============================
                // ????????
                //==============================
                if (
                    Mathf.Abs(
                        ourPosition.x -
                        lastMoveEnd.x
                    ) == 1
                    &&
                    ourPosition.y == lastMoveEnd.y
                )
                {
                    // ????????赯?
                    Vector2 enPassantMove =
                        new Vector2(
                            lastMoveEnd.x,
                            ourPosition.y +
                            (IsWhite ? 1 : -1)
                        );

                    legalMoves.Add(enPassantMove);
                }
            }
        }

        //==============================
        // ??????
        //==============================
        Vector2 forwardMove =
            new Vector2(
                currentCoordinates.x,
                currentCoordinates.y + direction
            );

        // ????對??????
        if (
            IsPositionWithinBoard(forwardMove)
            &&
            logicManager.boardMap[
                (int)forwardMove.x,
                (int)forwardMove.y
            ] == null
        )
        {
            legalMoves.Add(forwardMove);
        }

        //==============================
        // ????謕?
        //==============================
        if (HasMoved == 0)
        {
            Vector2 doubleForwardMove =
                new Vector2(
                    currentCoordinates.x,
                    currentCoordinates.y +
                    (2 * direction)
                );

            // ???瞏殷??對??????
            if (
                IsPositionWithinBoard(doubleForwardMove)
                &&
                logicManager.boardMap[
                    (int)forwardMove.x,
                    (int)forwardMove.y
                ] == null
                &&
                logicManager.boardMap[
                    (int)doubleForwardMove.x,
                    (int)doubleForwardMove.y
                ] == null
            )
            {
                legalMoves.Add(doubleForwardMove);
            }
        }

        //==============================
        // ??????
        //==============================
        Vector2 captureLeft =
            new Vector2(
                currentCoordinates.x - 1,
                currentCoordinates.y + direction
            );

        //==============================
        // ??????
        //==============================
        Vector2 captureRight =
            new Vector2(
                currentCoordinates.x + 1,
                currentCoordinates.y + direction
            );

        //==============================
        // ????????
        //==============================
        if (
            IsPositionWithinBoard(captureLeft)
            &&
            logicManager.boardMap[
                (int)captureLeft.x,
                (int)captureLeft.y
            ] != null
        )
        {
            Piece targetPiece =
                logicManager.boardMap[
                    (int)captureLeft.x,
                    (int)captureLeft.y
                ];

            // ??????
            if (
                targetPiece != null &&
                targetPiece.IsWhite != IsWhite
            )
            {
                legalMoves.Add(captureLeft);
            }
        }

        //==============================
        // ?????????
        //==============================
        if (
            IsPositionWithinBoard(captureRight)
            &&
            logicManager.boardMap[
                (int)captureRight.x,
                (int)captureRight.y
            ] != null
        )
        {
            Piece targetPiece =
                logicManager.boardMap[
                    (int)captureRight.x,
                    (int)captureRight.y
                ];

            // ??????
            if (
                targetPiece != null &&
                targetPiece.IsWhite != IsWhite
            )
            {
                legalMoves.Add(captureRight);
            }
        }

        return legalMoves;
    }

    //==============================
    // ?謘??擗??哨??
    // ??踐??????
    //==============================
    public override List<Vector2> GetAttackedFields()
    {
        
        if (UsesDefinitionRules)
        {
            return GetDefinitionAttackedFields();
        }

// ?擗??哨???謅?
        List<Vector2> attackedFields =
            new List<Vector2>();

        // ?鞈???蹓??甄??
        int direction = IsWhite ? 1 : -1;

        //==============================
        // ????擗?
        //==============================
        Vector2 leftAttackMove =
            new Vector2(
                transform.position.x - 1,
                transform.position.z + direction
            );

        //==============================
        // ????擗?
        //==============================
        Vector2 rightAttackMove =
            new Vector2(
                transform.position.x + 1,
                transform.position.z + direction
            );

        // ??????????
        if (IsPositionWithinBoard(leftAttackMove))
        {
            attackedFields.Add(leftAttackMove);
        }

        if (IsPositionWithinBoard(rightAttackMove))
        {
            attackedFields.Add(rightAttackMove);
        }

        return attackedFields;
    }
}
