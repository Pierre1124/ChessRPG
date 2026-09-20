using UnityEngine;
using UnityEngine.EventSystems;

public class StatusIconHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private CardInfoUI owner;
    private StatusRuntime status;

    /// <summary>
    /// 保存資訊面板及狀態資料，供游標提示使用。
    /// </summary>
    public void Initialize(CardInfoUI cardInfo, StatusRuntime statusRuntime)
    {
        owner = cardInfo;
        status = statusRuntime;
    }

    /// <summary>
    /// 游標進入狀態圖示時顯示提示內容。
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.ShowStatusTooltip(status, eventData.position);
        }
    }

    /// <summary>
    /// 游標離開狀態圖示時隱藏提示內容。
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.HideStatusTooltip();
        }
    }

    /// <summary>
    /// 圖示停用時關閉其所屬資訊面板的狀態提示。
    /// </summary>
    private void OnDisable()
    {
        if (owner != null)
        {
            owner.HideStatusTooltip();
        }
    }
}
