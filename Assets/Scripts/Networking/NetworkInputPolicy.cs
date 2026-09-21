using System;
using System.Collections.Generic;

/// <summary>集中處理不依賴場景的連線輸入驗證。</summary>
public sealed class NetworkInputPolicy
{
    private readonly Dictionary<int, int> lastSequences = new Dictionary<int, int>();

    /// <summary>操作要求僅由主機接收，狀態及結果僅接受目前主機發送。</summary>
    public static bool IsTrustedSender(byte code, int sender, int master, bool localIsMaster, bool isRoomMember)
    {
        if (!isRoomMember || master <= 0) return false;
        bool request = code == 1 || code == 2 || code == 3 || code == 14;
        return request ? localIsMaster : sender == master;
    }

    /// <summary>每位玩家只接受遞增的正整數序號，拒絕重送及過期操作。</summary>
    public bool TryConsume(int actor, int sequence)
    {
        if (sequence <= 0 || (lastSequences.TryGetValue(actor, out int previous) && sequence <= previous)) return false;
        lastSequences[actor] = sequence;
        return true;
    }

    /// <summary>驗證卡號、數量與重複上限，成功前不更動既有牌組。</summary>
    public static bool TryParseDeck(string value, Func<string, CardDefinition> lookup, int libraryCount,
        out List<CardDefinition> cards)
    {
        cards = new List<CardDefinition>();
        if (string.IsNullOrEmpty(value) || value.Length > 8192) return false;
        string[] ids = value.Split(',');
        if (ids.Length > libraryCount * DeckStorage.DefaultMaxCopies) return false;
        var counts = new Dictionary<string, int>();
        foreach (string id in ids)
        {
            CardDefinition card = lookup(id);
            counts.TryGetValue(id, out int count);
            if (card == null || count >= DeckStorage.DefaultMaxCopies) return false;
            counts[id] = count + 1;
            cards.Add(card);
        }
        return cards.Count > 0;
    }

    /// <summary>依事件協定嚴格檢查欄位型別及大小，避免轉型例外或過大資料。</summary>
    public static bool IsValidPayload(byte code, object payload)
    {
        if (code == 1 || code == 6 || code == 7 || code == 12 || code == 13)
            return payload is string text && text.Length <= (code == 6 ? 262144 : 8192);
        if (code == 14) return true;
        string schema;
        switch (code)
        {
            case 2: schema = "ibiis"; break;
            case 3: schema = "sb"; break;
            case 4: schema = "iibs"; break;
            case 5: schema = "biisb"; break;
            case 8: schema = "ibiiiibiisb"; break;
            case 9: schema = "ibiis"; break;
            case 10: schema = "ibsii"; break;
            case 11: schema = "ibss"; break;
            case 15: schema = "ib"; break;
            default: return false;
        }
        if (!(payload is object[] fields) || fields.Length != schema.Length) return false;
        for (int i = 0; i < fields.Length; i++)
        {
            if (schema[i] == 'i' && !(fields[i] is int)) return false;
            if (schema[i] == 'b' && !(fields[i] is bool)) return false;
            if (schema[i] == 's' && !(fields[i] is string value && value.Length <= 8192)) return false;
        }
        if (code == 2 || code == 9) return IsBoardCell(fields[2], fields[3]);
        if (code == 8) return IsBoardCell(fields[2], fields[3]) && IsBoardCell(fields[4], fields[5]);
        if (code == 10) return IsBoardCell(fields[3], fields[4]);
        return true;
    }

    /// <summary>拒絕棋盤外座標，避免索引越界及非法目標。</summary>
    private static bool IsBoardCell(object x, object y)
    {
        return (int)x >= 0 && (int)x < 8 && (int)y >= 0 && (int)y < 8;
    }
}
