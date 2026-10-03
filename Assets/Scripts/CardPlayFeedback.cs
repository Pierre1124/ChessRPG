using UnityEngine;

/// <summary>已確認出牌成功時顯示短暫目標光圈，不修改棋子材質或棋盤高亮。</summary>
public sealed class CardPlayFeedback : MonoBehaviour
{
    private LineRenderer ring;
    private Material material;
    private float startedAt;
    private LogicManager logic;

    /// <summary>在已接受的目標位置建立獨立光圈，目標被技能移除後仍能顯示結果。</summary>
    public static void Show(Transform target)
    {
        if (target == null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        if (shader == null) return;
        var root = new GameObject("Card played successfully");
        var feedback = root.AddComponent<CardPlayFeedback>();
        feedback.startedAt = Time.unscaledTime;
        feedback.logic = Object.FindFirstObjectByType<LogicManager>();
        feedback.material = new Material(shader);
        Color tint = new Color(0.3f, 1f, 0.7f);
        feedback.material.SetColor("_BaseColor", tint);
        feedback.material.SetColor("_Color", tint);
        feedback.ring = root.AddComponent<LineRenderer>();
        feedback.ring.sharedMaterial = feedback.material;
        feedback.ring.loop = true;
        feedback.ring.useWorldSpace = false;
        feedback.ring.widthMultiplier = 0.09f;
        feedback.ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        feedback.ring.receiveShadows = false;
        root.transform.position = target.position;
        Collider collider = target.GetComponent<Collider>();
        if (target.GetComponent<FieldCardPlace>() != null && collider != null)
        {
            Bounds b = collider.bounds;
            root.transform.position = new Vector3(b.center.x, b.max.y + 0.035f, b.center.z);
            feedback.ring.positionCount = 4;
            feedback.ring.SetPositions(new[] { new Vector3(-b.extents.x,0,-b.extents.z),
                new Vector3(-b.extents.x,0,b.extents.z), new Vector3(b.extents.x,0,b.extents.z),
                new Vector3(b.extents.x,0,-b.extents.z) });
        }
        else
        {
            root.transform.position = new Vector3(target.position.x, Mathf.Max(0.13f, target.position.y), target.position.z);
            feedback.ring.positionCount = 64;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64;
                feedback.ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.42f);
            }
        }
    }

    /// <summary>以不受暫停影響的時間收細光圈，結束後回收物件。</summary>
    private void Update()
    {
        if (logic != null && logic.IsClassicChess) { Destroy(gameObject); return; }
        float progress = (Time.unscaledTime - startedAt) / 0.65f;
        if (progress >= 1f) { Destroy(gameObject); return; }
        if (ring != null) ring.widthMultiplier = 0.09f * (1f - progress);
    }

    /// <summary>離開場景或動畫結束時釋放專用材質。</summary>
    private void OnDestroy() { if (material != null) Destroy(material); }
}
