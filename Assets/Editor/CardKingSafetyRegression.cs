using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>用獨立、停用的棋盤驗證出牌安全，不操作目前場景棋局。</summary>
public static class CardKingSafetyRegression
{
    [MenuItem("Tools/Chess/Run Card King Safety Regression")]
    public static void Run()
    {
        var root = new GameObject("Card safety regression");
        root.SetActive(false);
        var results = new List<string>();
        try
        {
            var logic = root.AddComponent<LogicManager>();
            typeof(LogicManager).GetProperty("IsClassicChess").SetValue(logic, false);
            Piece Add(Type type, bool white, int x, int y)
            {
                var go = new GameObject(type.Name);
                go.transform.SetParent(root.transform);
                go.transform.position = new Vector3(x, 0, y);
                var piece = (Piece)go.AddComponent(type);
                typeof(Piece).GetField("logicManager", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(piece, logic);
                piece.Initialize(type.Name, white);
                logic.boardMap[x, y] = piece;
                return piece;
            }
            void Check(bool condition, string label)
            {
                if (!condition) throw new Exception(label);
                results.Add("PASS: " + label);
            }
            void Move(Piece piece, int x, int y)
            {
                Vector2 old = piece.GetCoordinates();
                logic.boardMap[(int)old.x, (int)old.y] = null;
                piece.transform.position = new Vector3(x, 0, y);
                logic.boardMap[x, y] = piece;
            }
            var whiteKing = Add(typeof(King), true, 7, 0);
            var blackKing = Add(typeof(King), false, 3, 4);
            var pawn = Add(typeof(Pawn), true, 3, 2);
            var spear = new CardDefinition { id = "J01", cardType = CardType.JobChange, targetTypes = CardTargetType.Pawn };
            spear.skill.ConfigureCard(spear);
            var dismissal = new CardDefinition { id = "E01", cardType = CardType.Event, targetTypes = CardTargetType.All };
            Check(logic.WouldCardLeaveKingInCheck(spear, pawn), "Spearman cannot give check before moving");
            Check(!spear.CanApplyTo(pawn, logic), "Shared target validation rejects checking card");
            Check(pawn.cardDefinition == null && pawn.CardRuntime == null, "Preview restores equipment");
            Move(blackKing, 3, 3);
            Check(!logic.WouldCardLeaveKingInCheck(spear, pawn), "Spearman first forward square is not attacked");
            pawn.cardDefinition = spear;
            Move(blackKing, 4, 3);
            Check(logic.WouldCardLeaveKingInCheck(dismissal, pawn), "Dismissal cannot restore a checking pawn diagonal");
            Move(blackKing, 3, 4);
            Check(!logic.WouldCardLeaveKingInCheck(dismissal, pawn), "Dismissal may remove an existing check");
            Move(blackKing, 7, 7);
            Move(whiteKing, 4, 3);
            var enemyPawn = Add(typeof(Pawn), false, 3, 4);
            enemyPawn.cardDefinition = spear;
            Check(logic.WouldCardLeaveKingInCheck(dismissal, enemyPawn), "Dismissal cannot expose own king");
            Move(whiteKing, 7, 0);
            Move(pawn, 0, 1);
            Move(enemyPawn, 1, 6);
            var rook = Add(typeof(Rook), true, 0, 4);
            var first = Add(typeof(Rook), false, 3, 2);
            var second = Add(typeof(Rook), false, 3, 6);
            var moat = new CardDefinition { id = "J03", cardType = CardType.JobChange, targetTypes = CardTargetType.Rook };
            first.cardDefinition = second.cardDefinition = moat;
            Move(blackKing, 6, 4);
            logic.UpdateCheckMap();
            Check(!logic.whiteCheckMap[6, 4], "Moat blocks check rays");
            Check(logic.WouldCardLeaveKingInCheck(dismissal, first), "Removing moat pair cannot reveal check");
            Check(first.cardDefinition == moat && second.cardDefinition == moat, "Preview restores both moat endpoints");
            Check(!logic.whiteCheckMap[6, 4] && logic.boardMap[6, 4] == blackKing, "Preview preserves attack map and board");
            Check(!logic.WouldCardLeaveKingInCheck(moat, first), "Keeping protective moat is allowed");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            Directory.CreateDirectory("output");
            File.WriteAllLines("output/card-king-safety-regression.txt", results);
        }
        Debug.Log("Card king safety: " + results.Count + " passed");
    }
}

