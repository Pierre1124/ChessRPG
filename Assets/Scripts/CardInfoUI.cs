using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardInfoUI : MonoBehaviour
{
    [SerializeField] private Transform infoRoot;
    [SerializeField] private Transform cardEquipRoot;
    [SerializeField] private GameObject cardPreviewPrefab;
    [SerializeField] private TMP_Text cardEquipText;
    [SerializeField] private Image chessTypeImage;
    [SerializeField] private Transform chessStatusRoot;
    [SerializeField] private TMP_Text chessAtkText;
    [SerializeField] private TMP_Text chessValueText;
    [SerializeField] public StatusInfoTooltip statusTooltip;
    [SerializeField] public ChessCard imageDatabase;
    [SerializeField] private Vector2 statusIconSize =
        new Vector2(64f, 64f);
    [SerializeField] private Color modifiedStatColor =
        new Color32(143, 217, 255, 255);

    private LogicManager logicManager;
    private Piece inspectedPiece;
    [SerializeField] private Button activeSkillButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text activeSkillReason;
    private MultiplayerGameController multiplayer;
    private GameObject cardPreviewObject;
    private readonly List<GameObject> spawnedStatusIcons =
        new List<GameObject>();

    /// <summary>
    /// 設定對局引用並隱藏初始資訊及狀態提示。
    /// </summary>
    public void Initialize(LogicManager owner)
    {
        logicManager = owner;
        multiplayer = FindFirstObjectByType<MultiplayerGameController>();

        if (infoRoot == null)
        {
            infoRoot = transform;
        }

        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (activeSkillButton != null) activeSkillButton.onClick.AddListener(UseActiveSkill);
        HideStatusTooltip();
        Hide();
    }

    /// <summary>
    /// 顯示指定棋子的裝備、狀態與有效屬性。
    /// </summary>
    public void Show(Piece piece)
    {
        if (piece == null || (logicManager != null && logicManager.IsClassicChess))
        {
            Hide();
            return;
        }

        if (infoRoot == null)
        {
            infoRoot = transform;
        }

        CardDefinition equippedCard = piece.cardDefinition;
        inspectedPiece = piece;
        List<StatusRuntime> visibleStatuses =
            GetVisibleStatuses(piece);

        ApplyCardEquip(equippedCard);
        ApplyChessType(piece);
        RenderStatusIcons(visibleStatuses);
        SetText(
            chessAtkText,
            FormatStatText("ATK", piece.Attack, GetEffectiveAttack(piece))
        );
        SetText(
            chessValueText,
            FormatStatText("Value", piece.Value, GetEffectiveValue(piece))
        );

        infoRoot.gameObject.SetActive(true);
        RefreshSkillButton();
    }

    /// <summary>
    /// 清除預覽及狀態圖示並隱藏棋子資訊面板。
    /// </summary>
    public void Hide()
    {
        inspectedPiece = null;
        HideStatusTooltip();
        ClearCardPreview();
        ClearStatusIcons();

        if (infoRoot != null)
        {
            infoRoot.gameObject.SetActive(false);
        }
    }

    /// <summary>建立固定側邊資訊面板與操作列，避免遮住棋盤中央。</summary>
    public void BakeControls()
    {
        if (activeSkillButton != null) return;
        if (Application.isPlaying) return;
        if (infoRoot == null) infoRoot = transform;
        RectTransform panel = infoRoot as RectTransform;
        if (panel != null)
        {
            panel.anchorMin = panel.anchorMax = new Vector2(1, 0.5f);
            panel.pivot = new Vector2(1, 0.5f);
            panel.anchoredPosition = new Vector2(-24, 25);
        }
        Button close = CreateButton("Close piece info", "關閉 ×", new Vector2(-65, -24), new Vector2(110, 40), new Vector2(1, 1));
        closeButton = close;
        activeSkillButton = CreateButton("Piece active skill", "主動技：關閉電網", new Vector2(0, -30), new Vector2(410, 48), new Vector2(0.5f, 0));
        activeSkillButton.onClick.AddListener(UseActiveSkill);
        var textObject = new GameObject("Active skill availability", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(infoRoot, false);
        activeSkillReason = textObject.GetComponent<TextMeshProUGUI>();
        activeSkillReason.font = chessAtkText != null ? chessAtkText.font : TMP_Settings.defaultFontAsset;
        activeSkillReason.fontSize = 18; activeSkillReason.alignment = TextAlignmentOptions.Center;
        activeSkillReason.raycastTarget = false;
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, -76); rect.sizeDelta = new Vector2(430, 40);
    }

    /// <summary>以完整 UGUI 元件建立按鈕並沿用專案中文字型。</summary>
    private Button CreateButton(string objectName, string label, Vector2 position, Vector2 size, Vector2 anchor)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(infoRoot, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        go.GetComponent<Image>().color = new Color32(32, 73, 91, 255);
        var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(go.transform, false);
        var text = labelObject.GetComponent<TextMeshProUGUI>(); text.text = label;
        text.font = chessAtkText != null ? chessAtkText.font : TMP_Settings.defaultFontAsset;
        text.fontSize = 22; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        var textRect = text.rectTransform; textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        return button;
    }

    /// <summary>即時顯示技能可用性；檢視敵方或等待回合時不提供可執行按鈕。</summary>
    private void RefreshSkillButton()
    {
        if (activeSkillButton == null) return;
        bool hasSkill = PieceActiveSkill.HasSkill(inspectedPiece);
        activeSkillButton.gameObject.SetActive(hasSkill);
        string reason = ControlBindings.Label(GameControl.InspectPiece) + " 查看棋子 · Esc 關閉";
        bool canUse = false;
        if (hasSkill)
        {
            canUse = PieceActiveSkill.CanUse(logicManager, inspectedPiece, inspectedPiece.IsWhite, out reason);
            if (multiplayer != null && !multiplayer.CanLocalPlayerAct(inspectedPiece.IsWhite))
            { canUse = false; reason = "只能操作自己回合的棋子"; }
            if (canUse) reason = "關閉後可移動；下次我方回合重新判定";
        }
        activeSkillButton.interactable = canUse;
        activeSkillReason.text = reason;
    }

    /// <summary>送交主機驗證或執行本機技能，避免按鈕直接繞過規則。</summary>
    private void UseActiveSkill()
    {
        if (inspectedPiece == null) return;
        RefreshSkillButton();
        if (!activeSkillButton.interactable) return;
        if (multiplayer != null && multiplayer.IsOnline)
            multiplayer.SubmitCommand(multiplayer.CreateActiveSkillCommand(inspectedPiece));
        else if (PieceActiveSkill.TryUse(logicManager, inspectedPiece, inspectedPiece.IsWhite))
            GameFlowUI.Show("電網已關閉，可移動城堡");
        RefreshSkillButton();
    }

    /// <summary>棋子移除或切換普通棋局時關閉面板，持續更新技能鎖定原因。</summary>
    private void Update()
    {
        if (inspectedPiece == null || logicManager == null || logicManager.IsClassicChess) { Hide(); return; }
        RefreshSkillButton();
    }

    /// <summary>
    /// 更新棋子裝備卡牌的預覽資訊。
    /// </summary>
    private void ApplyCardEquip(CardDefinition equippedCard)
    {
        ClearCardPreview();

        if (equippedCard != null && cardEquipRoot != null)
        {
            if (cardPreviewPrefab == null)
            {
                Debug.LogError(
                    "CardInfoUI requires Card Preview Prefab to show equipped cards."
                );
            }
            else
            {
                cardPreviewObject =
                    Instantiate(cardPreviewPrefab, cardEquipRoot);
                cardPreviewObject.name = equippedCard.cardName;

                RectTransform previewRect =
                    cardPreviewObject.GetComponent<RectTransform>();

                if (previewRect != null)
                {
                    previewRect.anchorMin = new Vector2(0.5f, 0.5f);
                    previewRect.anchorMax = new Vector2(0.5f, 0.5f);
                    previewRect.anchoredPosition = Vector2.zero;
                    previewRect.localScale = Vector3.one;
                }

                ApplyCardPreviewData(cardPreviewObject, equippedCard);
                DisablePreviewInteraction(cardPreviewObject);
            }
        }

        if (cardEquipText != null)
        {
            cardEquipText.text = equippedCard != null
                ? ""
                : "\u5C1A\u672A\u88DD\u5099\u5361\u7247";
        }
    }

    /// <summary>
    /// 清除目前卡牌預覽的顯示內容。
    /// </summary>
    private void ClearCardPreview()
    {
        if (cardPreviewObject != null)
        {
            Destroy(cardPreviewObject);
            cardPreviewObject = null;
        }
    }

    /// <summary>
    /// 將卡牌資料套用到資訊面板的預覽物件。
    /// </summary>
    private void ApplyCardPreviewData(
        GameObject cardObject,
        CardDefinition card
    )
    {
        Image image = cardObject.GetComponent<Image>();

        if (image == null)
        {
            image = FindChildComponent<Image>(
                cardObject.transform,
                "CardImage"
            );
        }

        if (image != null && card.cardImage != null)
        {
            image.sprite = card.cardImage;
        }

        SetText(
            FindChildComponent<TMP_Text>(
                cardObject.transform,
                "CardNameText"
            ),
            card.cardName
        );

        Transform cardLord =
            FindChildRecursive(cardObject.transform, "CardLord");

        if (cardLord != null)
        {
            SetText(
                FindChildComponent<TMP_Text>(
                    cardLord,
                    "CardLordText"
                ),
                card.description
            );
        }

        ApplyTags(cardObject.transform, card.tags);
    }

    /// <summary>
    /// 將卡牌標籤套用到對應的 UI 文字。
    /// </summary>
    private void ApplyTags(Transform root, List<string> tags)
    {
        Transform tagGroup = FindChildRecursive(root, "CardTagGroup");
        Transform template =
            tagGroup != null
                ? FindChildRecursive(tagGroup, "CardTag")
                : FindChildRecursive(root, "CardTag");

        if (template == null || template.parent == null)
        {
            return;
        }

        for (int i = template.parent.childCount - 1; i >= 0; i--)
        {
            Transform child = template.parent.GetChild(i);

            if (child != template && child.name == "CardTag")
            {
                Destroy(child.gameObject);
            }
        }

        if (tags == null || tags.Count == 0)
        {
            template.gameObject.SetActive(false);
            return;
        }

        for (int i = 0; i < tags.Count; i++)
        {
            Transform tagTransform =
                i == 0
                    ? template
                    : Instantiate(
                        template.gameObject,
                        template.parent
                    ).transform;

            tagTransform.name = "CardTag";
            tagTransform.gameObject.SetActive(true);

            TMP_Text text =
                tagTransform.GetComponentInChildren<TMP_Text>(true);

            if (text != null)
            {
                text.text = tags[i];
            }
        }
    }

    /// <summary>
    /// 停用預覽卡片的拖曳與操作元件。
    /// </summary>
    private void DisablePreviewInteraction(GameObject cardObject)
    {
        CardDragHandler dragHandler =
            cardObject.GetComponent<CardDragHandler>();

        if (dragHandler != null)
        {
            dragHandler.enabled = false;
        }

        foreach (Graphic graphic in
            cardObject.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }

    /// <summary>
    /// 依棋子種類更新資訊面板的顯示。
    /// </summary>
    private void ApplyChessType(Piece piece)
    {
        if (chessTypeImage == null)
        {
            return;
        }

        Sprite sprite = imageDatabase != null
            ? imageDatabase.GetPieceSprite(piece)
            : null;

        chessTypeImage.sprite = sprite;
        chessTypeImage.enabled = sprite != null;
    }

    /// <summary>
    /// 依棋子目前可見狀態建立狀態圖示。
    /// </summary>
    private void RenderStatusIcons(List<StatusRuntime> statuses)
    {
        ClearStatusIcons();

        if (chessStatusRoot == null || statuses.Count == 0)
        {
            return;
        }

        Sprite fallbackSprite = imageDatabase != null
            ? imageDatabase.DefaultStatusSprite
            : null;

        foreach (StatusRuntime status in statuses)
        {
            if (status == null || status.definition == null)
            {
                continue;
            }

            Sprite sprite = status.definition.icon != null
                ? status.definition.icon
                : fallbackSprite;

            if (sprite == null)
            {
                continue;
            }

            GameObject iconObject = SceneObjectTemplates.Spawn("Status icon", chessStatusRoot);
            iconObject.name = status.definition.statusName;

            RectTransform rectTransform =
                iconObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = statusIconSize;

            Image image = iconObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;

            StatusIconHover hover =
                iconObject.GetComponent<StatusIconHover>();
            hover.Initialize(this, status);

            spawnedStatusIcons.Add(iconObject);
        }
    }

    /// <summary>
    /// 顯示指定狀態的詳細提示。
    /// </summary>
    public void ShowStatusTooltip(
        StatusRuntime status,
        Vector2 screenPosition
    )
    {
        if (statusTooltip == null || status == null ||
            status.definition == null)
        {
            return;
        }

        statusTooltip.Show(
            BuildStatusTooltipContent(status),
            screenPosition
        );
    }

    /// <summary>
    /// 隱藏狀態詳細提示。
    /// </summary>
    public void HideStatusTooltip()
    {
        if (statusTooltip != null)
        {
            statusTooltip.Hide();
        }
    }

    /// <summary>
    /// 組合狀態名稱、來源與效果的提示文字。
    /// </summary>
    private string BuildStatusTooltipContent(StatusRuntime status)
    {
        CardDefinition card = GetStatusSourceCard(status);
        StringBuilder builder = new StringBuilder();
        builder.Append(status.definition.statusName);

        if (card != null && !string.IsNullOrWhiteSpace(card.description))
        {
            builder.Append("\n\u6548\u679c\uff1a");
            builder.Append(card.description);
        }

        string currentEffect = GetCurrentStatusEffect(status, card);
        if (!string.IsNullOrEmpty(currentEffect))
        {
            builder.Append("\n\u76ee\u524d\u6548\u679c\uff1a");
            builder.Append(currentEffect);
        }

        if (status.definition.durationTurns > 0)
        {
            builder.Append("\n\u5269\u9918\u56de\u5408\uff1a");
            builder.Append(status.remainingTurns);
        }

        if (status.UsesCharges)
        {
            builder.Append("\n\u5145\u80fd\uff1a");
            builder.Append(status.charges);
            builder.Append('/');
            builder.Append(status.definition.maxCharges);
        }

        builder.Append("\n\u4f86\u6e90\uff1a");
        builder.Append(GetStatusSourceText(status));
        return builder.ToString();
    }

    /// <summary>
    /// 取得狀態目前對應的效果資料。
    /// </summary>
    private string GetCurrentStatusEffect(
        StatusRuntime status,
        CardDefinition card
    )
    {
        if (card != null && card.id == "J10")
        {
            Piece source = status.source != null
                ? status.source
                : status.owner;
            CardRuntimeState runtime = source != null
                ? source.CardRuntime
                : null;
            int captures = runtime != null ? runtime.skillCounterA : 0;
            int friendlyLosses = runtime != null ? runtime.skillCounterB : 0;
            int bonus = card.friendlyDamageBonus + Mathf.Max(0, captures - friendlyLosses);
            return $"我方造成傷害 +{bonus}" +
                $"\n\u8c9e\u5fb7\u5403\u5b50\uff1a{captures}\uff0c" +
                $"\u6211\u65b9\u68cb\u5b50\u88ab\u5403\uff1a{friendlyLosses}";
        }

        if (card != null && card.id == "J06")
        {
            return "\u4e5d\u5bae\u683c\u5167\u6211\u65b9\u68cb\u5b50\u53d7\u5230\u50b7\u5bb3 -1";
        }

        if (card != null && card.id == "J09")
        {
            return $"我方造成傷害 +{card.friendlyDamageBonus}；我方無法回復 HP";
        }

        StringBuilder builder = new StringBuilder();
        foreach (CardEffectData effect in status.definition.effects)
        {
            string line = DescribeEffect(effect);
            if (string.IsNullOrEmpty(line)) continue;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(line);
        }
        return builder.ToString();
    }

    /// <summary>
    /// 將效果類型與數值轉成可閱讀的說明。
    /// </summary>
    private string DescribeEffect(CardEffectData effect)
    {
        if (effect == null) return string.Empty;

        switch (effect.effectType)
        {
            case CardEffectType.ModifyDamageDealt:
                return effect.operation == CardValueOperation.Set
                    ? $"\u9020\u6210\u50b7\u5bb3\u6539\u70ba {effect.value}"
                    : $"\u9020\u6210\u50b7\u5bb3 {FormatSigned(effect.value)}";
            case CardEffectType.ModifyDamageTaken:
                return $"\u53d7\u5230\u50b7\u5bb3 {FormatSigned(effect.value)}";
            case CardEffectType.ModifyOwnerPlayerDamage:
                return $"\u6240\u5c6c\u73a9\u5bb6\u53d7\u5230\u50b7\u5bb3 {FormatSigned(effect.value)}";
            case CardEffectType.ModifyAttack:
                return $"\u653b\u64ca\u529b {FormatSigned(effect.value)}";
            case CardEffectType.ModifyFriendlyAttack:
                return $"\u6211\u65b9\u6240\u6709\u68cb\u5b50\u653b\u64ca\u529b {FormatSigned(effect.value)}";
            case CardEffectType.ModifyValue:
                return $"Value {FormatSigned(effect.value)}";
            case CardEffectType.ModifyFriendlyValue:
                return $"\u6211\u65b9\u6240\u6709\u68cb\u5b50 Value {FormatSigned(effect.value)}";
            case CardEffectType.DamagePlayer:
                return $"\u9020\u6210 {effect.value} \u9ede HP \u50b7\u5bb3";
            case CardEffectType.HealPlayer:
                return $"\u56de\u5fa9 {effect.value} \u9ede HP";
            case CardEffectType.PreventMovement:
                return "\u7121\u6cd5\u79fb\u52d5";
            case CardEffectType.IgnoreDeathEffect:
                return "\u5ffd\u7565\u6b7b\u4ea1\u6548\u679c";
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// 取得狀態所屬的來源卡牌。
    /// </summary>
    private CardDefinition GetStatusSourceCard(StatusRuntime status)
    {
        if (status == null || status.definition == null) return null;

        Piece source = status.source != null ? status.source : status.owner;
        CardDefinition sourceCard = source != null &&
            source.CardRuntime != null
            ? source.CardRuntime.definition
            : null;

        if (sourceCard != null &&
            sourceCard.id == status.definition.sourceCardId)
        {
            return sourceCard;
        }

        return imageDatabase != null
            ? imageDatabase.GetCard(status.definition.sourceCardId)
            : null;
    }

    /// <summary>
    /// 產生狀態來源的顯示文字。
    /// </summary>
    private string GetStatusSourceText(StatusRuntime status)
    {
        if (status.hasSourcePlayer)
        {
            return status.sourcePlayerIsWhite
                ? "\u767d\u65b9\u73a9\u5bb6"
                : "\u9ed1\u65b9\u73a9\u5bb6";
        }

        Piece source = status.source != null
            ? status.source
            : status.owner;
        if (source == null) return "\u672a\u77e5";

        Vector2 coordinates = source.GetCoordinates();
        return $"{(source.IsWhite ? "\u767d\u65b9" : "\u9ed1\u65b9")} " +
            $"{GetPieceTypeName(source)} " +
            $"({coordinates.x:0},{coordinates.y:0})";
    }

    /// <summary>
    /// 取得棋子種類的顯示名稱。
    /// </summary>
    private string GetPieceTypeName(Piece piece)
    {
        if (piece is Pawn) return "\u5c0f\u5175";
        if (piece is Bishop) return "\u4e3b\u6559";
        if (piece is King) return "\u570b\u738b";
        if (piece is Knight) return "\u9a0e\u58eb";
        if (piece is Queen) return "\u7687\u540e";
        if (piece is Rook) return "\u57ce\u5821";
        return string.IsNullOrEmpty(piece.PieceType)
            ? piece.GetType().Name
            : piece.PieceType;
    }

    /// <summary>
    /// 將數值格式化為包含正負號的文字。
    /// </summary>
    private string FormatSigned(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

    /// <summary>
    /// 取得應顯示於棋子資訊面板的狀態清單。
    /// </summary>
    private List<StatusRuntime> GetVisibleStatuses(Piece piece)
    {
        List<StatusRuntime> statuses = new List<StatusRuntime>();

        foreach (StatusRuntime status in piece.Statuses)
        {
            if (IsVisibleStatus(status))
            {
                statuses.Add(status);
            }
        }

        if (logicManager == null)
        {
            return statuses;
        }

        for (int x = 0; x < logicManager.boardMap.GetLength(0); x++)
        {
            for (int y = 0; y < logicManager.boardMap.GetLength(1); y++)
            {
                Piece source = logicManager.boardMap[x, y];

                if (
                    source == null ||
                    source == piece ||
                    source.IsWhite != piece.IsWhite
                )
                {
                    continue;
                }

                foreach (StatusRuntime status in source.Statuses)
                {
                    if (
                        IsVisibleStatus(status) &&
                        IsAuraStatus(status) &&
                        DoesAuraAffectPiece(source, piece)
                    )
                    {
                        statuses.Add(status);
                    }
                }
            }
        }

        return statuses;
    }

    /// <summary>
    /// 判斷指定狀態是否為光環效果。
    /// </summary>
    private bool IsAuraStatus(StatusRuntime status)
    {
        if (status == null || status.definition == null)
        {
            return false;
        }

        if (status.definition.kind == StatusKind.Aura)
        {
            return true;
        }

        foreach (CardEffectData effect in status.definition.effects)
        {
            if (
                effect != null &&
                effect.effectType == CardEffectType.ModifyFriendlyAttack
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 依陣營與效果條件判斷光環是否影響目標棋子。
    /// </summary>
    private bool DoesAuraAffectPiece(Piece source, Piece target)
    {
        CardDefinition sourceCard = source != null && source.CardRuntime != null
            ? source.CardRuntime.definition
            : null;

        if (sourceCard != null && sourceCard.id == "J06")
        {
            return CardSkill.IsInNineGrid(source, target);
        }

        return true;
    }

    /// <summary>
    /// 判斷指定狀態是否應呈現在資訊面板。
    /// </summary>
    private bool IsVisibleStatus(StatusRuntime status)
    {
        return
            status != null &&
            status.definition != null &&
            status.HasCharges;
    }

    /// <summary>
    /// 清除先前建立的狀態圖示。
    /// </summary>
    private void ClearStatusIcons()
    {
        HideStatusTooltip();
        for (int i = spawnedStatusIcons.Count - 1; i >= 0; i--)
        {
            if (spawnedStatusIcons[i] != null)
            {
                Destroy(spawnedStatusIcons[i]);
            }
        }

        spawnedStatusIcons.Clear();
    }

    /// <summary>
    /// 依名稱尋找子物件，再取得所需類型的元件。
    /// </summary>
    private T FindChildComponent<T>(
        Transform root,
        string childName
    ) where T : Component
    {
        Transform child = FindChildRecursive(root, childName);

        if (child == null)
        {
            return null;
        }

        T component = child.GetComponent<T>();
        return component != null
            ? component
            : child.GetComponentInChildren<T>(true);
    }

    /// <summary>
    /// 依階層順序遞迴尋找指定名稱的 Transform；回傳第一個符合的物件。
    /// </summary>
    private Transform FindChildRecursive(
        Transform root,
        string childName
    )
    {
        if (root.name == childName)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            Transform match = FindChildRecursive(child, childName);

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// 取得棋子套用目前卡牌、狀態與場地修正後的攻擊力。
    /// </summary>
    private int GetEffectiveAttack(Piece piece)
    {
        if (logicManager == null)
        {
            return piece.Attack;
        }

        return logicManager.GetEffectiveAttack(piece);
    }

    /// <summary>
    /// 取得棋子套用目前效果後的價值。
    /// </summary>
    private int GetEffectiveValue(Piece piece)
    {
        if (logicManager == null)
        {
            return piece.Value;
        }

        return logicManager.GetEffectiveValue(piece);
    }

    /// <summary>
    /// 將基礎值與有效值組合成屬性顯示文字。
    /// </summary>
    private string FormatStatText(
        string label,
        int baseValue,
        int effectiveValue
    )
    {
        if (effectiveValue == baseValue)
        {
            return $"{label}: {effectiveValue}";
        }

        string color = ColorUtility.ToHtmlStringRGB(modifiedStatColor);
        return $"{label}: <color=#{color}>{effectiveValue}</color>";
    }

    /// <summary>
    /// 尋找指定文字元件並更新其顯示內容。
    /// </summary>
    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    /// <summary>
    /// 產生物件的辨識文字，供紀錄或偵錯訊息使用。
    /// </summary>
    private string Describe(Piece piece)
    {
        if (piece == null)
        {
            return "None";
        }

        Vector2 coordinates = piece.GetCoordinates();
        string side = piece.IsWhite ? "White" : "Black";
        string type = string.IsNullOrEmpty(piece.PieceType)
            ? piece.GetType().Name
            : piece.PieceType;

        return $"{side} {type} ({coordinates.x:0},{coordinates.y:0})";
    }
}
