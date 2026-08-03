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

    public static string BuildSerializedDeckIds(
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        return string.Join(",", BuildDeckIds(availableCards));
    }

    public static bool HasOpeningCard(
        IReadOnlyList<CardDefinition> availableCards
    )
    {
        DeckConfig config = LoadConfigOrDefault(availableCards);
        return !string.IsNullOrEmpty(config.openingCardId) &&
            config.counts.TryGetValue(config.openingCardId, out int count) &&
            count > 0;
    }

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
