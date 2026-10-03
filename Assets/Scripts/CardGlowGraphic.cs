using UnityEngine;
using UnityEngine.UI;

/// <summary>用透明漸層網格繪製 UI 外光暈，不需要粒子、貼圖或額外材質。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CardGlowGraphic : MaskableGraphic
{
    private const float GlowExtent = 24f;

    /// <summary>在指定 UI 外圍建立不攔截點擊、不參與排版的光暈。</summary>
    public static CardGlowGraphic Create(RectTransform parent)
    {
        var go = new GameObject("Playable card glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.transform.SetAsFirstSibling();
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var glow = go.AddComponent<CardGlowGraphic>();
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
        Rect rect = rectTransform.rect;
        float[] distances = { 0f, 3f, 8f, 16f, 24f };
        float[] alphas = { 1f, 0.75f, 0.4f, 0.12f, 0f };
        for (int band = 0; band < 4; band++)
        {
            int first = vh.currentVertCount;
            for (int ring = 0; ring < 2; ring++)
            {
                float d = distances[band + ring];
                Color tint = color;
                tint.a *= alphas[band + ring];
                vh.AddVert(new Vector3(rect.xMin - d, rect.yMin - d), tint, Vector2.zero);
                vh.AddVert(new Vector3(rect.xMin - d, rect.yMax + d), tint, Vector2.zero);
                vh.AddVert(new Vector3(rect.xMax + d, rect.yMax + d), tint, Vector2.zero);
                vh.AddVert(new Vector3(rect.xMax + d, rect.yMin - d), tint, Vector2.zero);
            }
            for (int edge = 0; edge < 4; edge++)
            {
                int next = (edge + 1) % 4;
                vh.AddTriangle(first + edge, first + next, first + 4 + next);
                vh.AddTriangle(first + edge, first + 4 + next, first + 4 + edge);
            }
        }
    }
}
