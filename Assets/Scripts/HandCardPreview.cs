using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>固定側邊大卡；只複製呈現，不保留出牌操作。</summary>
public sealed class HandCardPreview : MonoBehaviour, IPointerClickHandler
{
    private static HandCardPreview current;
    private CardDragHandler owner;
    private RectTransform cardRect;
    private bool pinned;
    private float hideAt = float.PositiveInfinity;

    public static bool ContainsPointer() => current != null && Mouse.current != null &&
        RectTransformUtility.RectangleContainsScreenPoint(current.cardRect, Mouse.current.position.ReadValue(), null);

    public static void Show(CardDragHandler source, bool pin)
    {
        if (current != null && current.owner == source)
        { current.pinned |= pin; current.hideAt = float.PositiveInfinity; return; }
        if (current != null && current.pinned && !pin) return;
        if (current != null) { var old = current; current = null; Dispose(old.gameObject); }
        var root = new GameObject("Hand card reading preview", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
        current = root.AddComponent<HandCardPreview>(); current.owner = source; current.pinned = pin;
        var clone = Instantiate(source.gameObject, root.transform, false);
        clone.name = "Read-only enlarged card";
        foreach (var drag in clone.GetComponentsInChildren<CardDragHandler>(true)) drag.enabled = false;
        foreach (var hint in clone.GetComponentsInChildren<CardUnavailableHint>(true)) hint.enabled = false;
        foreach (var glow in clone.GetComponentsInChildren<CardGlowGraphic>(true)) glow.enabled = false;
        foreach (var animator in clone.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var control in clone.GetComponentsInChildren<Selectable>(true)) control.enabled = false;
        foreach (var nested in clone.GetComponentsInChildren<Canvas>(true)) nested.overrideSorting = false;
        foreach (var group in clone.GetComponentsInChildren<CanvasGroup>(true))
        { group.alpha = 1; group.blocksRaycasts = true; group.interactable = true; }
        current.cardRect = clone.GetComponent<RectTransform>();
        current.cardRect.anchorMin = current.cardRect.anchorMax = new Vector2(0, .5f);
        current.cardRect.pivot = new Vector2(0, .5f);
        Canvas.ForceUpdateCanvases(); current.Fit();
    }

    private void Fit()
    {
        var root = (RectTransform)transform;
        float scale = Mathf.Min(Mathf.Min(480, root.rect.width * .28f) / Mathf.Max(1, cardRect.rect.width), root.rect.height * .76f / Mathf.Max(1, cardRect.rect.height));
        cardRect.localScale = Vector3.one * scale;
        cardRect.anchoredPosition = new Vector2(20, 0);
    }

    public static void ReleaseHover(CardDragHandler source)
    { if (current != null && current.owner == source && !current.pinned) current.hideAt = Time.unscaledTime + .25f; }
    public static void Hide(CardDragHandler source)
    {
        if (current == null || current.owner != source) return;
        var old = current; current = null; source?.DismissHover(); old.gameObject.SetActive(false); Dispose(old.gameObject);
    }
    private static void Dispose(GameObject value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pinned = true; }
    private void Update()
    {
        if (owner == null || !owner.isActiveAndEnabled) { if (current == this) current = null; Destroy(gameObject); return; }
        Fit();
        if (CardKeywordPopup.IsOpen) return;
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        { Hide(owner); return; }
        if (!pinned && Time.unscaledTime > hideAt && !ContainsPointer()) Hide(owner);
    }
    private void OnDestroy() { if (current == this) current = null; }
}
