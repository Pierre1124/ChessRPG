from pathlib import Path
p=Path('Assets/Scripts/GameFlowUI.cs')
s=p.read_text(encoding='utf-8-sig')
pos=s.index('    /// <summary>')
s=s[:pos]+'''    private GameObject decisionRoot;
    private string decisionKey;
    private LogicManager decisionLogic;

    /// <summary>獨立於設定及勝利面板的確認層，暫停時仍可操作。</summary>
    public static void ShowDecision(string key, string message, Action accept, Action cancel,
        string acceptText = "確認", string cancelText = "取消")
    {
        GameFlowUI ui = ResolveInstance();
        if (ui == null) return;
        ui.CloseDecision();
        ui.decisionKey = key;
        ui.decisionLogic = FindFirstObjectByType<LogicManager>();
        ui.decisionLogic?.PushOperationLock("MatchDecision");
        var root = new GameObject("MatchDecision", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(ui.transform, false);
        ui.decisionRoot = root;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var shade = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        shade.transform.SetParent(root.transform, false);
        var shadeRect = (RectTransform)shade.transform;
        shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one;
        shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
        shade.GetComponent<Image>().color = new Color(0, 0, 0, 0.85f);
        TMP_Text source = ui.alarmTemplate != null ? ui.alarmTemplate.GetComponentInChildren<TMP_Text>(true) : null;
        TMP_FontAsset font = source != null ? source.font : TMP_Settings.defaultFontAsset;
        var textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        var textRect = (RectTransform)textObject.transform;
        textRect.sizeDelta = new Vector2(900, 200); textRect.anchoredPosition = new Vector2(0, 90);
        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.font = font; label.text = message; label.fontSize = 32;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        if (accept != null) CreateActionButton(root.transform, "Accept", acceptText, new Vector2(-190, -100),
            new Vector2(340, 64), font, () => { ui.CloseDecision(); accept(); });
        CreateActionButton(root.transform, "Cancel", cancelText, new Vector2(accept == null ? 0 : 190, -100),
            new Vector2(340, 64), font, () => { ui.CloseDecision(); cancel?.Invoke(); });
    }

    public static void HideDecision(string key)
    {
        if (instance != null && instance.decisionKey == key) instance.CloseDecision();
    }

    private void CloseDecision()
    {
        if (decisionRoot != null) { decisionRoot.SetActive(false); Destroy(decisionRoot); }
        decisionRoot = null;
        decisionKey = null;
        if (decisionLogic != null) decisionLogic.PopOperationLock("MatchDecision");
        decisionLogic = null;
    }

    private void OnDestroy()
    {
        CloseDecision();
        if (instance == this) instance = null;
    }

    /// <summary>新增操作按鈕時沿用現有 TMP 字型，不複製舊的事件監聽。</summary>
    public static Button CreateActionButton(Transform parent, string name, string title, Vector2 position,
        Vector2 size, TMP_FontAsset font, UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        obj.GetComponent<Image>().color = new Color(0.14f, 0.32f, 0.38f);
        var button = obj.GetComponent<Button>();
        button.targetGraphic = obj.GetComponent<Image>();
        button.onClick.AddListener(action);
        var textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(obj.transform, false);
        var textRect = (RectTransform)textObj.transform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 4); textRect.offsetMax = new Vector2(-8, -4);
        var label = textObj.GetComponent<TextMeshProUGUI>();
        label.font = font; label.text = title; label.fontSize = 24;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return button;
    }

'''+s[pos:]
p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/SettingsPanelView.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('        owner = settings;','''        owner = settings;
        if (pages != null && pages.Length > 4 && pages[4].transform.Find("投降") == null)
            MakeButton(pages[4].transform, "投降", new Vector2(0, -130), new Vector2(440, 52), owner.ShowSurrenderConfirmation);''',1)
