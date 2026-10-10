using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>以固定示範資料展示傷害分組與大量效果排版，只建立 Play Mode UI。</summary>
[InitializeOnLoad]
public static class DamageReadabilityDemo
{
    private const string Key = "ChessRPG.DamageReadabilityDemo";
    private static GameObject root;
    private static TMP_FontAsset font;
    private static double ready;
    private static readonly Color TextColor = new Color(0.93f, 0.96f, 0.97f);
    private static readonly Color Accent = new Color(0.35f, 0.94f, 0.77f);
    private static readonly string[] Sources = { "基礎攻擊", "火焰", "火焰", "火焰", "中毒", "中毒", "中毒", "護甲", "護甲", "詛咒", "詛咒" };
    private static readonly int[] Values = { 8, 2, 2, 2, 2, 2, 2, -3, -3, 1, 1 };

    /// <summary>重載後等待遊戲就緒，離開播放模式時還原編輯器設定。</summary>
    static DamageReadabilityDemo()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Restore;
    }
    /// <summary>預覽重複來源合併前後的對照。</summary>
    [MenuItem("Tools/Chess/Damage Demo/3 Group Repeated Effects")]
    public static void Grouped() { Begin(3); }
    /// <summary>預覽將大量效果集中到側欄的棋盤畫面。</summary>
    [MenuItem("Tools/Chess/Damage Demo/5 Compact Board Layout")]
    public static void Compact() { Begin(5); }
    /// <summary>以同一組資料啟動正式集中結算面板，完成後實際扣除測試對局血量。</summary>
    [MenuItem("Tools/Chess/Damage Demo/Live Unified Panel")]
    public static void Live() { Begin(6); }
    /// <summary>以多個目標同時顯示正式浮動計算 UI。</summary>
    [MenuItem("Tools/Chess/Damage Demo/Parallel Targets")]
    public static void ParallelTargets() { Begin(7); }
    /// <summary>只在離線播放模式啟動展示，保存原本啟動場景。</summary>
    private static void Begin(int mode)
    {
        if (Photon.Pun.PhotonNetwork.IsConnected) return;
        if (EditorApplication.isPlaying)
        {
            if (SessionState.GetBool(Key, false)) Build(mode);
            return;
        }
        SessionState.SetString(Key + "Scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetInt(Key + "Mode", mode);
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + "Built", false);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/ChessScene.unity");
        EditorApplication.isPlaying = true;
    }
    /// <summary>等待棋盤與字型初始化後建立展示。</summary>
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || SessionState.GetBool(Key + "Built", false) || !EditorApplication.isPlaying) return;
        if (Object.FindFirstObjectByType<LogicManager>() == null) return;
        if (ready == 0) { ready = EditorApplication.timeSinceStartup + 2; return; }
        if (EditorApplication.timeSinceStartup < ready) return;
        SessionState.SetBool(Key + "Built", true);
        Build(SessionState.GetInt(Key + "Mode", 3));
    }
    /// <summary>共用同一組十一筆展示資料，依模式切換比較面板或側欄。</summary>
    private static void Build(int mode)
    {
        if (root != null) { root.SetActive(false); Object.Destroy(root); }
        if (mode == 6 || mode == 7)
        {
            LogicManager logic = Object.FindFirstObjectByType<LogicManager>();
            if (logic == null || logic.IsDamageCalculationBusy) return;
            var sequence = new DamageCalculationSequence { attacker = logic.boardMap[4, 1], target = logic.boardMap[4, 6],
                damagedWhitePlayer = false, finalDamage = Values.Sum(), resultStartWorldPosition = logic.boardMap[4, 6].transform.position };
            for (int i = 0; i < Sources.Length; i++) sequence.steps.Add(new DamageCalculationStep { title = Sources[i],
                hasContribution = true, contribution = Values[i], displayText = Mathf.Abs(Values[i]).ToString(),
                worldPosition = logic.boardMap[i % 8, i % 2 == 0 ? 1 : 6].transform.position,
                countingIcon = Values[i] < 0 ? DamageCountingIcon.Defense : DamageCountingIcon.Attack });
            if (mode == 7)
            {
                var sequences = new System.Collections.Generic.List<DamageCalculationSequence>();
                for (int n = 0; n < 3; n++)
                {
                    Piece target = logic.boardMap[n * 3, 6];
                    var item = new DamageCalculationSequence { target = target, finalDamage = sequence.finalDamage,
                        resultStartWorldPosition = target.transform.position };
                    item.steps.AddRange(sequence.steps); sequences.Add(item);
                }
                Object.FindFirstObjectByType<DamageCalculationVisualizer>().PlayBatch(sequences, null, null, null);
            }
            else logic.PlayDamageCalculation(sequence, null, () => new CardBattleSystem(logic).DamagePlayer(false, sequence.finalDamage, false));
            return;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CardPrefabs/CardImage.prefab");
        font = prefab.GetComponentInChildren<TMP_Text>(true).font;
        root = new GameObject("Damage readability demo", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32700;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        if (mode == 3) Comparison(); else BoardLayout();
    }
    /// <summary>將逐筆資料與相同來源分組並排，合併只影響展示且保留加總明細。</summary>
    private static void Comparison()
    {
        Panel(root.transform, "Backdrop", Vector2.zero, new Vector2(2400, 1400), new Color(0, 0, 0, 0.62f));
        RectTransform card = Panel(root.transform, "Comparison", Vector2.zero, new Vector2(1340, 860), new Color(0.035f, 0.055f, 0.07f, 0.98f));
        Label(card, "03  重複效果合併", new Vector2(0, 360), new Vector2(1200, 60), 38, TextColor);
        Label(card, "同一目標、同一結算階段、同一来源才合併 · 展示用資料", new Vector2(0, 305), new Vector2(1200, 40), 23, Accent);
        Label(card, "逐筆呈現  /  11 行", new Vector2(-330, 235), new Vector2(540, 44), 27, TextColor);
        Label(card, "分組呈現  /  5 行", new Vector2(330, 235), new Vector2(540, 44), 27, Accent);
        for (int i = 0; i < Sources.Length; i++)
            Row(card, new Vector2(-330, 183 - i * 39), 550, 34, $"{i + 1:00}   {Sources[i]}", Signed(Values[i]), 21);
        int row = 0;
        foreach (var group in Enumerable.Range(0, Sources.Length).GroupBy(i => Sources[i]))
        {
            RectTransform item = Row(card, new Vector2(330, 166 - row++ * 80), 550, 70,
                group.Key + (group.Count() > 1 ? $" ×{group.Count()}" : ""), Signed(group.Sum(i => Values[i])), 25);
            Label(item, string.Join("  +  ", group.Select(i => Values[i] < 0 ? $"({Values[i]})" : Values[i].ToString())), new Vector2(-100, -19), new Vector2(300, 26), 17, new Color(0.65f, 0.75f, 0.79f));
        }
        Label(card, "最終傷害  16     ·     合併前後相同", new Vector2(0, -300), new Vector2(1160, 60), 32, Accent);
        Label(card, "試作：保留每筆數值供查閱，不更動計算順序與技能觸發。", new Vector2(0, -365), new Vector2(1200, 40), 22, TextColor);
    }
    /// <summary>大量效果收納在固定側欄，棋盤只保留目標與總傷害標記。</summary>
    private static void BoardLayout()
    {
        RectTransform header = Panel(root.transform, "Heading", new Vector2(-285, 410), new Vector2(1040, 128), new Color(0.035f, 0.055f, 0.07f, 0.95f));
        Label(header, "05  大量效果集中顯示", new Vector2(0, 22), new Vector2(980, 46), 32, TextColor);
        Label(header, "棋盤保留目標位置，十一筆明細收進右側五組摘要。", new Vector2(0, -30), new Vector2(980, 35), 21, Accent);
        RectTransform side = Panel(root.transform, "Damage details", new Vector2(740, 0), new Vector2(450, 910), new Color(0.035f, 0.055f, 0.07f, 0.98f));
        Label(side, "傷害結算", new Vector2(0, 391), new Vector2(410, 55), 34, TextColor);
        Label(side, "目標：黑方士兵 e7", new Vector2(0, 336), new Vector2(410, 42), 23, TextColor);
        Label(side, "11 筆效果  /  5 組來源", new Vector2(0, 294), new Vector2(410, 38), 21, Accent);
        int row = 0;
        foreach (var group in Enumerable.Range(0, Sources.Length).GroupBy(i => Sources[i]))
            Row(side, new Vector2(0, 217 - row++ * 75), 402, 61,
                group.Key + (group.Count() > 1 ? $" ×{group.Count()}" : ""), Signed(group.Sum(i => Values[i])), 24);
        Label(side, "最終傷害", new Vector2(0, -203), new Vector2(390, 40), 24, TextColor);
        Label(side, "16", new Vector2(0, -263), new Vector2(390, 70), 58, Accent);
        Label(side, "同來源相加 · 不同來源分列\n更長的明細可沿用此區域捲動", new Vector2(0, -352), new Vector2(402, 70), 20, TextColor);
        LogicManager logic = Object.FindFirstObjectByType<LogicManager>();
        Piece target = logic != null ? logic.boardMap[4, 6] : null;
        if (target != null && Camera.main != null)
        {
            Vector2 screen = Camera.main.WorldToScreenPoint(target.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root.GetComponent<RectTransform>(), screen, null, out Vector2 point);
            RectTransform badge = Panel(root.transform, "Target summary", point + new Vector2(0, 76), new Vector2(172, 75), new Color(0.045f, 0.15f, 0.14f, 0.98f));
            Label(badge, "e7   傷害 16", Vector2.zero, new Vector2(160, 65), 24, Accent);
        }
        RectTransform note = Panel(root.transform, "Demo notice", new Vector2(-260, -418), new Vector2(1090, 62), new Color(0.035f, 0.055f, 0.07f, 0.96f));
        Label(note, "排版試作 · 使用與圖 03 相同的展示數值，尚未接入正式結算", Vector2.zero, new Vector2(1060, 54), 22, TextColor);
    }
    /// <summary>依正負號格式顯示傷害加成或減免。</summary>
    private static string Signed(int value) { return value > 0 ? "+" + value : value.ToString(); }
    /// <summary>建立固定列高的效果摘要，數值獨立靠右便於掃讀。</summary>
    private static RectTransform Row(Transform parent, Vector2 position, float width, float height, string title, string value, float size)
    {
        RectTransform row = Panel(parent, title, position, new Vector2(width, height), new Color(0.095f, 0.13f, 0.16f));
        Label(row, title, new Vector2(-width * 0.12f, height > 65 ? 10 : 0), new Vector2(width * 0.7f, 36), size, TextColor);
        Label(row, value, new Vector2(width * 0.38f, 0), new Vector2(100, 40), size + 2, value.StartsWith("-") ? new Color(0.45f, 0.75f, 1f) : Accent);
        return row;
    }
    /// <summary>建立以畫面中心為座標基準的展示底板。</summary>
    private static RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        Image image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return rect;
    }
    /// <summary>使用正式卡面字型顯示展示文字。</summary>
    private static void Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center; label.color = color; label.raycastTarget = false;
        label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size;
    }
    /// <summary>結束示範後還原原本的播放起始場景。</summary>
    private static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
        string scene = SessionState.GetString(Key + "Scene", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(scene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
        SessionState.SetBool(Key, false); ready = 0; root = null;
    }
}
