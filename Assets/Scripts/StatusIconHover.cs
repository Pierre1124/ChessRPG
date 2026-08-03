using UnityEngine;
using UnityEngine.EventSystems;

public class StatusIconHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private CardInfoUI owner;
    private StatusRuntime status;

    public void Initialize(CardInfoUI cardInfo, StatusRuntime statusRuntime)
    {
        owner = cardInfo;
        status = statusRuntime;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.ShowStatusTooltip(status, eventData.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.HideStatusTooltip();
        }
    }

    private void OnDisable()
    {
        if (owner != null)
        {
            owner.HideStatusTooltip();
        }
    }
}
