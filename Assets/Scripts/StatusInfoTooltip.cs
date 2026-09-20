using TMPro;
using UnityEngine;

public class StatusInfoTooltip : MonoBehaviour
{
    [SerializeField] public RectTransform panel;
    [SerializeField] public TMP_Text infoText;
    [SerializeField] private Vector2 padding = new Vector2(28f, 20f);
    [SerializeField, Min(1f)] private float minWidth = 180f;
    [SerializeField, Min(1f)] private float maxWidth = 420f;
    [SerializeField, Min(1f)] private float minHeight = 64f;
    [SerializeField] private Vector2 mouseOffset = new Vector2(16f, 16f);

    /// <summary>
    /// 依提示文字計算面板尺寸，再定位到指定螢幕位置旁。
    /// </summary>
    public void Show(string content, Vector2 screenPosition)
    {
        if (panel == null || infoText == null || string.IsNullOrEmpty(content))
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        infoText.text = content;
        infoText.ForceMeshUpdate();

        float maxTextWidth = Mathf.Max(1f, maxWidth - padding.x);
        float naturalWidth = infoText.GetPreferredValues(content).x;
        float textWidth = Mathf.Clamp(
            naturalWidth,
            Mathf.Max(1f, minWidth - padding.x),
            maxTextWidth
        );
        Vector2 preferred = infoText.GetPreferredValues(
            content,
            textWidth,
            0f
        );

        panel.sizeDelta = new Vector2(
            Mathf.Clamp(preferred.x + padding.x, minWidth, maxWidth),
            Mathf.Max(minHeight, preferred.y + padding.y)
        );

        PlaceBesideMouse(screenPosition);
    }

    /// <summary>
    /// 停用狀態提示物件。
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 將提示面板放在游標旁並依畫面範圍調整位置。
    /// </summary>
    private void PlaceBesideMouse(Vector2 screenPosition)
    {
        RectTransform parentRect = panel.parent as RectTransform;
        if (parentRect == null) return;

        Canvas canvas = panel.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            screenPosition,
            eventCamera,
            out Vector2 mouseLocal))
        {
            return;
        }

        Rect bounds = parentRect.rect;
        Vector2 size = panel.rect.size;

        Vector2 fourthQuadrant = new Vector2(
            mouseLocal.x + mouseOffset.x,
            mouseLocal.y - mouseOffset.y
        );
        bool fitsFourthQuadrant =
            fourthQuadrant.x + size.x <= bounds.xMax &&
            fourthQuadrant.y - size.y >= bounds.yMin;

        panel.pivot = fitsFourthQuadrant
            ? new Vector2(0f, 1f)
            : new Vector2(0f, 0f);

        Vector2 target = fitsFourthQuadrant
            ? fourthQuadrant
            : new Vector2(
                mouseLocal.x + mouseOffset.x,
                mouseLocal.y + mouseOffset.y
            );

        target.x = Mathf.Clamp(
            target.x,
            bounds.xMin + panel.pivot.x * size.x,
            bounds.xMax - (1f - panel.pivot.x) * size.x
        );
        target.y = Mathf.Clamp(
            target.y,
            bounds.yMin + panel.pivot.y * size.y,
            bounds.yMax - (1f - panel.pivot.y) * size.y
        );

        Vector3 localPosition = panel.localPosition;
        panel.localPosition = new Vector3(target.x, target.y, localPosition.z);
    }
}
