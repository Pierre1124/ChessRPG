using UnityEngine;

/// <summary>Fits popup panels to the actual safe screen rectangle, including scaled canvases.</summary>
public static class MobilePanelBounds
{
    private static readonly Vector3[] corners = new Vector3[4];
    public static void Fit(RectTransform panel)
    {
        if (!MobilePrototype.Enabled || panel == null || !panel.gameObject.activeInHierarchy) return;
        var canvas = panel.GetComponentInParent<Canvas>();
        var parent = panel.parent as RectTransform;
        if (canvas == null || parent == null || canvas.renderMode == RenderMode.WorldSpace) return;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Rect safe = Screen.safeArea;
        float margin = Mathf.Min(safe.width, safe.height) * .015f;
        safe = Rect.MinMaxRect(safe.xMin + margin, safe.yMin + margin, safe.xMax - margin, safe.yMax - margin);
        Rect bounds = ScreenBounds(panel, camera);
        float scale = Mathf.Min(1, safe.width / Mathf.Max(1, bounds.width), safe.height / Mathf.Max(1, bounds.height));
        if (scale < 1) { panel.localScale *= scale; bounds = ScreenBounds(panel, camera); }
        Vector2 delta = new Vector2(
            bounds.xMin < safe.xMin ? safe.xMin - bounds.xMin : Mathf.Min(0, safe.xMax - bounds.xMax),
            bounds.yMin < safe.yMin ? safe.yMin - bounds.yMin : Mathf.Min(0, safe.yMax - bounds.yMax));
        Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, panel.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, point, camera, out var before) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, point + delta, camera, out var after))
            panel.anchoredPosition += after - before;
    }
    public static Rect ScreenBounds(RectTransform panel, Camera camera)
    {
        panel.GetWorldCorners(corners);
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        foreach (var corner in corners)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(camera, corner);
            min = Vector2.Min(min, p); max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
