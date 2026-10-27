using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class CardKeywordEntry
{
    public string id;
    public string term;
    public string description;
    public string[] aliases;
    public string damageType;
    public string fallbackColor;
}

/// <summary>只處理顯示文字；不改寫卡牌資料或遊戲規則。</summary>
public sealed class CardKeywordFormatter
{
    private readonly Dictionary<string, CardKeywordEntry> entries = new Dictionary<string, CardKeywordEntry>();
    private readonly List<KeyValuePair<string, string>> terms = new List<KeyValuePair<string, string>>();
    private readonly Func<CardKeywordEntry, string> colorResolver;

    public CardKeywordFormatter(IEnumerable<CardKeywordEntry> source, Func<CardKeywordEntry, string> colorResolver = null)
    {
        this.colorResolver = colorResolver;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in source ?? Array.Empty<CardKeywordEntry>())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.term) ||
                string.IsNullOrWhiteSpace(entry.description) || entry.id.Any(c => !char.IsLetterOrDigit(c) && c != '-')) continue;
            if (entries.ContainsKey(entry.id)) continue;
            entries.Add(entry.id, entry);
            foreach (string term in new[] { entry.term }.Concat(entry.aliases ?? Array.Empty<string>()))
                if (!string.IsNullOrWhiteSpace(term) && seen.Add(term)) terms.Add(new KeyValuePair<string, string>(term, entry.id));
        }
        terms.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
    }

    public bool TryGet(string link, out CardKeywordEntry entry)
    {
        entry = null;
        return link != null && link.StartsWith("kw:", StringComparison.Ordinal) && entries.TryGetValue(link.Substring(3), out entry);
    }

    public string Format(string source)
    {
        if (string.IsNullOrEmpty(source)) return source ?? "";
        var result = new StringBuilder(source.Length);
        bool inLink = false, noParse = false;
        for (int i = 0; i < source.Length;)
        {
            if (source[i] == '<')
            {
                int end = source.IndexOf('>', i);
                if (end >= 0)
                {
                    string tag = source.Substring(i, end - i + 1);
                    if (tag.Equals("</noparse>", StringComparison.OrdinalIgnoreCase)) noParse = false;
                    else if (!noParse)
                    {
                        if (tag.Equals("<noparse>", StringComparison.OrdinalIgnoreCase)) noParse = true;
                        else if (tag.StartsWith("<link=", StringComparison.OrdinalIgnoreCase)) inLink = true;
                        else if (tag.Equals("</link>", StringComparison.OrdinalIgnoreCase)) inLink = false;
                    }
                    result.Append(tag); i = end + 1; continue;
                }
            }
            bool found = false;
            if (!inLink && !noParse)
                foreach (var term in terms)
                {
                    int length = term.Key.Length;
                    if (i + length > source.Length || string.CompareOrdinal(source, i, term.Key, 0, length) != 0) continue;
                    if (IsAsciiWord(term.Key[0]) && i > 0 && IsAsciiWord(source[i - 1])) continue;
                    if (IsAsciiWord(term.Key[length - 1]) && i + length < source.Length && IsAsciiWord(source[i + length])) continue;
                    string color = colorResolver?.Invoke(entries[term.Value]);
                    result.Append("<link=\"kw:").Append(term.Value).Append("\"><b>");
                    if (!string.IsNullOrEmpty(color)) result.Append("<color=#").Append(color).Append('>');
                    result.Append("<u>").Append(term.Key).Append("</u>");
                    if (!string.IsNullOrEmpty(color)) result.Append("</color>");
                    result.Append("</b></link>");
                    i += length; found = true; break;
                }
            if (!found) result.Append(source[i++]);
        }
        return result.ToString();
    }

    private static bool IsAsciiWord(char c) => c < 128 && (char.IsLetterOrDigit(c) || c == '_');
}

public static class CardKeywordGlossary
{
    [Serializable] private class Data { public CardKeywordEntry[] entries; }
    private static CardKeywordFormatter formatter;
    public static CardKeywordFormatter Formatter
    {
        get
        {
            if (formatter == null)
            {
                var asset = Resources.Load<TextAsset>("CardKeywords");
                var data = asset != null ? JsonUtility.FromJson<Data>(asset.text) : null;
                formatter = new CardKeywordFormatter(data?.entries, ResolveColor);
            }
            return formatter;
        }
    }
    private static string ResolveColor(CardKeywordEntry entry)
    {
        if (string.IsNullOrEmpty(entry.damageType) || !Enum.TryParse(entry.damageType, out BattlePresentationType type)) return null;
        var style = BattleFeedback.GetStyle(type);
        if (style != null) return ColorUtility.ToHtmlStringRGB(style.numberColor);
        // Menu scenes have no battle presenter; use the saved battle palette there.
        return ColorUtility.TryParseHtmlString("#" + entry.fallbackColor, out Color fallback)
            ? ColorUtility.ToHtmlStringRGB(fallback) : null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => formatter = null;
}
