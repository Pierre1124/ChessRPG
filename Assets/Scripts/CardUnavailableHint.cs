using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>滑鼠停留在不可用手牌時顯示原因；提示不攔截點擊，也不改變出牌規則。</summary>
public sealed class CardUnavailableHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CardHandManager hand;
    private CardDefinition card;
    private PointerEventData pointer;
    private RectTransform panel;
    private TMP_Text label;
    private float showAt;
    private float refreshAt;

    /// <summary>綁定目前手牌並清除上一次停留狀態。</summary>
    public void Initialize(CardHandManager manager, CardDefinition definition)
    {
        Dismiss();
        hand = manager;
        card = definition;
    }

    /// <summary>開始短暫停留計時，避免快速移動滑鼠時閃出提示。</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        pointer = eventData;
        showAt = Time.unscaledTime + 0.35f;
        refreshAt = 0f;
    }

    /// <summary>離開卡片後立即移除提示。</summary>
    public void OnPointerExit(PointerEventData eventData) { Dismiss(); }

    /// <summary>定期重查不可用原因，回合或棋盤改變後不保留過期文字。</summary>
    private void Update()
    {
        if (pointer != null && pointer.dragging) { Dismiss(); return; }
        if (pointer == null || hand == null || Time.unscaledTime < showAt) return;
        if (Time.unscaledTime >= refreshAt)
        {
            refreshAt = Time.unscaledTime + 0.15f;
            string reason = hand.GetCardUnavailableReason(card);
            if (string.IsNullOrEmpty(reason))
            {
                if (panel != null) panel.gameObject.SetActive(false);
                return;
            }
            if (panel == null) CreatePanel();
            if (panel == null) return;
            label.text = reason;
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
        }
        if (panel == null || !panel.gameObject.activeSelf) return;
        RectTransform parent = (RectTransform)panel.parent;
        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pointer.position, camera, out Vector2 local))
        {
            float x = Mathf.Clamp(local.x + 20f, parent.rect.xMin, Mathf.Max(parent.rect.xMin, parent.rect.xMax - panel.rect.width));
            float y = Mathf.Clamp(local.y + panel.rect.height + 24f, parent.rect.yMin + panel.rect.height, parent.rect.yMax);
            panel.localPosition = new Vector3(x, y, 0f);
        }
    }

    /// <summary>在手牌裁切區外建立不遮擋操作的提示，沿用卡面中文字型。</summary>
    private void CreatePanel()
    {
        if (hand.CardGameUiRoot == null) return;
        var root = SceneObjectTemplates.Spawn("Card unavailable hint", hand.CardGameUiRoot);
        panel = root.GetComponent<RectTransform>();
        label = root.GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>拖曳開始或離開手牌時取消計時並立即隱藏提示。</summary>
    public void Dismiss()
    {
        pointer = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    /// <summary>切換模式或隱藏手牌時清除提示。</summary>
    private void OnDisable() { Dismiss(); }

    /// <summary>手牌重建或卡牌用完時回收掛在外層 UI 的提示物件。</summary>
    private void OnDestroy()
    {
        if (panel != null) Destroy(panel.gameObject);
    }
}
