using UnityEngine;
using UnityEngine.Rendering;

/// <summary>將軍狀態只標示在國王底部，不佔用操作警示或改寫棋盤選取顏色。</summary>
public sealed class KingCheckIndicator : MonoBehaviour
{
    private LineRenderer ring;
    private Material material;
    private readonly Vector3[] points = new Vector3[64];

    public static void SetVisible(King king, bool visible)
    {
        var indicator = king.GetComponentInChildren<KingCheckIndicator>(true);
        if (indicator == null && visible)
        {
            var root = new GameObject("King check indicator");
            root.transform.SetParent(king.transform, false);
            indicator = root.AddComponent<KingCheckIndicator>();
            indicator.Build();
        }
        if (indicator != null)
        {
            indicator.gameObject.SetActive(visible);
            if (visible) indicator.LateUpdate();
        }
    }

    private void Build()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        material = new Material(shader);
        var red = new Color(1f, .035f, .035f, 1f);
        material.SetColor("_BaseColor", red);
        material.SetColor("_Color", red);
        ring = gameObject.AddComponent<LineRenderer>();
        ring.sharedMaterial = material;
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = points.Length;
        ring.widthMultiplier = .12f;
        ring.startColor = ring.endColor = red;
        ring.shadowCastingMode = ShadowCastingMode.Off;
        ring.receiveShadows = false;
    }

    private void LateUpdate()
    {
        if (ring == null || transform.parent == null) return;
        Vector3 center = transform.parent.position;
        center.y = Mathf.Max(.12f, center.y + .025f);
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / points.Length;
            points[i] = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .42f;
        }
        ring.SetPositions(points);
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
    }
}
