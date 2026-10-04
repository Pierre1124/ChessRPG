using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>只在 Play Mode 建立的卡牌目標高亮視覺樣板，不改寫場景或遊戲規則。</summary>
[InitializeOnLoad]
public static class CardTargetHighlightDemo
{
    private const string Key = "ChessRPG.HighlightDemo";
    private static double readyAt;

    /// <summary>重載後接續示範，退出 Play Mode 時還原原本的起始場景。</summary>
    static CardTargetHighlightDemo()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Restore;
    }

    /// <summary>從編輯模式啟動可隨時退出的本機視覺示範。</summary>
    [MenuItem("Tools/Chess/Preview Card Target Highlights")]
    public static void Run()
    {
        Begin(false);
    }

    /// <summary>預覽可使用手牌的柔和外光暈，另留一張未發光的卡作比較。</summary>
    [MenuItem("Tools/Chess/Preview Playable Hand Glow")]
    public static void RunHandGlow()
    {
        Begin(true);
    }

    /// <summary>使用真正的手牌判定與渲染，展開可用及不可用卡牌供目視驗證。</summary>
    [MenuItem("Tools/Chess/Preview Live Hand Glow")]
    public static void RunLiveHandGlow()
    {
        Begin(true);
        SessionState.SetBool(Key + "LiveHand", true);
    }

    /// <summary>在本機 Play Mode 開啟正式設定介面，供版面與即時光暈預覽驗證。</summary>
    [MenuItem("Tools/Chess/Preview Settings (Play Mode)")]
    public static void PreviewSettings()
    {
        if (!EditorApplication.isPlaying) return;
        Object.FindFirstObjectByType<SettingsUI>()?.ShowPanel();
    }

    /// <summary>共用示範啟動流程，保存原設定並選擇視覺樣板。</summary>
    private static void Begin(bool handGlow)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Photon.Pun.PhotonNetwork.IsConnected) return;
        SessionState.SetBool(Key + "HandGlow", handGlow);
        SessionState.SetBool(Key + "LiveHand", false);
        SessionState.SetString(Key + "Scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + "Built", false);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/ChessScene.unity");
        EditorApplication.isPlaying = true;
    }

    /// <summary>等待棋盤建立後製作示範光圈及說明；不送出任何出牌命令。</summary>
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || SessionState.GetBool(Key + "Built", false)) return;
        LogicManager logic = Object.FindFirstObjectByType<LogicManager>();
        if (logic == null || logic.boardMap[4, 1] == null) return;
        if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 2; return; }
        if (EditorApplication.timeSinceStartup < readyAt) return;
        SessionState.SetBool(Key + "Built", true);
        if (SessionState.GetBool(Key + "LiveHand", false)) BuildLiveHand();
        else if (SessionState.GetBool(Key + "HandGlow", false)) BuildHandGlow();
        else Build(logic);
    }

    /// <summary>只在本機示範設定測試手牌，不替換正式 UI 或強制開啟光暈。</summary>
    private static void BuildLiveHand()
    {
        CardHandManager hand = Object.FindFirstObjectByType<CardHandManager>();
        var field = typeof(CardHandManager).GetField("openingHandSize",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        int previous = (int)field.GetValue(hand);
        field.SetValue(hand, 3);
        try { hand.SetDeckForPlayerAsAuthority(true, "J01,E01,F01", true); }
        finally { field.SetValue(hand, previous); }
        HandCardToggle toggle = Object.FindFirstObjectByType<HandCardToggle>();
        if (toggle != null && !toggle.IsExpanded) toggle.Toggle();
    }

    /// <summary>使用現有卡面與預製物呈現兩張可用手牌及一張普通手牌的視覺對照。</summary>
    private static void BuildHandGlow()
    {
        CardHandManager hand = Object.FindFirstObjectByType<CardHandManager>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var originalHand = (RectTransform)typeof(CardHandManager).GetField("handRoot", flags).GetValue(hand);
        originalHand.gameObject.SetActive(false);
        var root = new GameObject("PlayableHandGlowDemo (Play Mode only)", typeof(Canvas), typeof(CanvasScaler));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 32700;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CardPrefabs/CardImage.prefab");
        TMP_FontAsset font = prefab.GetComponentInChildren<TMP_Text>(true).font;
        var cyan = new Color(0.15f, 0.92f, 0.72f);
        Label(root.transform, font, "可使用手牌 · 光暈 DEMO", new Vector2(150, -24), new Vector2(620, 56), 32, Color.white);
        Label(root.transform, font, "青綠光暈＝可以使用　｜　無光暈＝目前不可使用", new Vector2(150, -86), new Vector2(830, 48), 25, cyan);
        Label(root.transform, font, "視覺對照示範，尚未接入可用條件判定", new Vector2(150, -140), new Vector2(720, 42), 21, Color.white);
        Sprite glow = MakeGlowSprite();
        string[] ids = { "J01", "E01", "F01" };
        for (int i = 0; i < ids.Length; i++)
        {
            float x = (i - 1) * 380f;
            if (i < 2)
            {
                var halo = new GameObject("Soft playable glow", typeof(RectTransform), typeof(Image));
                halo.transform.SetParent(root.transform, false);
                var image = halo.GetComponent<Image>();
                image.sprite = glow;
                image.color = cyan;
                image.raycastTarget = false;
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(x, 288f);
                rect.sizeDelta = new Vector2(384, 480);
            }
            GameObject card = Object.Instantiate(prefab, root.transform, false);
            card.name = "Demo " + ids[i];
            CardPresentation.ApplyCardData(card, hand.cardLibrary.GetCard(ids[i]));
            card.GetComponent<CardDragHandler>().enabled = false;
            foreach (Graphic graphic in card.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.localScale = Vector3.one;
            cardRect.anchoredPosition = new Vector2(x, 288f);
            TMP_Text caption = Label(root.transform, font, i < 2 ? "可使用" : "未發光・對照", Vector2.zero, new Vector2(288, 42), 23,
                i < 2 ? cyan : new Color(0.75f, 0.78f, 0.82f));
            var captionRect = (RectTransform)caption.transform.parent;
            captionRect.anchorMin = captionRect.anchorMax = new Vector2(0.5f, 0f);
            captionRect.pivot = new Vector2(0.5f, 0f);
            captionRect.anchoredPosition = new Vector2(x, 30f);
            caption.alignment = TextAlignmentOptions.Center;
        }
    }

    /// <summary>以矩形邊緣距離建立柔和透明光暈，只生成示範用執行階段材質圖。</summary>
    private static Sprite MakeGlowSprite()
    {
        const int width = 384, height = 480;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - width / 2f) - 134f, 0f);
                float dy = Mathf.Max(Mathf.Abs(y - height / 2f) - 182f, 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 0.58f * Mathf.Exp(-distance * distance / (2f * 11f * 11f));
                pixels[y * width + x] = new Color(1, 1, 1, alpha);
            }
        texture.SetPixels(pixels);
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100);
    }

    /// <summary>建立青色合法目標光圈與金色聚焦光圈，保留原本棋子材質。</summary>
    private static void Build(LogicManager logic)
    {
        var root = new GameObject("CardTargetHighlightDemo (Play Mode only)");
        var cyan = new Color(0.12f, 0.86f, 0.94f);
        var gold = new Color(1f, 0.76f, 0.25f);
        for (int x = 0; x < 8; x++)
        {
            Piece pawn = logic.boardMap[x, 1];
            Vector3 center = new Vector3(pawn.transform.position.x, 0.11f, pawn.transform.position.z);
            Ring(root.transform, center, 0.36f, x == 4 ? gold : cyan, x == 4 ? 0.065f : 0.035f);
            if (x == 4) Ring(root.transform, center, 0.44f, gold, 0.018f);
        }
        var canvasObject = new GameObject("Demo legend", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(root.transform);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32700;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        TMP_Text source = Object.FindFirstObjectByType<TMP_Text>();
        TMP_FontAsset font = source != null ? source.font : TMP_Settings.defaultFontAsset;
        Label(canvas.transform, font, "卡牌目標高亮 · 視覺 DEMO", new Vector2(34, -24), new Vector2(700, 54), 32, Color.white);
        Label(canvas.transform, font, "青色光圈：可使用的目標    金色雙圈：目前指向", new Vector2(34, -84), new Vector2(880, 48), 25, cyan);
        Label(canvas.transform, font, "示範情境：拖曳一張以兵為目標的卡牌（尚未套用效果）", new Vector2(34, -136), new Vector2(920, 44), 22, Color.white);
        Canvas.ForceUpdateCanvases();
        Vector3 screen = Camera.main.WorldToViewportPoint(logic.boardMap[4, 1].transform.position + Vector3.up * 1.1f);
        Vector2 anchor = new Vector2(screen.x, screen.y);
        TMP_Text hint = Label(canvas.transform, font, "放開以使用", Vector2.zero, new Vector2(210, 48), 26, gold);
        RectTransform hintPanel = (RectTransform)hint.transform.parent;
        hintPanel.anchorMin = hintPanel.anchorMax = anchor;
        hintPanel.pivot = new Vector2(0.5f, 0f);
        hintPanel.anchoredPosition = Vector2.zero;
        hint.alignment = TextAlignmentOptions.Center;
    }

    /// <summary>以不受燈光影響的線段繪製貼近棋盤的圓環，不使用粒子。</summary>
    private static void Ring(Transform parent, Vector3 center, float radius, Color color, float width)
    {
        var go = new GameObject("Target ring");
        go.transform.SetParent(parent);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        line.sharedMaterial.SetColor("_BaseColor", color);
        line.loop = true;
        line.positionCount = 64;
        line.widthMultiplier = width;
        line.useWorldSpace = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
        }
    }

    /// <summary>建立有深色底板的提示文字，避免遊戲背景影響閱讀。</summary>
    private static TMP_Text Label(Transform parent, TMP_FontAsset font, string text, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var panel = new GameObject("Hint panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = new Color(0.025f, 0.045f, 0.075f, 0.95f);
        panel.GetComponent<Image>().raycastTarget = false;
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(panel.transform, false);
        var tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.margin = new Vector4(14, 0, 14, 0);
        tmp.raycastTarget = false;
        tmp.rectTransform.anchorMin = Vector2.zero;
        tmp.rectTransform.anchorMax = Vector2.one;
        tmp.rectTransform.sizeDelta = Vector2.zero;
        tmp.rectTransform.anchoredPosition = Vector2.zero;
        return tmp;
    }

    /// <summary>退出示範後還原編輯器設定；所有光圈隨 Play Mode 場景自動移除。</summary>
    private static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
        string path = SessionState.GetString(Key + "Scene", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        SessionState.SetBool(Key, false);
        readyAt = 0;
    }
}


