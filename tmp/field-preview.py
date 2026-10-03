from pathlib import Path
p=Path('Assets/Scripts/LogicManager.cs');s=p.read_text(encoding='utf-8-sig');a=s.index('        if (IsClassicChess) return false;',s.index('public bool TryPlayFieldCard(CardDefinition card, FieldCardPlace place)'));b=s.index('        if (cardSlotCost > 1)',a)
s=s[:a]+'''        if (!CanPlayFieldCard(card, place, out string reason))
        {
            GameFlowUI.Show(reason);
            return false;
        }
        int cardSlotCost = GetFieldSlotCost(card);

'''+s[b:];a=s.index('    /// <summary>\n    /// 檢查欄位與場地規則後嘗試放置場地卡。')
s=s[:a]+'''    /// <summary>提供場地欄位供預覽查詢，避免重複搜尋場景。</summary>
    public IReadOnlyList<FieldCardPlace> GetFieldPlaces()
    {
        ResolveFieldCardPlaces();
        return fieldCardPlaces;
    }

    /// <summary>共用場地放置與預覽規則，不消耗卡牌或觸發合成。</summary>
    public bool CanPlayFieldCard(CardDefinition card, FieldCardPlace place, out string reason)
    {
        ResolveFieldCardPlaces();
        reason = null;
        if (IsClassicChess) reason = "普通西洋棋模式不能使用場地卡";
        else if (card == null || card.cardType != CardType.Field) reason = "這不是場地卡";
        else if (place == null || !IsKnownFieldPlace(place) || !place.isActiveAndEnabled) reason = "請放到場地欄位";
        else if (isFieldFusionPlaying) reason = "場地合成中，請稍候";
        else if (place.ActiveCard != null) reason = "這個場地欄位已被佔用";
        else if (GetFieldSlotCost(card) > GetEmptyFieldPlaceCount()) reason = "空的場地欄位不足";
        return reason == null;
    }

'''+s[a:];p.write_text(s,encoding='utf-8')