s=s.replace('        content = root;\n        MakeButton(root, "完成並返回"','        MakeButton(content, "投降", new Vector2(0, -130), new Vector2(440, 52), owner.ShowSurrenderConfirmation);\n        content = root;\n        MakeButton(root, "完成並返回"')
# Bind an existing baked surrender button as well.
s=s.replace('        BindNamed("重新開始", owner.ShowRestartConfirmation);', '''        if (pages != null && pages.Length > 4)
        {
            Transform surrender = pages[4].transform.Find("投降");
            if (surrender != null) Bind(surrender.GetComponent<Button>(), owner.ShowSurrenderConfirmation);
        }
        BindNamed("重新開始", owner.ShowRestartConfirmation);''')
p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/SettingsUI.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('目前棋局進度將會清除。','連線對局需雙方同意才會重開。')
s=s.replace('        PlayerPrefs.Save(); Time.timeScale = 1f; ReleaseLock();\n        if (multiplayerGameController != null) multiplayerGameController.RequestRestartGame();\n        else UnityEngine.SceneManagement.SceneManager.LoadScene("ChessScene");','''        goBack();
        if (multiplayerGameController != null) multiplayerGameController.RequestRestartGame();
        else { Time.timeScale = 1f; UnityEngine.SceneManagement.SceneManager.LoadScene("ChessScene"); }''')
pos=s.index('    /// <summary>返回主選單前')
s=s[:pos]+'''    public void ShowSurrenderConfirmation()
    {
        if (displayPending || logicManager == null || logicManager.IsGameOver) return;
        if (multiplayerGameController != null && !multiplayerGameController.CanGameplayOperate)
        { GameFlowUI.Show("請等待雙方準備完成"); return; }
        view.ShowConfirmation("確定投降？\\n投降後由對方獲勝。", () =>
        {
            goBack();
            if (multiplayerGameController != null) multiplayerGameController.RequestSurrender();
            else logicManager.Surrender(logicManager.isWhiteTurn);
        }, CancelRestart, "確認投降", "繼續對局");
    }

'''+s[pos:]
p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/GameOverUI.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('        if (panel != null)\n        {\n            panel.SetActive(false);','        EnsureReturnButton();\n        if (panel != null)\n        {\n            panel.SetActive(false);',1)
s=s.replace('        Time.timeScale = 1f;\n        MultiplayerGameController flow', '        MultiplayerGameController flow')
s=s.replace('            UnityEngine.SceneManagement.SceneManager.LoadScene("ChessScene");','            Time.timeScale = 1f;\n            UnityEngine.SceneManagement.SceneManager.LoadScene("ChessScene");')
s=s.replace('            Debug.Log("[NetworkGame] Restart blocked while waiting for player.");','            GameFlowUI.Show("對手尚未就緒或已離開，請返回主選單");')
pos=s.index('    /// <summary>\n    /// 判斷目前對局狀態')
s=s[:pos]+'''    private void EnsureReturnButton()
    {
        if (panel == null || panel.transform.Find("ReturnToMenu") != null) return;
        var restart = panel.GetComponentInChildren<UnityEngine.UI.Button>(true);
        if (restart == null) return;
        var rect = (RectTransform)restart.transform;
        var button = GameFlowUI.CreateActionButton(rect.parent, "ReturnToMenu", "返回主選單",
            rect.anchoredPosition + new Vector2(0, -rect.rect.height - 20), rect.sizeDelta,
            winnerText != null ? winnerText.font : TMP_Settings.defaultFontAsset, ReturnToStartMenu);
        var added = (RectTransform)button.transform;
        added.anchorMin = rect.anchorMin; added.anchorMax = rect.anchorMax; added.pivot = rect.pivot;
    }

    public void ReturnToStartMenu()
    {
        MultiplayerGameController flow = GetMultiplayerGameController();
        if (flow != null) flow.RequestReturnToStart();
        else { Time.timeScale = 1f; UnityEngine.SceneManagement.SceneManager.LoadScene("StartScene"); }
    }

'''+s[pos:]
p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Networking/NetworkSessionLauncher.cs')
s=p.read_text(encoding='utf-8-sig').replace('-private-cards-v1','-private-cards-v1-match-flow-v2')
p.write_text(s,encoding='utf-8')
