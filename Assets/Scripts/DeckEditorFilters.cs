using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>牌組分類、搜尋與獨立初始卡選擇；所有控制項由場景提供。</summary>
public sealed class DeckEditorFilters : MonoBehaviour
{
    public TMP_InputField search;
    public TMP_Dropdown opening;
    public Button[] categories;
    public Button selectedOnlyButton, resetButton;
    public TMP_Text resultCount;
    private StartMenuController owner;
    private int category = -1;
    private bool selectedOnly;
    private readonly List<string> openingIds = new List<string>();

    /// <summary>開啟編輯器時重設篩選並綁定控制項，重複開啟不累加事件。</summary>
    public void Bind(StartMenuController controller)
    {
        owner = controller;
        search.onValueChanged.RemoveAllListeners(); search.onValueChanged.AddListener(_ => Refresh());
        opening.onValueChanged.RemoveAllListeners(); opening.onValueChanged.AddListener(index =>
        { if (index >= 0 && index < openingIds.Count) owner.SelectOpeningCard(openingIds[index]); });
        for (int i = 0; i < categories.Length; i++)
        {
            int value = i - 1;
            categories[i].onClick.RemoveAllListeners();
            categories[i].onClick.AddListener(() => { category = value; Refresh(); });
        }
        selectedOnlyButton.onClick.RemoveAllListeners();
        selectedOnlyButton.onClick.AddListener(() => { selectedOnly = !selectedOnly; Refresh(); });
        resetButton.onClick.RemoveAllListeners(); resetButton.onClick.AddListener(ResetFilters);
        ResetFilters();
    }

    /// <summary>恢復所有卡片，保留牌組及初始卡選擇。</summary>
    public void ResetFilters() { category = -1; selectedOnly = false; search.SetTextWithoutNotify(""); Refresh(); }

    /// <summary>同步分類按鈕狀態與結果清單。</summary>
    private void Refresh()
    {
        for (int i = 0; i < categories.Length; i++)
            categories[i].GetComponent<Image>().color = category == i - 1 ? new Color(.12f,.48f,.4f) : new Color(.16f,.2f,.26f);
        selectedOnlyButton.GetComponentInChildren<TMP_Text>().text = selectedOnly ? "已選卡片 ✓" : "只看已選";
        owner?.ApplyDeckFilters();
    }

    /// <summary>比對三大類、已選狀態及卡號／名稱／說明關鍵字。</summary>
    public bool Matches(CardDefinition card, int count)
    {
        if (category >= 0 && (int)card.cardType != category) return false;
        if (selectedOnly && count <= 0) return false;
        string query = search.text.Trim();
        return string.IsNullOrEmpty(query) || Contains(card.id, query) || Contains(card.cardName, query) || Contains(card.description, query);
    }

    /// <summary>文字搜尋不區分大小寫，容許尚未填寫的卡牌欄位。</summary>
    private static bool Contains(string text, string query) => text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>初始卡候選只包含牌組卡片，不受目前清單篩選影響。</summary>
    public void RefreshOpening(IReadOnlyList<CardDefinition> cards, Dictionary<string,int> counts, string selected)
    {
        openingIds.Clear(); openingIds.Add(null);
        var labels = new List<string> { "隨機起手（不指定）" };
        int selectedIndex = 0;
        foreach (var card in cards)
        {
            if (card == null || !counts.TryGetValue(card.id, out int n) || n <= 0) continue;
            openingIds.Add(card.id); labels.Add(card.id + "  " + card.cardName);
            if (card.id == selected) selectedIndex = openingIds.Count - 1;
        }
        opening.ClearOptions(); opening.AddOptions(labels); opening.SetValueWithoutNotify(selectedIndex); opening.RefreshShownValue();
    }

    /// <summary>顯示結果張數與空結果提示。</summary>
    public void SetResultCount(int visible, int total)
    { resultCount.text = visible == 0 ? "沒有符合的卡片，請調整篩選" : $"顯示 {visible} / {total} 種卡片"; }
}
