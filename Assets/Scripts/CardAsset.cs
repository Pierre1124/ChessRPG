using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardPlayActionType { Damage, Heal, ApplyStatuses, ApplyStatusesToBoard }

/// <summary>可由 Inspector 編輯並組合的一次性效果。</summary>
[Serializable]
public sealed class CardPlayAction
{
    public CardPlayActionType type;
    [Min(0)] public int amount;
}

/// <summary>持久化卡牌資料；對局使用副本，不在資產上存放層數或充能。</summary>
[CreateAssetMenu(menuName = "Chess RPG/Card", fileName = "NewCard")]
public sealed class CardAsset : ScriptableObject
{
    public CardDefinition definition = new CardDefinition();
    [TextArea] public string sourceReference;

    /// <summary>複製巢狀效果與狀態資料，同時保留 Unity 素材引用。</summary>
    public CardDefinition CreateDefinition()
    {
        CardDefinition copy = JsonUtility.FromJson<CardDefinition>(JsonUtility.ToJson(definition));
        // 複製卡片後只需改卡號，狀態仍歸屬於新的卡片。
        foreach (StatusDefinition status in copy.statusesToApply)
            if (status != null) status.sourceCardId = copy.id;
        return copy;
    }
}

/// <summary>從正式資產載入卡庫，維持卡號與牌組儲存格式相容。</summary>
public static class CardAssetLibrary
{
    /// <summary>自動收集卡牌資料夾，拒絕空白或重複卡號，避免連線查找歧義。</summary>
    public static List<CardDefinition> LoadDefinitions()
    {
        CardAsset[] assets = Resources.LoadAll<CardAsset>("Cards");
        Array.Sort(assets, (a, b) => CompareIds(a.definition?.id, b.definition?.id));
        var cards = new List<CardDefinition>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (CardAsset asset in assets)
        {
            if (asset.definition == null || string.IsNullOrWhiteSpace(asset.definition.id) || !ids.Add(asset.definition.id))
                throw new InvalidOperationException("卡牌資產的卡號空白或重複：" + asset.name);
            cards.Add(asset.CreateDefinition());
        }
        if (cards.Count == 0) Debug.LogError("缺少卡牌資產，請先執行 Tools/Chess/Cards/Migrate Spreadsheet Cards。");
        return cards;
    }

    /// <summary>維持轉職、事件、場地及其卡號的穩定順序。</summary>
    private static int CompareIds(string first, string second)
    {
        int a = Group(first), b = Group(second);
        return a != b ? a.CompareTo(b) : string.CompareOrdinal(first, second);
    }

    /// <summary>取得卡號分類，未知前綴排在既有分類之後。</summary>
    private static int Group(string id)
    {
        if (string.IsNullOrEmpty(id)) return 4;
        return id[0] == 'J' ? 0 : id[0] == 'E' ? 1 : id[0] == 'F' ? 2 : 3;
    }
}
