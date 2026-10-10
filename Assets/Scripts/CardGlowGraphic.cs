using UnityEngine;
using UnityEngine.UI;

/// <summary>用透明漸層網格繪製 UI 外光暈，不需要粒子、貼圖或額外材質。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CardGlowGraphic : MaskableGraphic
{
    private const float GlowExtent = 24f;
    [SerializeField] private bool usePreferences = true;
    private int revision = -1;
    private static readonly float[] Distances = { 0f, 3f, 8f, 16f, 24f };
    private static readonly float[] Alphas = { 1f, 0.75f, 0.4f, 0.12f, 0f };

    /// <summary>在指定 UI 外圍建立不攔截點擊、不參與排版的光暈。</summary>
    public static CardGlowGraphic Create(RectTransform parent, bool usePreferences = true)
    {
        CardGlowGraphic existing = parent.GetComponentInChildren<CardGlowGraphic>(true);
        if (existing != null && existing.transform.parent == parent) { existing.usePreferences = usePreferences; return existing; }
        if (Application.isPlaying)
        {
            var copy = SceneObjectTemplates.Spawn("Card glow", parent).GetComponent<CardGlowGraphic>();
            copy.usePreferences = usePreferences; copy.transform.SetAsFirstSibling(); return copy;
        }
        var go = new GameObject("Playable card glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.transform.SetAsFirstSibling();
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var glow = go.AddComponent<CardGlowGraphic>();
        glow.usePreferences = usePreferences;
        glow.raycastTarget = false;
        glow.color = new Color(0.15f, 0.92f, 0.72f, 0.7f);
        glow.rectTransform.anchorMin = Vector2.zero;
        glow.rectTransform.anchorMax = Vector2.one;
        glow.rectTransform.sizeDelta = Vector2.zero;
        glow.rectTransform.anchoredPosition = Vector2.zero;
        // 卡牌本身的裁切框必須包含外光暈；仍保留上層手牌區的裁切限制。
        RectMask2D cardMask = parent.GetComponent<RectMask2D>();
        if (cardMask != null)
        {
            Vector4 padding = cardMask.padding;
            cardMask.padding = new Vector4(
                Mathf.Min(padding.x, -GlowExtent), Mathf.Min(padding.y, -GlowExtent),
                Mathf.Min(padding.z, -GlowExtent), Mathf.Min(padding.w, -GlowExtent));
        }
        return glow;
    }

    /// <summary>建立由內向外淡出的四層邊框，中心保持透明以免蓋住文字。</summary>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int style = usePreferences ? CardGlowSettings.Style : 0;
        if (style == 3) return;
        Rect rect = rectTransform.rect;
        for (int band = 0; band < 4; band++)
        {
            int first = vh.currentVertCount;
            for (int ring = 0; ring < 2; ring++)
            {
                float d = Distances[band + ring] * (style == 1 ? 0.22f : 1f);
                Color tint = usePreferences ? CardGlowSettings.Tint : color;
                if (usePreferences) tint.a = CardGlowSettings.Intensity;
                if (style == 2) tint.a *= 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 2.5f);
                tint.a *= Alphas[band + ring];
                float radius = Mathf.Min(8f, Mathf.Min(rect.width, rect.height) * 0.5f);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                        corner < 2 ? rect.yMax - radius : rect.yMin + radius);
                    for (int step = 0; step <= 6; step++)
                    {
                        float angle = (corner * 90f + step * 15f) * Mathf.Deg2Rad;
                        vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + d), tint, Vector2.zero);
                    }
                }
            }
            for (int edge = 0; edge < 28; edge++)
            {
                int next = (edge + 1) % 28;
                vh.AddTriangle(first + edge, first + next, first + 28 + next);
                vh.AddTriangle(first + edge, first + 28 + next, first + 28 + edge);
            }
        }
    }

    /// <summary>靜態樣式只在設定改變時重建，呼吸樣式使用不受暫停影響的時間。</summary>
    private void Update()
    {
        if (!usePreferences) return;
        if (revision != CardGlowSettings.Revision || CardGlowSettings.Style == 2)
        {
            revision = CardGlowSettings.Revision;
            SetVerticesDirty();
        }
    }
}
