using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>只有底線連結攔截射線；其他文字仍可拖曳卡牌或選取卡組項目。</summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class CardKeywordText : MonoBehaviour, ICanvasRaycastFilter,
    IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private TMP_Text label;
    public static void Bind(TMP_Text text, string content)
    {
        if (text == null) return;
        var handler = text.GetComponent<CardKeywordText>();
        if (handler == null) handler = text.gameObject.AddComponent<CardKeywordText>();
        handler.label = text;
        text.richText = true;
        text.overrideColorTags = false;
        text.raycastTarget = true;
        text.text = CardKeywordGlossary.Formatter.Format(content);
        // Graphics register with their nearest Canvas, not the root Canvas raycaster.
        Canvas canvas = text.GetComponentInParent<Canvas>(true);
        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private int FindLink(Vector2 position, Camera camera)
    {
        if (label == null) label = GetComponent<TMP_Text>();
        return TMP_TextUtilities.FindIntersectingLink(label, position, camera);
    }
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        int index = FindLink(screenPoint, eventCamera);
        return index >= 0 && CardKeywordGlossary.Formatter.TryGet(label.textInfo.linkInfo[index].GetLinkID(), out _);
    }
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || data.dragging) return;
        int index = FindLink(data.position, data.pressEventCamera);
        if (index >= 0 && CardKeywordGlossary.Formatter.TryGet(label.textInfo.linkInfo[index].GetLinkID(), out var entry))
        {
            CardKeywordPopup.Show(entry);
            data.Use();
        }
    }
    // Consume the gesture at a keyword so it cannot also play a card or press a parent Button.
    public void OnPointerDown(PointerEventData data) { }
    public void OnBeginDrag(PointerEventData data) { }
    public void OnDrag(PointerEventData data) { }
    public void OnEndDrag(PointerEventData data) { }
}
