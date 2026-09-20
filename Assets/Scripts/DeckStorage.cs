using System;
using System.Collections.Generic;
using UnityEngine;

public static class DeckStorage
{
    private const string PlayerPrefsKey = "SelectedDeckV2";
    private const string LegacyPlayerPrefsKey = "SelectedDeckV1";
    public const int DefaultMaxCopies = 2;

    public sealed class DeckConfig
    {
        public readonly Dictionary<string, int> counts =
            new Dictionary<string, int>();
        public string openingCardId;

        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<string, int> pair in counts)
                {
                    total += Mathf.Max(0, pair.Value);
                }

                return total;
            }
        }
    }

    [Serializable]
    private class DeckCardCount
    {
        public string cardId;
        public int count;
    }

    [Serializable]
    private class DeckSaveData
    {
        public List<DeckCardCount> cardCounts = new List<DeckCardCount>();
        public string openingCardId;
    }

    [Serializable]
    private class LegacyDeckSaveData
    {
        public List<string> cardIds = new List<string>();
    }

    /// <summary>
    /// 載入牌組並轉成卡號清單；缺少有效設定時使用預設牌組。
    /// </summary>
    public static List<string> LoadOrDefault(
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        DeckConfig config = LoadConfigOrDefault(availableCards);
        List<string> result = new List<string>();
        foreach (CardDefinition card in availableCards)
        {
            if (card == null || !config.counts.TryGetValue(card.id, out int count))
            {
                continue;
            }

            for (int i = 0; i < count; i++)
            {
                result.Add(card.id);
            }
        }

        return result;
    }

    /// <summary>
    /// 載入目前或舊版牌組設定，並依現有卡牌庫清理資料。
    /// </summary>
    public static DeckConfig LoadConfigOrDefault(
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        DeckConfig config = PlayerPrefs.HasKey(PlayerPrefsKey)
            ? LoadCurrentConfig()
            : LoadLegacyConfig();

        if (config == null || config.counts.Count == 0)
        {
            config = BuildDefaultConfig(availableCards);
        }

        return SanitizeConfig(config, availableCards);
    }

    /// <summary>
    /// 將牌組選擇整理成目前版本的存檔格式並寫入 PlayerPrefs。
    /// </summary>
    public static void Save(
        Dictionary<string, int> selectedCounts,
        string openingCardId,
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        DeckConfig config = new DeckConfig();
        if (selectedCounts != null)
        {
            foreach (KeyValuePair<string, int> pair in selectedCounts)
            {
                config.counts[pair.Key] = pair.Value;
            }
        }

        config.openingCardId = openingCardId;
        config = SanitizeConfig(config, availableCards);
        if (config.TotalCount == 0)
        {
            return;
        }

        DeckSaveData data = new DeckSaveData
        {
            openingCardId = config.openingCardId
        };

        foreach (CardDefinition card in availableCards)
        {
            if (
                card == null ||
                !config.counts.TryGetValue(card.id, out int count) ||
                count <= 0
            )
            {
                continue;
            }

            data.cardCounts.Add(new DeckCardCount
            {
                cardId = card.id,
                count = count
            });
        }

        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();

        Debug.Log(
            $"[CardDebug][DeckSaved] Count={config.TotalCount} | " +
            $"Opening={config.openingCardId}"
        );
    }

    /// <summary>
    /// 將牌組選擇整理成目前版本的存檔格式並寫入 PlayerPrefs。
    /// </summary>
    public static void Save(
        IEnumerable<string> selectedIds,
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        if (selectedIds != null)
        {
            foreach (string cardId in selectedIds)
            {
                counts[cardId] = 1;
            }
        }

        Save(counts, null, availableCards);
    }

    /// <summary>
    /// 尚未儲存牌組時建立並保存預設牌組。
    /// </summary>
    public static void EnsureDefault(
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        if (
            PlayerPrefs.HasKey(PlayerPrefsKey) ||
            PlayerPrefs.HasKey(LegacyPlayerPrefsKey)
        )
        {
            return;
        }

        DeckConfig config = BuildDefaultConfig(availableCards);
        Save(config.counts, config.openingCardId, availableCards);
    }

    /// <summary>
    /// 依牌組設定建立卡牌清單，將指定起手卡放在最前方。
    /// </summary>
    public static List<CardDefinition> BuildDeck(
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        List<CardDefinition> result = new List<CardDefinition>();
        if (availableCards == null)
        {
            return result;
        }

        DeckConfig config = LoadConfigOrDefault(availableCards);
        Dictionary<string, int> remainingCounts =
            new Dictionary<string, int>(config.counts);

        if (
            !string.IsNullOrEmpty(config.openingCardId) &&
            remainingCounts.TryGetValue(config.openingCardId, out int openingCount) &&
            openingCount > 0
        )
        {
            CardDefinition openingCard = FindCard(
                availableCards,
                config.openingCardId
            );
            if (openingCard != null)
            {
                result.Add(openingCard);
                remainingCounts[config.openingCardId] = openingCount - 1;
            }
        }

        foreach (CardDefinition card in availableCards)
        {
            if (
                card == null ||
                !remainingCounts.TryGetValue(card.id, out int count)
            )
            {
                continue;
            }

            for (int i = 0; i < count; i++)
            {
                result.Add(card);
            }
        }

        return result;
    }

    /// <summary>
    /// 依目前牌組設定產生卡號清單。
    /// </summary>
    public static List<string> BuildDeckIds(
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        List<string> result = new List<string>();
        foreach (CardDefinition card in BuildDeck(availableCards))
        {
            if (card != null && !string.IsNullOrEmpty(card.id))
            {
                result.Add(card.id);
            }
        }

        return result;
    }

    /// <summary>
    /// 將目前牌組轉成可提交給連線主機的卡號字串。
    /// </summary>
    public static string BuildSerializedDeckIds(
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        return string.Join(",", BuildDeckIds(availableCards));
    }

    /// <summary>
    /// 判斷目前牌組是否設定有效的指定起手卡。
    /// </summary>
    public static bool HasOpeningCard(
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        DeckConfig config = LoadConfigOrDefault(availableCards);
        return !string.IsNullOrEmpty(config.openingCardId) &&
            config.counts.TryGetValue(config.openingCardId, out int count) &&
            count > 0;
    }

    /// <summary>
    /// 讀取目前版本的牌組存檔。
    /// </summary>
    private static DeckConfig LoadCurrentConfig()
    {
        try
        {
            DeckSaveData data = JsonUtility.FromJson<DeckSaveData>(
                PlayerPrefs.GetString(PlayerPrefsKey)
            );
            if (data == null) return null;

            DeckConfig config = new DeckConfig
            {
                openingCardId = data.openingCardId
            };

            if (data.cardCounts != null)
            {
                foreach (DeckCardCount entry in data.cardCounts)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.cardId))
                    {
                        config.counts[entry.cardId] = entry.count;
                    }
                }
            }

            return config;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// 讀取舊版牌組存檔並轉成目前設定結構。
    /// </summary>
    private static DeckConfig LoadLegacyConfig()
    {
        if (!PlayerPrefs.HasKey(LegacyPlayerPrefsKey))
        {
            return null;
        }

        try
        {
            LegacyDeckSaveData data =
                JsonUtility.FromJson<LegacyDeckSaveData>(
                    PlayerPrefs.GetString(LegacyPlayerPrefsKey)
                );
            if (data == null || data.cardIds == null) return null;

            DeckConfig config = new DeckConfig();
            foreach (string cardId in data.cardIds)
            {
                if (!string.IsNullOrEmpty(cardId))
                {
                    config.counts[cardId] = 1;
                }
            }

            return config;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// 依可用卡牌建立預設牌組設定。
    /// </summary>
    private static DeckConfig BuildDefaultConfig(
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        DeckConfig config = new DeckConfig();
        foreach (string cardId in GetAvailableIds(availableCards))
        {
            config.counts[cardId] = 1;
        }

        return config;
    }

    /// <summary>
    /// 移除不存在的卡號並依現有規則整理數量與起手卡設定。
    /// </summary>
    private static DeckConfig SanitizeConfig(
        DeckConfig config,
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        DeckConfig result = new DeckConfig();
        HashSet<string> validIds = new HashSet<string>(
            GetAvailableIds(availableCards)
        );

        foreach (string cardId in validIds)
        {
            if (!config.counts.TryGetValue(cardId, out int count))
            {
                continue;
            }

            count = Mathf.Clamp(count, 0, DefaultMaxCopies);
            if (count > 0)
            {
                result.counts[cardId] = count;
            }
        }

        if (
            !string.IsNullOrEmpty(config.openingCardId) &&
            result.counts.ContainsKey(config.openingCardId)
        )
        {
            result.openingCardId = config.openingCardId;
        }

        return result;
    }

    /// <summary>
    /// 依卡號尋找卡牌定義。
    /// </summary>
    private static CardDefinition FindCard(
        IReadOnlyList<CardDefinition> availableCards,
        string cardId
    )
    {
        if (availableCards == null) return null;

        foreach (CardDefinition card in availableCards)
        {
            if (card != null && card.id == cardId)
            {
                return card;
            }
        }

        return null;
    }

    /// <summary>
    /// 收集卡牌庫中的有效卡號。
    /// </summary>
    private static List<string> GetAvailableIds(
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        List<string> ids = new List<string>();
        if (availableCards == null)
        {
            return ids;
        }

        foreach (CardDefinition card in availableCards)
        {
            if (card != null && !string.IsNullOrEmpty(card.id))
            {
                ids.Add(card.id);
            }
        }

        return ids;
    }
}
