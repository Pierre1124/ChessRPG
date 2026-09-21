using System;
using UnityEngine;

/// <summary>只傳送接收玩家手牌與公開張數，不傳送任何牌堆順序。</summary>
[Serializable]
public sealed class PrivateCardState
{
    public int version = 1;
    public bool recipientWhite;
    public string handIds;
    public int opponentHandCount;

    /// <summary>建立指定玩家可見的卡牌資料。</summary>
    public static string Encode(bool white, string hand, int opponentCount)
    {
        return JsonUtility.ToJson(new PrivateCardState
        { recipientWhite = white, handIds = hand, opponentHandCount = opponentCount });
    }

    /// <summary>拒絕舊版完整手牌協定、無效版本及非法張數。</summary>
    public static bool TryDecode(string json, out PrivateCardState state)
    {
        state = null;
        if (string.IsNullOrEmpty(json) || json.Length > 8192 || !json.StartsWith("{")) return false;
        try { state = JsonUtility.FromJson<PrivateCardState>(json); }
        catch (ArgumentException) { return false; }
        return state != null && state.version == 1 && state.handIds != null &&
            state.opponentHandCount >= 0 && state.opponentHandCount <= 2048;
    }
}
