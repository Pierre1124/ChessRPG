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

    private void Start()
    {
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

    public void StartGame()
    {
        ShowStartMenu();
    }

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

    public void ShowStartMenu()
    {
        ResolveMenuPanels();
        CloseDeckEditor();
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(startMenuPanel, true);
    }

    public void ShowMainMenu()
    {
        ResolveMenuPanels();
        CloseDeckEditor();
        SetPanelActive(startMenuPanel, false);
        SetPanelActive(mainMenuPanel, true);
    }

    public void BackToMainMenu()
    {
        ShowMainMenu();
    }

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

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null && panel.activeSelf != active)
        {
            panel.SetActive(active);
        }
    }

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
    }

    public void SaveDeck()
    {
        if (cardLibrary == null || GetSelectedCopyCount() == 0)
        {
            return;
        }

        DeckStorage.Save(selectedCounts, openingCardId, cardLibrary.Cards);
        CloseDeckEditor();
    }

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

    public void ClearCards()
    {
        selectedCounts.Clear();
        openingCardId = null;
        RefreshSelectionVisuals();
    }

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

    private int GetSelectedCount(string cardId)
    {
        return selectedCounts.TryGetValue(cardId, out int count)
            ? Mathf.Clamp(count, 0, DeckStorage.DefaultMaxCopies)
            : 0;
    }

    private int GetSelectedCopyCount()
    {
        int count = 0;
        foreach (KeyValuePair<string, int> pair in selectedCounts)
        {
            count += Mathf.Clamp(pair.Value, 0, DeckStorage.DefaultMaxCopies);
        }

        return count;
    }

    private string GetOpeningCardName()
    {
        CardDefinition openingCard = FindCard(openingCardId);
        return openingCard != null
            ? $"{openingCard.id} {openingCard.cardName}"
            : "未設定";
    }

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

    private static void SetText(Transform root, string name, string value)
    {
        TMP_Text text = FindChildComponent<TMP_Text>(root, name);
        if (text != null) text.text = value;
    }

    private static T FindChildComponent<T>(Transform root, string name)
        where T : Component
    {
        Transform child = FindChildRecursive(root, name);
        return child != null ? child.GetComponent<T>() : null;
    }

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
