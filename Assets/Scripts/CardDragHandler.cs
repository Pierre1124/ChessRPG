using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CanvasGroup))]
public class CardDragHandler : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
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
    private bool isDragging;
    private CardGlowGraphic playableGlow;
    private CardTargetFeedback targetFeedback;
    private GameObject clickSurface;
    private PointerEventData lastPointer;
    private bool dragWhiteTurn;
    private Coroutine returnRoutine;
    private RectTransform returnSlot;

    private static CardDragHandler selected, dragging;
    private static int consumedFrame = -1;
    public static bool BlocksBoardInput => selected != null || dragging != null || consumedFrame == Time.frameCount || (MobilePrototype.Enabled && HandCardPreview.IsOpen);
    private HandCardToggle handToggle;
    private bool collapsedForDrag, hovered;
    private float hoverAt;
    private int selectedFrame;
    private bool touchHolding, touchInspected;
    private float touchStarted;
    private Vector2 touchOrigin;
    public bool IsSelected => selected == this;
    public void DismissHover() { hovered = false; }

    public void OnPointerEnter(PointerEventData data) { if (MobilePrototype.Enabled || GameplayPointer.IsTouch) return; hovered = true; hoverAt = Time.unscaledTime + .25f; }
    public void OnPointerExit(PointerEventData data) { hovered = false; HandCardPreview.ReleaseHover(this); }
    public void OnPointerDown(PointerEventData data)
    {
        if (!GameplayPointer.IsTouch && !MobilePrototype.Enabled) return;
        touchHolding = true; touchInspected = false;
        touchStarted = Time.unscaledTime; touchOrigin = data.position;
    }
    public void OnPointerUp(PointerEventData data) { touchHolding = false; }

    public static void CancelActiveInteraction()
    {
        if (selected != null) selected.CancelSelection();
        if (dragging != null)
        {
            var card = dragging; dragging = null; card.isDragging = false;
            card.ClearTargetFeedback(); card.RestoreToHand();
        }
        consumedFrame = Time.frameCount;
    }

    public void OnPointerClick(PointerEventData data)
    {
        if (touchInspected) { touchInspected = false; data.Use(); return; }
        if (data.button != PointerEventData.InputButton.Left || data.dragging || isDragging || returnRoutine != null || handManager == null) return;
        if (BlocksBoardInput || !handManager.TryBeginCardDrag()) return;
        selected = this; selectedFrame = Time.frameCount; consumedFrame = Time.frameCount;
        PickUp(data);
        targetFeedback.ClickMode = true;
        // 第二次點擊只負責放牌，避免同時按到下方的抽牌、結束回合或關鍵字。
        clickSurface = new GameObject("Place held card", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var surface = (RectTransform)clickSurface.transform;
        surface.SetParent(handManager.CardGameUiRoot, false);
        surface.anchorMin = Vector2.zero;
        surface.anchorMax = Vector2.one;
        surface.offsetMin = surface.offsetMax = Vector2.zero;
        clickSurface.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        targetFeedback.ClickSurface = clickSurface;
        data.Use();
    }

    private void CancelSelection()
    {
        if (selected != this) return;
        selected = null; consumedFrame = Time.frameCount;
        hovered = false;
        ClearTargetFeedback();
        RestoreToHand();
        HandCardPreview.Hide(this);
    }

    internal static void ReleaseBeforeHandRebuild(CardHandManager manager)
    {
        if (selected != null && selected.handManager == manager) selected.CancelSelection();
        if (dragging != null && dragging.handManager == manager)
        {
            var card = dragging;
            dragging = null;
            consumedFrame = Time.frameCount;
            card.isDragging = false;
            card.ClearTargetFeedback();
            card.RestoreToHand();
        }
    }

    private void Update()
    {
        if (GameplayPointer.Canceled) { touchHolding = false; CancelActiveInteraction(); return; }
        if (touchHolding && !isDragging && !BlocksBoardInput && GameplayPointer.Held &&
            Vector2.Distance(touchOrigin, GameplayPointer.Position) <= GameplayPointer.TapSlop &&
            Time.unscaledTime - touchStarted >= 0.55f)
        {
            touchHolding = false; touchInspected = true;
            GetComponent<CardUnavailableHint>()?.Dismiss();
            HandCardPreview.Show(this, true);
            consumedFrame = Time.frameCount;
        }
        if (hovered && !BlocksBoardInput && returnRoutine == null && Time.unscaledTime >= hoverAt && !CardKeywordPopup.IsOpen)
            HandCardPreview.Show(this, false);
        if (selected != this || !GameplayPointer.Available) return;
        if (CardKeywordPopup.IsOpen) return;
        if (dragWhiteTurn != handManager.IsWhiteCardTurn || !handManager.CanPreviewCard(cardDefinition) ||
            GameplayPointer.CancelPressed)
        { CancelSelection(); return; }
        var pointer = new PointerEventData(EventSystem.current) { position = GameplayPointer.Position };
        FollowPointer(pointer);
        targetFeedback?.ShowAt(pointer);
        if (Time.frameCount == selectedFrame || !GameplayPointer.Released) return;
        if (MobilePrototype.IsOverControls(pointer.position)) return;
        consumedFrame = Time.frameCount;
        selected = null;
        ResolveDrop(pointer);
    }

    /// <summary>
    /// 保存手牌管理器與卡牌資料，取得拖曳使用的 UI 元件。
    /// </summary>
    public void Initialize(CardHandManager manager, CardDefinition card)
    {
        handManager = manager;
        cardDefinition = card;
        CardUnavailableHint hoverHint = GetComponent<CardUnavailableHint>();
        if (hoverHint == null) { Debug.LogError("手牌樣板缺少 CardUnavailableHint", this); return; }
        hoverHint.Initialize(manager, card);
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (playableGlow == null && rectTransform != null) playableGlow = CardGlowGraphic.Create(rectTransform);
        if (playableGlow != null) playableGlow.enabled = false;

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

        if (touchInspected || GameplayPointer.Canceled || BlocksBoardInput || returnRoutine != null || eventData.button != PointerEventData.InputButton.Left ||
            !handManager.TryBeginCardDrag()) return;
        eventData.eligibleForClick = false;
        touchHolding = false;
        isDragging = true; dragging = this; consumedFrame = Time.frameCount;
        PickUp(eventData);
    }

    private void PickUp(PointerEventData eventData)
    {
        hovered = false;
        HandCardPreview.CloseCurrent();
        GetComponent<CardUnavailableHint>()?.Dismiss();
        dragWhiteTurn = handManager.IsWhiteCardTurn;

        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalAnchoredPosition = rectTransform.anchoredPosition;
        originalScale = transform.localScale;
        originalPivot = rectTransform.pivot;
        originalAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        handToggle = transform.GetComponentInParent<HandCardToggle>();
        collapsedForDrag = false;

        transform.SetParent(handManager.CardGameUiRoot, true);
        transform.SetAsLastSibling();

        rectTransform.pivot = new Vector2(0f, 1f);
        FollowPointer(eventData);
        transform.localScale = originalScale * 0.5f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.5f;
            canvasGroup.blocksRaycasts = false;
        }
        lastPointer = eventData;
        if (playableGlow != null) playableGlow.enabled = false;
        TMPro.TMP_Text label = GetComponentInChildren<TMPro.TMP_Text>(true);
        targetFeedback = CardTargetFeedback.Create(handManager, cardDefinition, label != null ? label.font : null);
        targetFeedback.ShowAt(eventData);
    }

    /// <summary>
    /// 依游標位置更新拖曳中的卡片位置。
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging && rectTransform != null)
        {
            FollowPointer(eventData);
            lastPointer = eventData;
            if (!collapsedForDrag && handToggle != null && !handToggle.ContainsPointer(eventData.position))
            { handToggle.Collapse(); collapsedForDrag = true; }
        }
    }

    private void FollowPointer(PointerEventData pointer)
    {
        var canvas = handManager.CardGameUiRoot.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)rectTransform.parent,
            pointer.position, camera, out var position)) rectTransform.position = position;
    }

    /// <summary>
    /// 判斷卡片放置目標並提交操作；未成功放置時恢復手牌位置。
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        // Unity 仍可能對被拒絕的拖曳送出後續事件，不能因此移動或使用卡片。
        if (!isDragging) return;
        if (GameplayPointer.Canceled) { CancelActiveInteraction(); return; }
        isDragging = false; dragging = null; consumedFrame = Time.frameCount;
        ResolveDrop(eventData);
    }

    private void ResolveDrop(PointerEventData eventData)
    {
        ClearTargetFeedback();

        if (handManager == null || dragWhiteTurn != handManager.IsWhiteCardTurn || !handManager.TryBeginCardDrag())
        {
            RestoreToHand();
            return;
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

        string rejection = GetDropRejection(eventData);
        if (rejection != null)
        {
            GameFlowUI.Show(rejection);
            returnRoutine = StartCoroutine(ReturnToHand());
            return;
        }
        bool accepted = cardDefinition.cardType == CardType.Field
            ? TryDropFieldCard(eventData)
            : handManager.TryApplyCardToPiece(cardDefinition, eventData.position);
        if (accepted)
        {
            handManager.PlayUsingCardAnimation(
                cardDefinition,
                gameObject
            );
            return;
        }

        GameFlowUI.Show("出牌未成功，請確認目標條件後再試");
        returnRoutine = StartCoroutine(ReturnToHand());
    }

    /// <summary>在提交前檢查實際放置位置，使用既有合法目標集合產生具體拒絕原因。</summary>
    private string GetDropRejection(PointerEventData pointer)
    {
        var hits = new System.Collections.Generic.List<RaycastResult>();
        if (EventSystem.current != null) EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Exists(uiHit => !uiHit.gameObject.transform.IsChildOf(transform))) return "請將卡牌放到棋盤上的有效目標";
        if (Camera.main == null || !Physics.Raycast(Camera.main.ScreenPointToRay(pointer.position), out RaycastHit hit))
            return cardDefinition.cardType == CardType.Field ? "請將場地卡放到場地欄位" : "請將卡牌放到符合條件的棋子";
        Component target = cardDefinition.cardType == CardType.Field
            ? (Component)hit.transform.GetComponentInParent<FieldCardPlace>() : hit.transform.GetComponentInParent<Piece>();
        if (target == null) return cardDefinition.cardType == CardType.Field ? "請將場地卡放到場地欄位" : "請將卡牌放到符合條件的棋子";
        var targets = new System.Collections.Generic.List<Component>();
        handManager.CollectCardTargets(cardDefinition, targets);
        if (targets.Contains(target)) return null;
        return handManager.GetCardUnavailableReason(cardDefinition) ?? "此目標不符合這張卡牌的使用條件";
    }

    /// <summary>保留排版位置並以短動畫回到正在移動的手牌面板，期間禁止重複拖曳。</summary>
    private System.Collections.IEnumerator ReturnToHand()
    {
        if (originalParent == null) { Destroy(gameObject); yield break; }
        var slot = SceneObjectTemplates.Spawn("Returning card slot");
        returnSlot = slot.GetComponent<RectTransform>();
        returnSlot.SetParent(originalParent, false);
        returnSlot.SetSiblingIndex(originalSiblingIndex);
        returnSlot.sizeDelta = rectTransform.rect.size;
        returnSlot.pivot = originalPivot;
        returnSlot.anchoredPosition = originalAnchoredPosition;
        returnSlot.localScale = originalScale;
        var layout = slot.GetComponent<UnityEngine.UI.LayoutElement>();
        layout.preferredWidth = rectTransform.rect.width;
        layout.preferredHeight = rectTransform.rect.height;
        if (originalParent is RectTransform parentRect) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        Vector3 center = rectTransform.TransformPoint(rectTransform.rect.center);
        rectTransform.pivot = originalPivot;
        rectTransform.position += center - rectTransform.TransformPoint(rectTransform.rect.center);
        Vector3 start = rectTransform.position;
        Vector3 scale = rectTransform.localScale;
        float alpha = canvasGroup.alpha;
        canvasGroup.blocksRaycasts = false;
        float elapsed = 0f;
        while (elapsed < 0.24f && returnSlot != null && originalParent != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.24f));
            rectTransform.position = Vector3.Lerp(start, returnSlot.position, t);
            rectTransform.localScale = Vector3.Lerp(scale, originalScale, t);
            canvasGroup.alpha = Mathf.Lerp(alpha, originalAlpha, t);
            yield return null;
        }
        // 手牌在回位期間重建時，佔位會一併移除；舊卡不可再插回造成重複。
        if (returnSlot == null || originalParent == null)
        {
            returnRoutine = null;
            Destroy(gameObject);
            yield break;
        }
        ClearReturnSlot();
        RestoreToHand();
        returnRoutine = null;
    }

    /// <summary>清除回位動畫的排版佔位，不留下空白手牌。</summary>
    private void ClearReturnSlot()
    {
        if (returnSlot != null) { returnSlot.gameObject.SetActive(false); Destroy(returnSlot.gameObject); }
        returnSlot = null;
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
        if (!isDragging || handManager == null || dragWhiteTurn != handManager.IsWhiteCardTurn || !handManager.TryBeginCardDrag())
        {
            return false;
        }

        bool recycled = handManager.TryRecycleCard(cardDefinition);
        if (recycled)
        {
            isDragging = false; if (dragging == this) dragging = null;
            ClearTargetFeedback();
        }
        return recycled;
    }

    /// <summary>即時更新手牌可用光暈，拖曳途中若失去資格則還原並清除提示。</summary>
    private void LateUpdate()
    {
        if (handManager == null) return;
        if (isDragging && (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame ||
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        { isDragging = false; dragging = null; consumedFrame = Time.frameCount; ClearTargetFeedback(); RestoreToHand(); }
        if (isDragging && (dragWhiteTurn != handManager.IsWhiteCardTurn || handManager.GetCardInteractionBlockReason() != null))
        {
            isDragging = false; if (dragging == this) dragging = null;
            ClearTargetFeedback();
            RestoreToHand();
        }
        if (playableGlow != null)
            playableGlow.enabled = (selected == null || selected == this) && (dragging == null || dragging == this)
                && returnRoutine == null && handManager.HasPlayableCardTarget(cardDefinition);
        if (isDragging && targetFeedback != null && lastPointer != null) targetFeedback.ShowAt(lastPointer);
    }

    /// <summary>在出牌、取消或物件停用時立即清除世界目標與 UI 提示。</summary>
    private void ClearTargetFeedback()
    {
        if (clickSurface != null)
        {
            clickSurface.SetActive(false);
            Destroy(clickSurface);
            clickSurface = null;
        }
        if (targetFeedback != null)
        {
            targetFeedback.ClearVisuals();
            Destroy(targetFeedback.gameObject);
            targetFeedback = null;
        }
        lastPointer = null;
    }

    /// <summary>手牌重繪、切換模式或離開場景時不保留拖曳視覺物件。</summary>
    private void OnApplicationPause(bool paused) { if (paused) { touchHolding = false; CancelActiveInteraction(); } }
    private void OnApplicationFocus(bool focused) { if (!focused) { touchHolding = false; CancelActiveInteraction(); } }

    private void OnDisable()
    {
        if (selected == this) CancelSelection();
        if (dragging == this) { dragging = null; consumedFrame = Time.frameCount; }
        HandCardPreview.Hide(this);
        if (returnRoutine != null) { StopCoroutine(returnRoutine); returnRoutine = null; }
        ClearReturnSlot();
        isDragging = false;
        ClearTargetFeedback();
        if (playableGlow != null) playableGlow.enabled = false;
    }

    /// <summary>
    /// 判斷游標是否落在卡片回收區。
    /// </summary>
    private bool IsOverRecycleCard(PointerEventData eventData)
    {
        if (handManager == null || handManager.RecycleCardRoot == null ||
            !handManager.RecycleCardRoot.gameObject.activeInHierarchy)
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
            canvasGroup.blocksRaycasts = true;
        }
    }
}
