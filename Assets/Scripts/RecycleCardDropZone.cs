using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class RecycleCardDropZone : MonoBehaviour, IDropHandler
{
    /// <summary>
    /// 接收拖入回收區的卡片並交由拖曳元件處理回收。
    /// </summary>
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
