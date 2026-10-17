using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>把牌組工具列保存至主選單場景，沿用現有卡片與文字設計。</summary>
public static class DeckEditorSetup
{
    /// <summary>建立分類、搜尋及獨立初始卡欄位，保留既有介面與引用。</summary>
    [MenuItem("Tools/Chess/Presentation/Setup Deck Filters")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先退出 Play Mode。");
        var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        string originalPath = original.path;
        if (original.isDirty) EditorSceneManager.SaveScene(original);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/StartScene.unity");
        try
        {
            var menu = Object.FindFirstObjectByType<StartMenuController>(FindObjectsInactive.Include);
            if (menu.deckFilters != null)
            {
                FitLayout(menu);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                return;
            }
            TMP_FontAsset font = menu.deckCountText.font;
            var body = menu.deckEditorPanel.transform.Find("PanelBody");
            var root = new GameObject("Deck filters", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(DeckEditorFilters));
            root.transform.SetParent(body, false); root.transform.SetSiblingIndex(1);
            var layout = root.GetComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childControlWidth = true;
            root.GetComponent<LayoutElement>().preferredHeight = 148;
            var filters = root.GetComponent<DeckEditorFilters>();
            var tabs = Row(root.transform, "Card categories", 40);
            filters.categories = new Button[4];
            string[] names = { "全部", "轉職卡", "事件卡", "場地卡" };
            for (int i = 0; i < names.Length; i++) filters.categories[i] = Button(tabs, names[i], font, 160);
            var searchRow = Row(root.transform, "Search", 44);
            var input = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            input.name = "Card search"; input.transform.SetParent(searchRow, false); Size(input, 600, 44, 1);
            filters.search = input.GetComponent<TMP_InputField>();
            ((TMP_Text)filters.search.placeholder).text = "搜尋卡名、卡號或效果說明";
            foreach (var text in input.GetComponentsInChildren<TMP_Text>(true)) { text.font = font; text.fontSize = 22; }
            filters.selectedOnlyButton = Button(searchRow, "只看已選", font, 180);
            filters.resetButton = Button(searchRow, "清除篩選", font, 180);
            var openingRow = Row(root.transform, "Opening card", 48);
            Label(openingRow, "初始卡牌", font, 150);
            var dropdown = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            dropdown.name = "Opening card choice"; dropdown.transform.SetParent(openingRow, false); Size(dropdown, 500, 48, 1);
            filters.opening = dropdown.GetComponent<TMP_Dropdown>();
            foreach (var text in dropdown.GetComponentsInChildren<TMP_Text>(true)) { text.font = font; text.fontSize = 22; }
            filters.resultCount = Label(openingRow, "", font, 440);
            menu.deckFilters = filters;
            var so = new SerializedObject(menu);
            var main = (GameObject)so.FindProperty("mainMenuPanel").objectReferenceValue;
            if (main != null)
            {
                var label = Label(main.transform, "牌組摘要", font, 900);
                label.name = "Saved deck summary";
                var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.pivot = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(0, 24); rect.sizeDelta = new Vector2(1000, 76);
                label.alignment = TextAlignmentOptions.Center; menu.mainDeckSummary = label;
            }
            var all = body.Find("Footer/SelectAll");
            if (all != null) all.GetComponentInChildren<TMP_Text>(true).text = "全卡庫各選 1 張";
            var close = body.Find("Footer/CloseDeck");
            if (close != null) close.GetComponentInChildren<TMP_Text>(true).text = "返回（不儲存）";
            FitLayout(menu);
            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("output"); File.WriteAllText("output/deck-filter-setup.txt", "PASS: saved editable deck filters, opening selector and main summary.\n");
        }
        finally { if (!string.IsNullOrEmpty(originalPath)) EditorSceneManager.OpenScene(originalPath); }
    }

    /// <summary>讓捲動區使用剩餘空間，固定工具列與操作列，避免小視窗裁切。</summary>
    private static void FitLayout(StartMenuController menu)
    {
        var body = menu.deckEditorPanel.transform.Find("PanelBody");
        body.GetComponent<VerticalLayoutGroup>().childControlHeight = true;
        var scroll = body.Find("CardScrollView").GetComponent<LayoutElement>();
        scroll.minHeight = 0; scroll.preferredHeight = 0; scroll.flexibleHeight = 1;
        foreach (string name in new[] { "Header", "Deck filters", "Footer" })
        {
            var element = body.Find(name).GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight; element.flexibleHeight = 0;
        }
        string[] buttons = { "SelectAll", "ClearDeck", "CloseDeck", "SaveDeck" };
        string[] labels = { "全選", "清空", "取消", "儲存" };
        for (int i = 0; i < buttons.Length; i++)
            body.Find("Footer/" + buttons[i]).GetComponentInChildren<TMP_Text>(true).text = labels[i];
        var filters = menu.deckFilters;
        foreach (var control in new[] { filters.search.gameObject, filters.opening.gameObject })
        {
            control.GetComponent<Image>().color = new Color(.16f, .2f, .26f);
            foreach (var text in control.GetComponentsInChildren<TMP_Text>(true)) text.color = Color.white;
        }
        filters.opening.template.GetComponent<Image>().color = new Color(.16f, .2f, .26f);
        if (menu.mainDeckSummary == null)
        {
            foreach (var candidate in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name != "MainMenu") continue;
                menu.mainDeckSummary = Label(candidate, "牌組摘要", menu.deckCountText.font, 480);
                menu.mainDeckSummary.name = "Saved deck summary";
                menu.mainDeckSummary.alignment = TextAlignmentOptions.Center;
                EditorUtility.SetDirty(menu);
                break;
            }
        }
        if (menu.mainDeckSummary != null)
        {
            var summary = menu.mainDeckSummary;
            summary.GetComponent<LayoutElement>().ignoreLayout = true;
            summary.rectTransform.anchorMin = summary.rectTransform.anchorMax = new Vector2(.5f, .5f);
            summary.rectTransform.pivot = new Vector2(.5f, .5f);
            summary.rectTransform.anchoredPosition = new Vector2(600, 0);
            summary.rectTransform.sizeDelta = new Vector2(480, 180);
        }
    }

    /// <summary>建立工具列的一行，讓寬度隨原面板配置。</summary>
    private static Transform Row(Transform parent, string name, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)); go.transform.SetParent(parent, false);
        var group = go.GetComponent<HorizontalLayoutGroup>(); group.spacing = 10; group.childControlWidth = true; group.childControlHeight = true; group.childForceExpandWidth = false;
        go.GetComponent<LayoutElement>().preferredHeight = height; return go.transform;
    }

    /// <summary>指定新控制項的布局尺寸。</summary>
    private static void Size(GameObject go, float width, float height, float flex = 0)
    { var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>(); le.preferredWidth = width; le.preferredHeight = height; le.flexibleWidth = flex; }

    /// <summary>建立只用於新增工具列的文字，不修改既有卡片字級。</summary>
    private static TMP_Text Label(Transform parent, string value, TMP_FontAsset font, float width)
    {
        var go = new GameObject(value.Length > 0 ? value : "Result count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var text = go.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 22; text.text = value; text.raycastTarget = false; text.alignment = TextAlignmentOptions.MidlineLeft;
        Size(go, width, 44); return text;
    }

    /// <summary>建立分類與篩選按鈕，事件由場景控制器於開啟時綁定。</summary>
    private static Button Button(Transform parent, string value, TMP_FontAsset font, float width)
    {
        var go = new GameObject(value, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(.16f,.2f,.26f); Size(go,width,44);
        var label = Label(go.transform,value,font,width); label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }
}
