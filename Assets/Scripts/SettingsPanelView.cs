using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>管理場景內可編輯的設定版面；執行期只綁定功能與更新數值。</summary>
public sealed class SettingsPanelView : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private RectTransform content;
    [SerializeField] private TMP_Text volumeText, intensityText, confirmationText;
    [SerializeField] private GameObject modal;
    [SerializeField] private Button accept, cancel;
    [SerializeField] private Button[] styles, colors;
    [SerializeField] private Slider intensity;
    [SerializeField] private SettingsUI owner;
    [SerializeField] private GameObject[] pages;
    [SerializeField] private Button[] tabs, bindingButtons;
    [SerializeField] private Toggle longPress;
    [SerializeField] private TMP_Text bindingHint;
    [SerializeField] private Button[] actionButtons;
    private static readonly string[] ActionNames = { "恢復預設按鍵", "恢復預設光暈", "套用顯示設定", "重新開始", "返回主選單", "完成並返回" };
    private GameControl bindingAction;
    private int captureStartFrame;

    /// <summary>只顯示目前分類，切換時取消未完成的按鍵輸入。</summary>
    private void SelectPage(int index)
    {
        CancelBinding();
        for (int i = 0; i < pages.Length; i++)
        { pages[i].SetActive(i == index); tabs[i].GetComponent<Image>().color = i == index ? Accent : Surface; }
    }

    /// <summary>開始等待下一個按鍵，略過啟動按鈕的當幀輸入。</summary>
    private void BeginBinding(GameControl action)
    {
        bindingAction = action; captureStartFrame = Time.frameCount;
        ControlBindings.Capturing = true;
        bindingHint.text = "請按新的按鍵 · Esc 取消 · 不接受重複綁定";
        bindingButtons[(int)action].GetComponentInChildren<TMP_Text>().text = "等待輸入…";
    }

    /// <summary>更新所有按鍵欄位。</summary>
    private void RefreshBindings()
    {
        if (bindingButtons == null) return;
        for (int i = 0; i < bindingButtons.Length; i++)
            bindingButtons[i].GetComponentInChildren<TMP_Text>().text = ControlBindings.Label((GameControl)i);
    }

    /// <summary>取消重新綁定並抑制同幀的棋盤快捷操作。</summary>
    private void CancelBinding()
    {
        if (!ControlBindings.Capturing) return;
        ControlBindings.Capturing = false; ControlBindings.CaptureFinishedFrame = Time.frameCount;
        RefreshBindings();
        if (bindingHint != null) bindingHint.text = "已取消按鍵設定";
    }

    /// <summary>以 Input System 擷取鍵盤或滑鼠輸入，保留原設定直到合法輸入成功。</summary>
    private void Update()
    {
        if (!ControlBindings.Capturing || Time.frameCount == captureStartFrame) return;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { CancelBinding(); return; }
        int code = 0;
        if (keyboard != null)
            foreach (var key in keyboard.allKeys) if (key.wasPressedThisFrame) { code = (int)key.keyCode; break; }
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.wasPressedThisFrame) code = -1;
            else if (mouse.middleButton.wasPressedThisFrame) code = -2;
            else if (mouse.backButton.wasPressedThisFrame) code = -3;
            else if (mouse.forwardButton.wasPressedThisFrame) code = -4;
        }
        if (code == 0) return;
        if (!ControlBindings.TrySet(bindingAction, code, out string reason)) { bindingHint.text = reason + "；請重試或按 Esc"; return; }
        CancelBinding(); bindingHint.text = "已儲存：" + ControlBindings.ActionName(bindingAction) + " → " + ControlBindings.Label(bindingAction);
    }

    /// <summary>面板停用時清理輸入擷取狀態。</summary>
    private void OnDisable() { CancelBinding(); ControlBindings.SettingsOpen = false; }
    private static readonly Color Surface = new Color(0.12f, 0.17f, 0.22f);
    private static readonly Color Accent = new Color(0.14f, 0.48f, 0.42f);
    public bool IsConfirmationVisible { get { return modal != null && modal.activeSelf; } }

    /// <summary>沿用原本字型與解析度下拉模板，替換舊面板內容。</summary>
    public static SettingsPanelView Build(SettingsUI settings)
    {
        if (Application.isPlaying) throw new InvalidOperationException("設定介面只能在編輯器建立。");
        var existing = settings.panel.GetComponent<SettingsPanelView>();
        if (existing != null) return existing;
        TMP_Text oldText = settings.panel.GetComponentInChildren<TMP_Text>(true);
        TMP_Dropdown template = settings.resolutionDropdown;
        var view = settings.panel.AddComponent<SettingsPanelView>();
        view.owner = settings;
        view.font = oldText != null ? oldText.font : TMP_Settings.defaultFontAsset;
        foreach (Transform child in settings.panel.transform) child.gameObject.SetActive(false);
        if (settings.confirmationPopup != null) settings.confirmationPopup.SetActive(false);
        RectTransform panelRect = settings.panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        Image background = settings.panel.GetComponent<Image>();
        if (background == null) background = settings.panel.AddComponent<Image>();
        background.color = new Color(0.025f, 0.04f, 0.06f, 0.98f);
        background.raycastTarget = true;
        Canvas canvas = settings.panel.GetComponent<Canvas>();
        if (canvas == null) canvas = settings.panel.AddComponent<Canvas>();
        // 對局 UI 同時使用攝影機與覆蓋式 Canvas，獨立覆蓋層才能確保設定在手牌上方。
        panelRect.SetParent(null, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true; canvas.sortingOrder = 30000;
        CanvasScaler scaler = settings.panel.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = settings.panel.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        if (settings.panel.GetComponent<GraphicRaycaster>() == null) settings.panel.AddComponent<GraphicRaycaster>();
        view.content = view.Rect("Settings content", panelRect, Vector2.zero, new Vector2(1000, 760));
        view.CreateControls(template);
        view.CreateConfirmation();
        settings.confirmationPopup = view.modal;
        view.RefreshGlow();
        return view;
    }

    /// <summary>只連接場景內既有控制項，保留 Inspector 編輯的階層、字型與排版。</summary>
    public void Initialize(SettingsUI settings)
    {
        owner = settings;
        for (int i = 0; i < tabs.Length; i++) { int n = i; Bind(tabs[i], () => SelectPage(n)); }
        for (int i = 0; i < bindingButtons.Length; i++) { GameControl action = (GameControl)i; Bind(bindingButtons[i], () => BeginBinding(action)); }
        for (int i = 0; i < styles.Length; i++)
        {
            int n = i;
            Bind(styles[i], () => { CardGlowSettings.Set(n, CardGlowSettings.Palette, CardGlowSettings.Intensity); RefreshGlow(); });
            Bind(colors[i], () => { CardGlowSettings.Set(CardGlowSettings.Style, n, CardGlowSettings.Intensity); RefreshGlow(); });
        }
        longPress.SetIsOnWithoutNotify(ControlBindings.LongPressEnabled);
        longPress.onValueChanged.RemoveAllListeners(); longPress.onValueChanged.AddListener(ControlBindings.SetLongPress);
        intensity.onValueChanged.RemoveAllListeners();
        intensity.onValueChanged.AddListener(value => { CardGlowSettings.Set(CardGlowSettings.Style, CardGlowSettings.Palette, value); RefreshGlow(); });
        BindNamed("恢復預設按鍵", () => { ControlBindings.ResetDefaults(); longPress.SetIsOnWithoutNotify(false); RefreshBindings(); bindingHint.text = "已恢復預設按鍵"; });
        BindNamed("恢復預設光暈", () => { CardGlowSettings.ResetDefaults(); RefreshGlow(); });
        BindNamed("套用顯示設定", owner.ApplyDisplayChanges);
        BindNamed("重新開始", owner.ShowRestartConfirmation); BindNamed("返回主選單", owner.ReturnToStartMenu);
        BindNamed("完成並返回", owner.goBack);
        RefreshBindings(); RefreshGlow(); SelectPage(0); HideConfirmation();
    }

    /// <summary>依保留的物件名稱連接一般操作按鈕。</summary>
    private void BindNamed(string objectName, UnityAction action)
    {
        int index = Array.IndexOf(ActionNames, objectName);
        if (index >= 0 && actionButtons != null && index < actionButtons.Length && actionButtons[index] != null)
            Bind(actionButtons[index], action);
    }

    /// <summary>在編輯器保存按鈕引用，之後修改物件名稱不影響功能。</summary>
    public void BakeActionReferences()
    {
        if (Application.isPlaying) return;
        if (actionButtons != null && actionButtons.Length == ActionNames.Length) return;
        actionButtons = new Button[ActionNames.Length];
        for (int i = 0; i < ActionNames.Length; i++)
            foreach (Button button in content.GetComponentsInChildren<Button>(true))
                if (button.name == ActionNames[i]) { actionButtons[i] = button; break; }
    }

    /// <summary>重綁執行期監聽器，不重建按鈕。</summary>
    private void Bind(Button button, UnityAction action)
    { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); }

    /// <summary>依可用画面等比例縮放，確保低解析度仍可操作所有設定。</summary>
    private void LateUpdate()
    {
        if (content == null) return;
        RectTransform parent = content.parent as RectTransform;
        float scale = Mathf.Min(parent.rect.width / 1040f, parent.rect.height / 800f);
        content.localScale = Vector3.one * Mathf.Max(0.1f, scale);
    }

    /// <summary>建立一般設定、光暈選項、預覽與對局操作。</summary>
    private void CreateControls(TMP_Dropdown template)
    {
        RectTransform root = content;
        Label(root, "設定", new Vector2(0, 330), new Vector2(900, 52), 36);
        string[] categories = { "操作", "音效", "顯示", "卡牌外觀", "對局" };
        pages = new GameObject[categories.Length]; tabs = new Button[categories.Length];
        for (int i = 0; i < categories.Length; i++)
        {
            int index = i;
            tabs[i] = MakeButton(root, categories[i], new Vector2(-380 + 190 * i, 265), new Vector2(174, 48), () => SelectPage(index));
            pages[i] = Rect(categories[i], root, Vector2.zero, new Vector2(950, 470)).gameObject;
        }
        content = pages[0].GetComponent<RectTransform>();
        Label(content, "點擊按鍵欄位，再按新的鍵盤鍵或滑鼠按鍵", new Vector2(0, 185), new Vector2(880, 36), 22);
        bindingButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            GameControl action = (GameControl)i;
            Label(content, ControlBindings.ActionName(action), new Vector2(-210, 110 - i * 68), new Vector2(390, 46), 24);
            bindingButtons[i] = MakeButton(content, "", new Vector2(235, 110 - i * 68), new Vector2(370, 46), () => BeginBinding(action));
        }
        longPress = ToggleRow("啟用長按左鍵查看（1.5 秒）", new Vector2(0, -115));
        longPress.SetIsOnWithoutNotify(ControlBindings.LongPressEnabled);
        longPress.onValueChanged.AddListener(ControlBindings.SetLongPress);
        bindingHint = Label(content, "左鍵：選取／移動 · Esc：取消／關閉（保留）", new Vector2(0, -183), new Vector2(920, 48), 20);
        MakeButton(content, "恢復預設按鍵", new Vector2(0, -250), new Vector2(440, 46), () => {
            ControlBindings.ResetDefaults(); longPress.SetIsOnWithoutNotify(false); RefreshBindings();
            bindingHint.text = "已恢復預設按鍵";
        });
        RefreshBindings();

        content = pages[1].GetComponent<RectTransform>();
        owner.soundToggle = ToggleRow("遊戲音效", new Vector2(0, 115));
        volumeText = Label(content, "音量", new Vector2(0, 30), new Vector2(440, 36), 24);
        owner.volumeSlider = SliderRow(new Vector2(0, -25), 0f, 1f);
        Label(content, "音效與音量即時套用", new Vector2(0, -100), new Vector2(800, 40), 20);

        content = pages[2].GetComponent<RectTransform>();
        owner.cameraRotationToggle = ToggleRow("回合切換時旋轉鏡頭", new Vector2(0, 170));
        Label(content, "解析度", new Vector2(-180, 95), new Vector2(130, 42), 22);
        if (template != null)
        {
            owner.resolutionDropdown = Instantiate(template, content);
            owner.resolutionDropdown.gameObject.SetActive(true);
            owner.resolutionDropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
            SetRect(owner.resolutionDropdown.GetComponent<RectTransform>(), new Vector2(90, 95), new Vector2(292, 44));
            if (owner.resolutionDropdown.captionText != null) owner.resolutionDropdown.captionText.fontSize = 22;
        }
        owner.windowedToggle = ToggleRow("視窗模式", new Vector2(0, 15));
        MakeButton(content, "套用顯示設定", new Vector2(0, -75), new Vector2(440, 46), owner.ApplyDisplayChanges);
        Label(content, "套用後有 12 秒確認，逾時自動還原。", new Vector2(0, -140), new Vector2(800, 40), 20);

        content = pages[3].GetComponent<RectTransform>();
        Label(content, "可使用手牌光暈", new Vector2(-220, 180), new Vector2(460, 40), 26);
        styles = new Button[4]; colors = new Button[4];
        string[] names = { "柔和", "細框", "呼吸", "關閉" };
        string[] palette = { "青綠", "金色", "天藍", "紫色" };
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            styles[i] = MakeButton(content, names[i], new Vector2(-395 + i * 112, 105), new Vector2(104, 44),
                () => { CardGlowSettings.Set(index, CardGlowSettings.Palette, CardGlowSettings.Intensity); RefreshGlow(); });
            colors[i] = MakeButton(content, palette[i], new Vector2(-395 + i * 112, 40), new Vector2(104, 44),
                () => { CardGlowSettings.Set(CardGlowSettings.Style, index, CardGlowSettings.Intensity); RefreshGlow(); });
        }
        intensityText = Label(content, "亮度", new Vector2(-225, -25), new Vector2(440, 28), 20);
        intensity = SliderRow(new Vector2(-225, -70), 0.2f, 1f);
        intensity.onValueChanged.AddListener(value => { CardGlowSettings.Set(CardGlowSettings.Style, CardGlowSettings.Palette, value); RefreshGlow(); });
        RectTransform sample = Rect("Glow preview card", content, new Vector2(255, 10), new Vector2(172, 220));
        sample.gameObject.AddComponent<Image>().color = Surface;
        Label(sample, "可用卡牌", new Vector2(0, 54), new Vector2(160, 40), 25);
        Label(sample, "即時外觀預覽", new Vector2(0, -26), new Vector2(160, 100), 20);
        CardGlowGraphic.Create(sample);
        MakeButton(content, "恢復預設光暈", new Vector2(-225, -160), new Vector2(440, 42),
            () => { CardGlowSettings.ResetDefaults(); RefreshGlow(); });

        content = pages[4].GetComponent<RectTransform>();
        Label(content, "對局操作會再次確認", new Vector2(0, 150), new Vector2(800, 48), 26);
        MakeButton(content, "重新開始", new Vector2(0, 45), new Vector2(440, 52), owner.ShowRestartConfirmation);
        MakeButton(content, "返回主選單", new Vector2(0, -45), new Vector2(440, 52), owner.ReturnToStartMenu);
        content = root;
        MakeButton(root, "完成並返回", new Vector2(0, -337), new Vector2(440, 48), owner.goBack);
        SelectPage(0);
    }

    /// <summary>同步選取樣式、顏色及亮度，關閉光暈時停用無效控制項。</summary>
    private void RefreshGlow()
    {
        for (int i = 0; i < 4; i++)
        {
            styles[i].GetComponent<Image>().color = i == CardGlowSettings.Style ? Accent : Surface;
            colors[i].GetComponent<Image>().color = i == CardGlowSettings.Palette ? Accent : Surface;
            colors[i].interactable = CardGlowSettings.Style != 3;
        }
        intensity.SetValueWithoutNotify(CardGlowSettings.Intensity);
        intensity.interactable = CardGlowSettings.Style != 3;
        intensityText.text = $"亮度  {Mathf.RoundToInt(CardGlowSettings.Intensity * 100f)}%";
    }

    /// <summary>顯示百分比或靜音狀態。</summary>
    public void UpdateVolume(float value, bool enabled)
    { volumeText.text = enabled ? $"音量  {Mathf.RoundToInt(value * 100f)}%" : $"音量  {Mathf.RoundToInt(value * 100f)}%（已靜音）"; }

    /// <summary>建立共用確認層，攔截底下設定項目的點擊。</summary>
    private void CreateConfirmation()
    {
        RectTransform overlay = Rect("Settings confirmation", content, Vector2.zero, new Vector2(1000, 760));
        overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.94f);
        modal = overlay.gameObject;
        confirmationText = Label(overlay, "", new Vector2(0, 60), new Vector2(760, 150), 28);
        accept = MakeButton(overlay, "確認", new Vector2(-165, -70), new Vector2(290, 52), null);
        cancel = MakeButton(overlay, "取消", new Vector2(165, -70), new Vector2(290, 52), null);
        modal.SetActive(false);
    }

    /// <summary>以指定操作及文案顯示確認視窗。</summary>
    public void ShowConfirmation(string message, UnityAction yes, UnityAction no, string yesText, string noText)
    {
        confirmationText.text = message;
        accept.onClick.RemoveAllListeners(); cancel.onClick.RemoveAllListeners();
        accept.onClick.AddListener(yes); cancel.onClick.AddListener(no);
        accept.GetComponentInChildren<TMP_Text>().text = yesText;
        cancel.GetComponentInChildren<TMP_Text>().text = noText;
        modal.SetActive(true); modal.transform.SetAsLastSibling();
    }
    /// <summary>更新顯示設定確認倒數。</summary>
    public void SetConfirmationText(string message) { confirmationText.text = message; }
    /// <summary>隱藏確認層。</summary>
    public void HideConfirmation() { modal.SetActive(false); }

    /// <summary>建立固定設計座標的 UI 容器。</summary>
    private RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>(); SetRect(rect, position, size); return rect;
    }
    /// <summary>統一矩形的錨點、大小與縮放。</summary>
    private void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one; rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    /// <summary>沿用中文字型建立不攔截點擊的說明。</summary>
    private TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var rect = Rect(text, parent, position, size);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = fontSize;
        label.color = new Color(0.92f, 0.95f, 0.96f); label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; return label;
    }
    /// <summary>建立具有可見焦點與點擊回饋的按鈕。</summary>
    private Button MakeButton(Transform parent, string text, Vector2 position, Vector2 size, UnityAction action)
    {
        var rect = Rect(text, parent, position, size);
        var image = rect.gameObject.AddComponent<Image>(); image.color = Surface;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        Label(rect, text, Vector2.zero, size - new Vector2(8, 4), 22);
        if (action != null) button.onClick.AddListener(action); return button;
    }
    /// <summary>建立整列可點擊的開關，勾選方塊清楚顯示目前狀態。</summary>
    private Toggle ToggleRow(string text, Vector2 position)
    {
        var rect = Rect(text, content, position, new Vector2(440, 44));
        Image background = rect.gameObject.AddComponent<Image>(); background.color = Surface;
        Label(rect, text, new Vector2(-24, 0), new Vector2(370, 42), 22);
        RectTransform box = Rect("Check", rect, new Vector2(190, 0), new Vector2(24, 24));
        Image border = box.gameObject.AddComponent<Image>(); border.color = new Color(0.36f, 0.43f, 0.48f); border.raycastTarget = false;
        RectTransform mark = Rect("Enabled", box, Vector2.zero, new Vector2(16, 16));
        Image check = mark.gameObject.AddComponent<Image>(); check.color = new Color(0.3f, 1f, 0.7f); check.raycastTarget = false;
        Toggle toggle = rect.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = background; toggle.graphic = check;
        return toggle;
    }
    /// <summary>建立具備填色及把手的滑桿，支援滑鼠與鍵盤調整。</summary>
    private Slider SliderRow(Vector2 position, float minimum, float maximum)
    {
        RectTransform rect = Rect("Slider", content, position, new Vector2(420, 30));
        Image background = rect.gameObject.AddComponent<Image>(); background.color = Surface;
        RectTransform fill = Rect("Fill", rect, Vector2.zero, new Vector2(420, 8));
        fill.gameObject.AddComponent<Image>().color = Accent;
        RectTransform handle = Rect("Handle", rect, Vector2.zero, new Vector2(20, 30));
        Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(0.6f, 0.95f, 0.85f);
        Slider slider = rect.gameObject.AddComponent<Slider>(); slider.fillRect = fill; slider.handleRect = handle;
        fill.sizeDelta = new Vector2(0, -22);
        handle.sizeDelta = new Vector2(20, 0);
        slider.targetGraphic = handleImage; slider.minValue = minimum; slider.maxValue = maximum;
        return slider;
    }
}
