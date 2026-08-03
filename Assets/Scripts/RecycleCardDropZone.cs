using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class RecycleCardDropZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null || eventData.pointerDrag == null)
        {
            return;
        }

        CardDragHandler card =
            eventData.pointerDrag.GetComponent<CardDragHandler>();

        if (card == null)
        {
            return;
        }

        if (card.RecycleFromDropZone())
        {
            Destroy(eventData.pointerDrag);
        }
    }
}
