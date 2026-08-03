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

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform != null)
        {
            rectTransform.position = eventData.position;
        }
    }

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

    public bool RecycleFromDropZone()
    {
        if (handManager == null)
        {
            return false;
        }

        return handManager.TryRecycleCard(cardDefinition);
    }

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
