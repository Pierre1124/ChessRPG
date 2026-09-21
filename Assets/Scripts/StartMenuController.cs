using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenuController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] public string gameSceneName = "ChessScene";
    [SerializeField] public ChessCard cardLibrary;
    [SerializeField] private NetworkSessionLauncher networkSessionLauncher;

    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject startMenuPanel;
    [SerializeField] private bool showMainMenuOnStart = true;

    [Header("Deck Editor")]
    [SerializeField] public GameObject deckEditorPanel;
    [SerializeField] public CanvasGroup deckEditorGroup;
    [SerializeField] public RectTransform cardContent;
    [SerializeField] public GameObject cardTemplate;
    [SerializeField] public TMP_Text deckCountText;
    [SerializeField] public Button saveDeckButton;

    [Header("Display Settings")]
    [SerializeField] public TMP_Dropdown resolutionDropdown;
    [SerializeField] public Toggle windowedToggle;

    [Header("Selection Colors")]
    [SerializeField] private Color selectedColor =
        new Color(0.18f, 0.58f, 0.45f, 1f);
    [SerializeField] private Color unselectedColor =
        new Color(0.18f, 0.2f, 0.24f, 1f);

    private readonly Dictionary<string, int> selectedCounts =
        new Dictionary<string, int>();
    private readonly Dictionary<string, GameObject> cardItems =
        new Dictionary<string, GameObject>();
    private Resolution[] availableResolutions;
    private string openingCardId;
    [SerializeField, Tooltip("選單操作提示，可不指定並由程式建立。")] private TMP_Text statusText;
    private Coroutine statusRoutine;

    /// <summary>顯示選單操作結果；未綁定文字時在 Canvas 下建立不攔截點擊的提示。</summary>
    public void ShowStatusMessage(string message)
    {
        if (statusText == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var label = new GameObject("MenuStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(canvas.transform, false);
            statusText = label.GetComponent<TextMeshProUGUI>();
            if (deckCountText != null) statusText.font = deckCountText.font;
            statusText.fontSize = 24;
            statusText.color = Color.white;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.raycastTarget = false;
            statusText.rectTransform.anchorMin = new Vector2(0.1f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(0.9f, 0f);
            statusText.rectTransform.pivot = new Vector2(0.5f, 0f);
            statusText.rectTransform.anchoredPosition = new Vector2(0f, 20f);
            statusText.rectTransform.sizeDelta = new Vector2(0f, 90f);
        }
        if (statusRoutine != null) StopCoroutine(statusRoutine);
        statusText.text = message;
        statusText.gameObject.SetActive(true);
        statusText.transform.SetAsLastSibling();
        statusRoutine = StartCoroutine(HideStatusMessage());
    }

    /// <summary>使用真實時間隱藏一次性提示，避免暫停時間影響選單顯示。</summary>
    private IEnumerator HideStatusMessage()
    {
        yield return new WaitForSecondsRealtime(6f);
        if (statusText != null) statusText.gameObject.SetActive(false);
        statusRoutine = null;
    }

    /// <summary>
    /// 補齊選單與連線引用，顯示初始面板並綁定畫面設定。
    /// </summary>
    private void Start()
    {
        LogicManager.SetNextGameMode(false);
        if (networkSessionLauncher == null)
        {
            networkSessionLauncher =
                FindFirstObjectByType<NetworkSessionLauncher>();
        }

        ResolveMenuPanels();
        if (showMainMenuOnStart)
        {
            ShowMainMenu();
        }

        BindDisplaySettings();
    }

    /// <summary>
    /// 顯示開始遊戲選單，供玩家選擇對局模式。
    /// </summary>
    public void StartGame()
    {
        ShowStartMenu();
    }

    /// <summary>
    /// 準備牌組後載入本機遊戲場景。
    /// </summary>
    public void StartLocalGame()
    {
        if (cardLibrary == null)
        {
            Debug.LogError("StartMenuController requires ChessCard.");
            return;
        }

        DeckStorage.EnsureDefault(cardLibrary.Cards);
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// 顯示起始選單相關面板。
    /// </summary>
    public void ShowStartMenu()
    {
        ResolveMenuPanels();
        CloseDeckEditor();
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(startMenuPanel, true);
    }

    /// <summary>
    /// 顯示主選單並更新其他面板狀態。
    /// </summary>
    public void ShowMainMenu()
    {
        ResolveMenuPanels();
        CloseDeckEditor();
        SetPanelActive(startMenuPanel, false);
        SetPanelActive(mainMenuPanel, true);
    }

    /// <summary>
    /// 從目前子面板返回主選單。
    /// </summary>
    public void BackToMainMenu()
    {
        ShowMainMenu();
    }

    /// <summary>
    /// 準備牌組並要求啟動 Photon 開房流程。
    /// </summary>
    public void StartHostGame()
    {
        if (!PrepareDeckBeforeGame())
        {
            return;
        }

        if (networkSessionLauncher == null)
        {
            Debug.LogError(
                "StartMenuController requires NetworkSessionLauncher for Host."
            );
            return;
        }

        networkSessionLauncher.SetGameSceneName(gameSceneName);
        networkSessionLauncher.StartHost();
    }

    /// <summary>
    /// 準備牌組並要求加入 Photon 房間。
    /// </summary>
    public void StartClientGame()
    {
        if (!PrepareDeckBeforeGame())
        {
            return;
        }

        if (networkSessionLauncher == null)
        {
            Debug.LogError(
                "StartMenuController requires NetworkSessionLauncher for Client."
            );
            return;
        }

        networkSessionLauncher.StartClient();
    }

    /// <summary>
    /// 進入對局前確認牌組設定已準備完成。
    /// </summary>
    private bool PrepareDeckBeforeGame()
    {
        if (cardLibrary == null)
        {
            Debug.LogError("StartMenuController requires ChessCard.");
            return false;
        }

        DeckStorage.EnsureDefault(cardLibrary.Cards);
        return true;
    }

    /// <summary>
    /// 補齊主選單與子面板的場景引用。
    /// </summary>
    private void ResolveMenuPanels()
    {
        if (mainMenuPanel == null)
        {
            Transform found = FindSceneObject("MainMenu");
            if (found != null) mainMenuPanel = found.gameObject;
        }

        if (startMenuPanel == null)
        {
            Transform found = FindSceneObject("StartMenu");
            if (found != null) startMenuPanel = found.gameObject;
        }
    }

    /// <summary>
    /// 在面板引用有效時設定顯示狀態。
    /// </summary>
    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
        {
            panel.SetActive(active);
        }
    }

    /// <summary>
    /// 依名稱尋找場景物件。
    /// </summary>
    private static Transform FindSceneObject(string objectName)
    {
        GameObject[] roots =
            UnityEngine.SceneManagement.SceneManager
                .GetActiveScene()
                .GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            Transform found = FindChildRecursive(root.transform, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 建立選單中的解析度與視窗模式設定綁定。
    /// </summary>
    private void BindDisplaySettings()
    {
        availableResolutions =
            DisplaySettingsController.PopulateResolutionDropdown(
                resolutionDropdown
            );
        DisplaySettingsController.BindWindowedToggle(windowedToggle);
        ApplyDisplaySettings(false);

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.AddListener(index =>
            {
                ApplyDisplaySettings(true);
            });
        }

        if (windowedToggle != null)
        {
            windowedToggle.onValueChanged.AddListener(isWindowed =>
            {
                ApplyDisplaySettings(true);
            });
        }
    }

    /// <summary>
    /// 依介面選擇套用畫面設定。
    /// </summary>
    private void ApplyDisplaySettings(bool save)
    {
        int index = resolutionDropdown != null
            ? resolutionDropdown.value
            : PlayerPrefs.GetInt(
                DisplaySettingsController.ResolutionIndexKey,
                0
            );
        bool windowed = windowedToggle != null
            ? windowedToggle.isOn
            : DisplaySettingsController.IsWindowed();

        DisplaySettingsController.ApplyResolution(
            index,
            availableResolutions,
            windowed,
            save
        );
    }

    /// <summary>
    /// 載入目前牌組選擇並開啟牌組編輯面板。
    /// </summary>
    public void OpenDeckEditor()
    {
        if (!HasEditorReferences())
        {
            return;
        }

        selectedCounts.Clear();
        DeckStorage.DeckConfig config =
            DeckStorage.LoadConfigOrDefault(cardLibrary.Cards);
        foreach (KeyValuePair<string, int> pair in config.counts)
        {
            if (pair.Value > 0)
            {
                selectedCounts[pair.Key] = pair.Value;
            }
        }
        openingCardId = config.openingCardId;

        deckEditorPanel.SetActive(true);
        deckEditorPanel.transform.SetAsLastSibling();
        deckEditorGroup.alpha = 1f;
        deckEditorGroup.interactable = true;
        deckEditorGroup.blocksRaycasts = true;
        RebuildCardList();
        RefreshSelectionVisuals();
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardContent);
        Canvas.ForceUpdateCanvases();
        ShowStatusMessage("點擊卡片：1 張 → 2 張 → 指定起手 → 移除");
    }

    /// <summary>
    /// 驗證並保存牌組編輯器目前的選擇。
    /// </summary>
    public void SaveDeck()
    {
        if (cardLibrary == null || GetSelectedCopyCount() == 0)
        {
            return;
        }

        DeckStorage.Save(selectedCounts, openingCardId, cardLibrary.Cards);
        CloseDeckEditor();
        ShowStatusMessage("牌組已儲存");
    }

    /// <summary>
    /// 關閉牌組編輯面板並返回選單。
    /// </summary>
    public void CloseDeckEditor()
    {
        if (deckEditorGroup != null)
        {
            deckEditorGroup.alpha = 0f;
            deckEditorGroup.interactable = false;
            deckEditorGroup.blocksRaycasts = false;
        }

        if (deckEditorPanel != null)
        {
            deckEditorPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 依既有編輯規則選取所有可用卡牌。
    /// </summary>
    public void SelectAllCards()
    {
        if (cardLibrary == null) return;

        selectedCounts.Clear();
        foreach (CardDefinition card in cardLibrary.Cards)
        {
            if (card != null) selectedCounts[card.id] = 1;
        }
        openingCardId = null;
        RefreshSelectionVisuals();
    }

    /// <summary>
    /// 清空牌組編輯器中的卡牌選擇。
    /// </summary>
    public void ClearCards()
    {
        selectedCounts.Clear();
        openingCardId = null;
        RefreshSelectionVisuals();
    }

    /// <summary>
    /// 檢查牌組編輯器所需的 UI 引用是否齊全。
    /// </summary>
    private bool HasEditorReferences()
    {
        bool valid = cardLibrary != null && deckEditorPanel != null &&
            deckEditorGroup != null &&
            cardContent != null && cardTemplate != null &&
            deckCountText != null && saveDeckButton != null;

        if (!valid)
        {
            Debug.LogError(
                "StartMenuController deck editor references are incomplete."
            );
        }
        return valid;
    }

    /// <summary>
    /// 依可用卡牌庫重建牌組編輯清單。
    /// </summary>
    private void RebuildCardList()
    {
        foreach (KeyValuePair<string, GameObject> pair in cardItems)
        {
            if (pair.Value != null) Destroy(pair.Value);
        }
        cardItems.Clear();

        foreach (CardDefinition card in cardLibrary.Cards)
        {
            if (card == null) continue;

            GameObject item = Instantiate(cardTemplate, cardContent);
            item.name = $"DeckCard_{card.id}";
            item.SetActive(true);
            ApplyCardData(item.transform, card);

            Button button = item.GetComponent<Button>();
            if (button != null)
            {
                string cardId = card.id;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ToggleCard(cardId));
            }

            cardItems[card.id] = item;
        }
    }

    /// <summary>
    /// 依既有循環規則切換卡片數量或指定起手卡狀態。
    /// </summary>
    private void ToggleCard(string cardId)
    {
        int count = GetSelectedCount(cardId);
        if (openingCardId == cardId)
        {
            selectedCounts.Remove(cardId);
            openingCardId = null;
        }
        else if (count <= 0)
        {
            selectedCounts[cardId] = 1;
        }
        else if (count < DeckStorage.DefaultMaxCopies)
        {
            selectedCounts[cardId] = count + 1;
        }
        else
        {
            openingCardId = cardId;
        }

        RefreshSelectionVisuals();
    }

    /// <summary>
    /// 依目前牌組選擇更新卡片標示與統計。
    /// </summary>
    private void RefreshSelectionVisuals()
    {
        foreach (KeyValuePair<string, GameObject> pair in cardItems)
        {
            int count = GetSelectedCount(pair.Key);
            bool selected = count > 0;
            bool opening = openingCardId == pair.Key;
            Image background = pair.Value.GetComponent<Image>();
            if (background != null)
            {
                background.color = selected
                    ? selectedColor
                    : unselectedColor;
            }

            Transform mark = FindChildRecursive(
                pair.Value.transform,
                "SelectedMark"
            );
            if (mark != null)
            {
                mark.gameObject.SetActive(selected);
            }

            CardDefinition card = FindCard(pair.Key);
            if (card != null)
            {
                SetText(
                    pair.Value.transform,
                    "CardName",
                    FormatDeckCardName(card, count, opening)
                );
            }
        }

        int total = cardLibrary != null ? cardLibrary.Cards.Count : 0;
        int selectedCopies = GetSelectedCopyCount();
        if (deckCountText != null)
        {
            deckCountText.text =
                $"已選 {selectedCopies} 張 / 卡種 {total} | " +
                $"起手：{GetOpeningCardName()}";
        }
        if (saveDeckButton != null)
        {
            saveDeckButton.interactable = selectedCopies > 0;
        }
    }

    /// <summary>
    /// 將卡牌圖像、文字與相關資訊套用到卡片 UI。
    /// </summary>
    private void ApplyCardData(Transform item, CardDefinition card)
    {
        Image cardArt = FindChildComponent<Image>(item, "CardArt");
        if (cardArt != null)
        {
            Sprite art = card.cardImage != null
                ? card.cardImage
                : card.skillImage;
            cardArt.sprite = art;
            cardArt.color = Color.white;
            cardArt.preserveAspect = true;
            cardArt.gameObject.SetActive(art != null);
        }

        SetText(item, "CardName", FormatDeckCardName(card, 0, false));
        SetText(item, "CardType", GetCardTypeLabel(card.cardType));
        SetText(item, "CardDescription", card.description);
    }

    /// <summary>
    /// 取得指定卡號的選取張數，並限制在編輯器允許的範圍。
    /// </summary>
    private int GetSelectedCount(string cardId)
    {
        return selectedCounts.TryGetValue(cardId, out int count)
            ? Mathf.Clamp(count, 0, DeckStorage.DefaultMaxCopies)
            : 0;
    }

    /// <summary>
    /// 取得指定卡號目前選取的張數。
    /// </summary>
    private int GetSelectedCopyCount()
    {
        int count = 0;
        foreach (KeyValuePair<string, int> pair in selectedCounts)
        {
            count += Mathf.Clamp(pair.Value, 0, DeckStorage.DefaultMaxCopies);
        }

        return count;
    }

    /// <summary>
    /// 取得指定起手卡的顯示名稱。
    /// </summary>
    private string GetOpeningCardName()
    {
        CardDefinition openingCard = FindCard(openingCardId);
        return openingCard != null
            ? $"{openingCard.id} {openingCard.cardName}"
            : "未設定";
    }

    /// <summary>
    /// 依卡號尋找卡牌定義。
    /// </summary>
    private CardDefinition FindCard(string cardId)
    {
        if (cardLibrary == null || string.IsNullOrEmpty(cardId))
        {
            return null;
        }

        foreach (CardDefinition card in cardLibrary.Cards)
        {
            if (card != null && card.id == cardId)
            {
                return card;
            }
        }

        return null;
    }

    /// <summary>
    /// 依選取數量與起手卡狀態組合卡片名稱。
    /// </summary>
    private string FormatDeckCardName(
        CardDefinition card,
        int count,
        bool opening
    )
    {
        string prefix = opening ? "★ " : "";
        string countText = count > 0 ? $"  x{count}" : "  x0";
        return $"{prefix}{card.id}  {card.cardName}{countText}";
    }

    /// <summary>
    /// 取得卡牌類型對應的介面文字。
    /// </summary>
    private string GetCardTypeLabel(CardType cardType)
    {
        switch (cardType)
        {
            case CardType.Event:
                return "事件卡";

            case CardType.Field:
                return "場地卡";

            default:
                return "轉職卡";
        }
    }

    /// <summary>
    /// 尋找指定文字元件並更新其顯示內容。
    /// </summary>
    private static void SetText(Transform root, string name, string value)
    {
        TMP_Text text = FindChildComponent<TMP_Text>(root, name);
        if (text != null) text.text = value;
    }

    /// <summary>
    /// 依名稱尋找子物件，再取得所需類型的元件。
    /// </summary>
    private static T FindChildComponent<T>(Transform root, string name)
        where T : Component
    {
        Transform child = FindChildRecursive(root, name);
        return child != null ? child.GetComponent<T>() : null;
    }

    /// <summary>
    /// 依階層順序遞迴尋找指定名稱的 Transform；回傳第一個符合的物件。
    /// </summary>
    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
