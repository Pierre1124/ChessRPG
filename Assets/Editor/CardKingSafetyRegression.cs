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
            // Multiple rows/columns must all survive the same geometry used by gameplay.
            logic.boardMap = new Piece[8, 8];
            var rooks = new List<Piece>();
            foreach (int x in new[] { 0, 3, 6 })
                foreach (int y in new[] { 0, 3 }) rooks.Add(Add(typeof(Rook), true, x, y));
            foreach (string id in new[] { "J03", "J04" })
            {
                logic.boardMap = new Piece[8, 8];
                for (int i = 0; i < rooks.Count; i++)
                {
                    rooks[i].transform.position = new Vector3(i / 2 * 3, 0, i % 2 * 3);
                    logic.boardMap[i / 2 * 3, i % 2 * 3] = rooks[i];
                }
                var card = new CardDefinition { id = id, cardType = CardType.JobChange, targetTypes = CardTargetType.Rook };
                foreach (Piece endpoint in rooks) { endpoint.ClearRpgState(); endpoint.cardDefinition = card; }
                var zones = logic.GetActiveBoardFieldZones();
                Check(zones.Count == 7, id + " creates all seven adjacent links across parallel rows and columns");
                var covered = new HashSet<Vector2Int>();
                foreach (var zone in zones)
                    foreach (Vector2Int cell in zone.cells)
                    {
                        Check(logic.boardMap[cell.x, cell.y] == null, id + " never creates a token on intermediate castle");
                        covered.Add(cell);
                    }
                Check(covered.Count == 14, id + " covers both rows and all three columns");
                foreach (Piece endpoint in rooks) endpoint.cardDefinition = null;
                logic.WouldCardLeaveKingInCheck(card, rooks[0]);
                Check(rooks.TrueForAll(piece => piece.cardDefinition == null && piece.CardRuntime == null),
                    id + " preview restores every connected endpoint");
                // Supply only missing templates temporarily, restoring the scene library in finally.
                var library = UnityEngine.Object.FindFirstObjectByType<SceneObjectTemplates>(FindObjectsInactive.Include);
                if (library == null) library = root.AddComponent<SceneObjectTemplates>();
                var fixtureEntries = new List<SceneObjectTemplates.Entry>();
                foreach (string key in new[] { "Moat token", "Electric net token" })
                {
                    if (library.templates.Exists(entry => entry.key == key && entry.template != null)) continue;
                    var template = new GameObject(key);
                    template.transform.SetParent(root.transform);
                    template.SetActive(false);
                    var entry = new SceneObjectTemplates.Entry { key = key, template = template, runtimeParent = root.transform };
                    library.templates.Insert(0, entry);
                    fixtureEntries.Add(entry);
                }
                try
                {
                    card.skill.ConfigureCard(card);
                    rooks[0].ApplyCard(card);
                    Check(rooks.TrueForAll(piece => piece.cardDefinition == card), id + " actual equip reaches every parallel castle");
                    Check(logic.GetActiveBoardFieldZones().Count == 7, id + " actual equip creates every connection");
                    // Reproduce the user's sequence: two endpoints first, a third rook aligns later.
                    foreach (Piece endpoint in rooks) endpoint.ClearRpgState();
                    logic.boardMap = new Piece[8, 8];
                    logic.boardMap[0, 0] = rooks[0];
                    logic.boardMap[3, 0] = rooks[2];
                    rooks[4].transform.position = new Vector3(6, 0, 2);
                    logic.boardMap[6, 2] = rooks[4];
                    rooks[0].ApplyCard(card);
                    Check(rooks[4].cardDefinition == null && logic.GetActiveBoardFieldZones().Count == 1,
                        id + " initially has only the original two-castle connection");
                    Move(rooks[4], 6, 0);
                    PieceActiveSkill.BeginTurn(logic, false);
                    Check(rooks[4].cardDefinition == null, id + " opponent turn does not extend friendly field");
                    PieceActiveSkill.BeginTurn(logic, true);
                    Check(rooks[4].cardDefinition == card && logic.GetActiveBoardFieldZones().Count == 2,
                        id + " own turn connects a third rook that aligned after equipping");
                    var runtime = rooks[4].CardRuntime;
                    PieceActiveSkill.BeginTurn(logic, true);
                    Check(rooks[4].CardRuntime == runtime && logic.GetActiveBoardFieldZones().Count == 2,
                        id + " repeated turn refresh neither reapplies card nor duplicates zones");
                    rooks[4].ClearRpgState();
                    logic.boardMap[4, 0] = Add(typeof(Pawn), true, 4, 0);
                    PieceActiveSkill.BeginTurn(logic, true);
                    Check(rooks[4].cardDefinition == null, id + " late connection respects intervening pieces");
                    logic.boardMap[4, 0] = null;
                    var unrelated = new CardDefinition { id = "OtherJob", cardType = CardType.JobChange };
                    rooks[4].cardDefinition = unrelated;
                    PieceActiveSkill.BeginTurn(logic, true);
                    Check(rooks[4].cardDefinition == unrelated, id + " automatic turn refresh preserves other equipment");
                }
                finally
                {
                    typeof(LogicManager).GetMethod("ClearBoardFieldVisuals", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(logic, null);
                    foreach (var entry in fixtureEntries) library.templates.Remove(entry);
                }
            }
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

