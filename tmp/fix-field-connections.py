from pathlib import Path
p=Path('Assets/Scripts/CardSkill.cs')
s=p.read_text(encoding='utf-8-sig')
a=s.index('    /// <summary>',s.index('public static class BoardFieldCardSkillUtility'))
b=s.index('    /// <summary>\n    /// 檢查目前技能情境',a)
s=s[:a]+'''    private static readonly HashSet<LogicManager> equippingBoards = new HashSet<LogicManager>();

    /// <summary>判斷是否存在有空格且路徑暢通的友方城堡連線。</summary>
    public static bool HasAlignedRookPartner(CardSkillContext context)
    {
        return FindConnectedRookPartners(context).Count > 0;
    }

    /// <summary>一次套用所有相連城堡，避免 OnEquip 遞迴選回既有搭檔而漏掉分支。</summary>
    public static void EquipAlignedRookPartner(CardSkillContext context, string logName)
    {
        if (!IsValid(context) || !equippingBoards.Add(context.logic)) return;
        try
        {
            foreach (Piece partner in FindConnectedRookPartners(context))
            {
                if (partner.cardDefinition == context.sourceCard) continue;
                Debug.Log($"[CardDebug][FieldAutoEquip] Card={logName} | Owner={Describe(context.owner)} | Partner={Describe(partner)}");
                partner.ApplyCard(context.sourceCard);
            }
            context.logic.RefreshBoardFieldEffects();
        }
        finally { equippingBoards.Remove(context.logic); }
    }

    /// <summary>出牌與將軍預判共用完整連線集合，包含經其他城堡延伸的平行列。</summary>
    internal static List<Piece> FindConnectedRookPartners(CardSkillContext context)
    {
        var partners = new List<Piece>();
        if (!IsValid(context)) return partners;
        var cells = FieldRookConnections.Collect(Vector2Int.RoundToInt(context.owner.GetCoordinates()),
            cell => context.logic.boardMap[cell.x, cell.y] != null,
            cell => context.logic.boardMap[cell.x, cell.y] is Rook rook && rook.IsWhite == context.owner.IsWhite);
        foreach (Vector2Int cell in cells) partners.Add(context.logic.boardMap[cell.x, cell.y]);
        return partners;
    }

'''+s[b:]
p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/LogicManager.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('''                    Piece partner = BoardFieldCardSkillUtility.FindAlignedRookPartner(
                        new CardSkillContext(this, target, card));
                    if (partner != null && partner.cardDefinition != card)
                        partner.SetCardForRulePreview(card, null);''','''                    foreach (Piece partner in BoardFieldCardSkillUtility.FindConnectedRookPartners(
                        new CardSkillContext(this, target, card)))
                        if (partner.cardDefinition != card) partner.SetCardForRulePreview(card, null);''')
s=s.replace('''        List<BoardFieldEffectZone> zones = GetActiveBoardFieldZones();
        foreach (BoardFieldEffectZone zone in zones)''','''        List<BoardFieldEffectZone> zones = GetActiveBoardFieldZones();
        var renderedCells = new HashSet<(BoardFieldEffectType, Vector2Int)>();
        foreach (BoardFieldEffectZone zone in zones)''',1)
s=s.replace('''                CreateBoardFieldVisual(visualSource, cell, zone.type);''','''                if (renderedCells.Add((zone.type, cell)))
                    CreateBoardFieldVisual(visualSource, cell, zone.type);''',1)
# Prevent a long connection spanning a third endpoint and duplicating the two shorter connections.
needle='''                BoardFieldEffectZone zone = BuildFieldZone('''
s=s.replace(needle,'''                bool hasIntermediateSource = false;
                foreach (Piece intermediate in sources)
                {
                    if (intermediate == null || intermediate == first || intermediate == second ||
                        intermediate.IsWhite != first.IsWhite) continue;
                    Vector2Int cell = ToBoardCell(intermediate.GetCoordinates());
                    if ((firstCell.x == secondCell.x && cell.x == firstCell.x &&
                         cell.y > Mathf.Min(firstCell.y, secondCell.y) && cell.y < Mathf.Max(firstCell.y, secondCell.y)) ||
                        (firstCell.y == secondCell.y && cell.y == firstCell.y &&
                         cell.x > Mathf.Min(firstCell.x, secondCell.x) && cell.x < Mathf.Max(firstCell.x, secondCell.x)))
                    { hasIntermediateSource = true; break; }
                }
                if (hasIntermediateSource) continue;

'''+needle,1)
p.write_text(s,encoding='utf-8')
