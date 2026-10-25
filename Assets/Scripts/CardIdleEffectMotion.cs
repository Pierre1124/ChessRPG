using System;
using UnityEngine;

/// <summary>純外觀循環：不修改棋子規則，生命週期由既有狀態接口管理。</summary>
public sealed class CardIdleEffectMotion : MonoBehaviour
{
    [Serializable]
    public sealed class Element
    {
        public Transform target;
        public Vector3 position;
        public float turnsPerLoop;
        public float bob;
        public float phase;
        [Range(0, .5f)] public float breathe;
    }

    [Min(.1f)] public float loopSeconds = 4f;
    [Min(.05f)] public float worldRadius = .42f;
    public Transform content;
    public Element[] elements = new Element[0];
    private float elapsed;

    private void OnEnable()
    {
        elapsed = 0;
        FitToPiece();
        Evaluate(0);
    }

    /// <summary>抵銷棋子 Prefab 的縮放，以一格內的半徑及實際模型高度呈現。</summary>
    public void FitToPiece()
    {
        if (content == null) return;
        var owner = GetComponentInParent<Piece>();
        var scale = transform.lossyScale;
        float height = .8f;
        Vector3 bottom = transform.position;
        if (owner != null)
        {
            bool found = false;
            Bounds bounds = default;
            foreach (var renderer in owner.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.transform.IsChildOf(transform)) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (found)
            {
                height = Mathf.Clamp(bounds.size.y, .35f, 1.8f);
                bottom.y = bounds.min.y + .025f;
            }
        }
        content.localPosition = transform.InverseTransformPoint(bottom);
        content.localScale = new Vector3(worldRadius / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            height / Mathf.Max(.001f, Mathf.Abs(scale.y)), worldRadius / Mathf.Max(.001f, Mathf.Abs(scale.z)));
    }

    private void Update()
    {
        elapsed = Mathf.Repeat(elapsed + Time.deltaTime, Mathf.Max(.1f, loopSeconds));
        Evaluate(elapsed);
    }

    /// <summary>絕對時間取樣，供預覽及循環端點驗證；每幀不建立物件或材質。</summary>
    public void Evaluate(float seconds)
    {
        float t = seconds / Mathf.Max(.1f, loopSeconds) * Mathf.PI * 2;
        foreach (var e in elements)
        {
            if (e.target == null) continue;
            float wave = Mathf.Sin(t + e.phase);
            e.target.localPosition = e.position + Vector3.up * (e.bob * wave);
            e.target.localRotation = Quaternion.Euler(0, t * Mathf.Rad2Deg * e.turnsPerLoop, 0);
            e.target.localScale = Vector3.one * (1 - e.breathe + e.breathe * wave);
        }
    }
}
