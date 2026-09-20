using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class CardDragHandler : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private CardHandManager handManager;
    private CardDefinition cardDefinition;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector2 originalAnchoredPosition;
    private Vector3 originalScale;
    private Vector2 originalPivot;
    private float originalAlpha = 1f;

    /// <summary>
    /// 保存手牌管理器與卡牌資料，取得拖曳使用的 UI 元件。
    /// </summary>
    public void Initialize(CardHandManager manager, CardDefinition card)
    {
        handManager = manager;
        cardDefinition = card;
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            Debug.LogError(
                $"{name} requires CanvasGroup for card dragging."
            );
        }
    }

    /// <summary>
    /// 記錄拖曳起點並調整卡片的顯示層級與互動狀態。
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (handManager == null || rectTransform == null)
        {
            return;
        }

        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalAnchoredPosition = rectTransform.anchoredPosition;
        originalScale = transform.localScale;
        originalPivot = rectTransform.pivot;
        originalAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        HandCardToggle handToggle =
            transform.GetComponentInParent<HandCardToggle>();
        if (handToggle != null)
        {
            handToggle.Collapse();
        }

        transform.SetParent(handManager.CardGameUiRoot, true);
        transform.SetAsLastSibling();

        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.position = eventData.position;
        transform.localScale = originalScale * 0.5f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.5f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 依游標位置更新拖曳中的卡片位置。
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform != null)
        {
            rectTransform.position = eventData.position;
        }
    }

    /// <summary>
    /// 判斷卡片放置目標並提交操作；未成功放置時恢復手牌位置。
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        if (
            handManager != null &&
            IsOverRecycleCard(eventData) &&
            handManager.TryRecycleCard(cardDefinition)
        )
        {
            Destroy(gameObject);
            return;
        }

        if (
            handManager != null &&
            TryDropFieldCard(eventData)
        )
        {
            handManager.PlayUsingCardAnimation(
                cardDefinition,
                gameObject
            );
            return;
        }

        if (
            handManager != null &&
            handManager.TryApplyCardToPiece(
                cardDefinition,
                eventData.position
            )
        )
        {
            handManager.PlayUsingCardAnimation(
                cardDefinition,
                gameObject
            );
            return;
        }

        RestoreToHand();
    }

    /// <summary>
    /// 嘗試將拖曳中的場地卡放入有效的場地欄位。
    /// </summary>
    private bool TryDropFieldCard(PointerEventData eventData)
    {
        if (
            handManager == null ||
            cardDefinition == null ||
            cardDefinition.cardType != CardType.Field ||
            Camera.main == null
        )
        {
            return false;
        }

        Ray ray = Camera.main.ScreenPointToRay(eventData.position);
        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            return false;
        }

        FieldCardPlace place =
            hit.transform.GetComponentInParent<FieldCardPlace>();
        if (place == null)
        {
            return false;
        }

        return handManager.TryApplyFieldCardToPlace(cardDefinition, place);
    }

    /// <summary>
    /// 接收回收區操作並嘗試回收目前卡牌。
    /// </summary>
    public bool RecycleFromDropZone()
    {
        if (handManager == null)
        {
            return false;
        }

        return handManager.TryRecycleCard(cardDefinition);
    }

    /// <summary>
    /// 判斷游標是否落在卡片回收區。
    /// </summary>
    private bool IsOverRecycleCard(PointerEventData eventData)
    {
        if (handManager == null || handManager.RecycleCardRoot == null)
        {
            return false;
        }

        Camera camera = eventData.pressEventCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(
            handManager.RecycleCardRoot,
            eventData.position,
            camera
        );
    }

    /// <summary>
    /// 將拖曳中的卡片還原到原本手牌階層與位置。
    /// </summary>
    private void RestoreToHand()
    {
        if (originalParent == null || rectTransform == null)
        {
            return;
        }

        transform.SetParent(originalParent, false);
        transform.SetSiblingIndex(originalSiblingIndex);
        rectTransform.pivot = originalPivot;
        transform.localScale = originalScale;
        rectTransform.anchoredPosition = originalAnchoredPosition;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = originalAlpha;
        }
    }
}
