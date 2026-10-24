from pathlib import Path
p=Path('Assets/Editor/CoreRulesRegression.cs')
s=p.read_text(encoding='utf-8-sig')
pos=s.index('        foreach (int requester')
s=s[:pos]+'''        var fieldRooks = new HashSet<UnityEngine.Vector2Int>
        {
            new UnityEngine.Vector2Int(0, 0), new UnityEngine.Vector2Int(3, 0), new UnityEngine.Vector2Int(6, 0),
            new UnityEngine.Vector2Int(0, 3), new UnityEngine.Vector2Int(3, 3), new UnityEngine.Vector2Int(6, 3)
        };
        var fieldOccupied = new HashSet<UnityEngine.Vector2Int>(fieldRooks);
        var connected = FieldRookConnections.Collect(new UnityEngine.Vector2Int(0, 0), fieldOccupied.Contains, fieldRooks.Contains);
        check(connected.Count == 5 && new HashSet<UnityEngine.Vector2Int>(connected).Count == 5,
            "Field card connects every rook across parallel rows without duplicates");
        foreach (var origin in fieldRooks)
            check(FieldRookConnections.Collect(origin, fieldOccupied.Contains, fieldRooks.Contains).Count == 5,
                "Field network is independent of selected endpoint " + origin);
        fieldRooks.Clear(); fieldOccupied.Clear();
        fieldRooks.UnionWith(new[] { new UnityEngine.Vector2Int(0, 0), new UnityEngine.Vector2Int(3, 0), new UnityEngine.Vector2Int(6, 0) });
        fieldOccupied.UnionWith(fieldRooks);
        check(FieldRookConnections.Collect(new UnityEngine.Vector2Int(0, 0), fieldOccupied.Contains, fieldRooks.Contains).Count == 2,
            "Collinear rooks connect through intermediate friendly endpoint");
        fieldOccupied.Add(new UnityEngine.Vector2Int(1, 0));
        check(FieldRookConnections.Collect(new UnityEngine.Vector2Int(0, 0), fieldOccupied.Contains, fieldRooks.Contains).Count == 0,
            "Intervening piece blocks the field network");
        fieldOccupied.Remove(new UnityEngine.Vector2Int(1, 0));
        fieldRooks.Remove(new UnityEngine.Vector2Int(3, 0));
        check(FieldRookConnections.Collect(new UnityEngine.Vector2Int(0, 0), fieldOccupied.Contains, fieldRooks.Contains).Count == 0,
            "Enemy rook blocks connection to friendly rook beyond");
        fieldRooks.Clear(); fieldOccupied.Clear();
        fieldRooks.UnionWith(new[] { new UnityEngine.Vector2Int(0, 0), new UnityEngine.Vector2Int(1, 0), new UnityEngine.Vector2Int(3, 3) });
        fieldOccupied.UnionWith(fieldRooks);
        check(FieldRookConnections.Collect(new UnityEngine.Vector2Int(0, 0), fieldOccupied.Contains, fieldRooks.Contains).Count == 0,
            "Adjacent and diagonal rooks do not create a field");
'''+s[pos:]
p.write_text(s,encoding='utf-8')
p=Path('Assets/Editor/CardKingSafetyRegression.cs')
s=p.read_text(encoding='utf-8-sig')
pos=s.index('        }\n        finally')
s=s[:pos]+'''            // Multiple rows/columns must all survive the same geometry used by gameplay.
            logic.boardMap = new Piece[8, 8];
            var rooks = new List<Piece>();
            foreach (int x in new[] { 0, 3, 6 })
                foreach (int y in new[] { 0, 3 }) rooks.Add(Add(typeof(Rook), true, x, y));
            foreach (string id in new[] { "J03", "J04" })
            {
                var card = new CardDefinition { id = id, cardType = CardType.JobChange, targetTypes = CardTargetType.Rook };
                foreach (Piece endpoint in rooks) endpoint.cardDefinition = card;
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
            }
'''+s[pos:]
p.write_text(s,encoding='utf-8')
