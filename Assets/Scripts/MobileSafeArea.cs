using UnityEngine;

/// <summary>Insets screen-space canvas children while preserving their authored anchors.</summary>
[RequireComponent(typeof(Canvas))]
public sealed class MobileSafeArea : MonoBehaviour
{
    private RectTransform content;
    private Rect lastSafe;
    private Vector2 lastSize;

    private void Awake()
    {
        if (!MobilePrototype.Enabled) { enabled = false; return; }
        var canvas = GetComponent<Canvas>();
        if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) { enabled = false; return; }
        var children = new Transform[transform.childCount];
        for (int i = 0; i < children.Length; i++) children[i] = transform.GetChild(i);
        content = new GameObject("Mobile safe area", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(transform, false);
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = content.offsetMax = Vector2.zero;
        foreach (var child in children) child.SetParent(content, false);
        Apply();
    }
    private void LateUpdate()
    {
        if (content != null && (lastSafe != Screen.safeArea || lastSize != new Vector2(Screen.width, Screen.height))) Apply();
    }
    private void Apply()
    {
        lastSafe = Screen.safeArea; lastSize = new Vector2(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;
        content.anchorMin = new Vector2(lastSafe.xMin / Screen.width, lastSafe.yMin / Screen.height);
        content.anchorMax = new Vector2(lastSafe.xMax / Screen.width, lastSafe.yMax / Screen.height);
        content.offsetMin = content.offsetMax = Vector2.zero;
    }
}
