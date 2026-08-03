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
    private GameObject cardPreviewObject;
    private readonly List<GameObject> spawnedStatusIcons =
        new List<GameObject>();

    public void Initialize(LogicManager owner)
    {
        logicManager = owner;

        if (infoRoot == null)
        {
            infoRoot = transform;
        }

        HideStatusTooltip();
        Hide();
    }

    public void Show(Piece piece)
    {
        if (piece == null)
        {
            Hide();
            return;
        }

        if (infoRoot == null)
        {
            infoRoot = transform;
        }

        CardDefinition equippedCard = piece.cardDefinition;
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
    }

    public void Hide()
    {
        HideStatusTooltip();
        ClearCardPreview();
        ClearStatusIcons();

        if (infoRoot != null)
        {
            infoRoot.gameObject.SetActive(false);
        }
    }

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

    private void ClearCardPreview()
    {
        if (cardPreviewObject != null)
        {
            Destroy(cardPreviewObject);
            cardPreviewObject = null;
        }
    }

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

            GameObject iconObject =
                new GameObject(
                    status.definition.statusName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );

            iconObject.transform.SetParent(chessStatusRoot, false);

            RectTransform rectTransform =
                iconObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = statusIconSize;

            Image image = iconObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;

            StatusIconHover hover =
                iconObject.AddComponent<StatusIconHover>();
            hover.Initialize(this, status);

            spawnedStatusIcons.Add(iconObject);
        }
    }

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

    public void HideStatusTooltip()
    {
        if (statusTooltip != null)
        {
            statusTooltip.Hide();
        }
    }

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
            int bonus = Mathf.Max(0, 1 + captures - friendlyLosses);
            return $"\u6211\u65b9\u6240\u6709\u68cb\u5b50\u653b\u64ca\u529b +{bonus}" +
                $"\n\u8c9e\u5fb7\u5403\u5b50\uff1a{captures}\uff0c" +
                $"\u6211\u65b9\u68cb\u5b50\u88ab\u5403\uff1a{friendlyLosses}";
        }

        if (card != null && card.id == "J06")
        {
            return "\u4e5d\u5bae\u683c\u5167\u6211\u65b9\u68cb\u5b50\u53d7\u5230\u50b7\u5bb3 -1";
        }

        if (card != null && card.id == "J09")
        {
            return "\u6211\u65b9\u6240\u6709\u68cb\u5b50\u653b\u64ca\u529b +2\uff1b\u6211\u65b9\u7121\u6cd5\u56de\u5fa9 HP";
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

    private string FormatSigned(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

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

    private bool IsVisibleStatus(StatusRuntime status)
    {
        return
            status != null &&
            status.definition != null &&
            status.HasCharges;
    }

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

    private int GetEffectiveAttack(Piece piece)
    {
        if (logicManager == null)
        {
            return piece.Attack;
        }

        return logicManager.GetEffectiveAttack(piece);
    }

    private int GetEffectiveValue(Piece piece)
    {
        if (logicManager == null)
        {
            return piece.Value;
        }

        return logicManager.GetEffectiveValue(piece);
    }

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

    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

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
