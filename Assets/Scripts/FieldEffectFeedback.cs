using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>場地效果的光暈只屬於來源卡片，目標僅播放一次性特效。</summary>
public sealed class FieldEffectFeedback : MonoBehaviour
{
    public RectTransform glowRoot;
    public GameObject targetEffectPrefab;
    public Vector3 effectOffset = new Vector3(0, .5f, 0);
    public Vector3 effectRotation = new Vector3(-45, 0, 0);
    public Vector3 effectScale = Vector3.one * .15f;
    public float effectLifetime = 1f;
    private FieldCardPlace source;
    private string cardId;
    private float deadline;
    private readonly List<GameObject> effects = new List<GameObject>();
    private static readonly List<FieldEffectFeedback> active = new List<FieldEffectFeedback>();

    public static bool Applies(CardDefinition card) => card != null && card.cardType == CardType.Field;

    public static void Play(CardDefinition card, Vector3 targetPosition, float duration)
    {
        if (!Applies(card)) return;
        var prefab = Resources.Load<FieldEffectFeedback>("UI/FieldEffectFeedback");
        if (prefab == null) { Debug.LogError("缺少場地卡特效樣板"); return; }
        FieldEffectFeedback impactOwner = null;
        foreach (var place in FindObjectsByType<FieldCardPlace>(FindObjectsSortMode.None))
        {
            if (place.ActiveCard?.id != card.id) continue;
            var feedback = active.Find(item => item != null && item.source == place && item.cardId == card.id);
            if (feedback == null)
            {
                feedback = Instantiate(prefab);
                feedback.source = place; feedback.cardId = card.id;
                active.Add(feedback);
            }
            feedback.deadline = Time.unscaledTime + Mathf.Max(duration, prefab.effectLifetime);
            feedback.PlaceGlow();
            if (impactOwner == null) impactOwner = feedback;
        }
        // The source may already have left the board by the time a queued effect is presented.
        if (impactOwner == null)
        {
            impactOwner = Instantiate(prefab);
            impactOwner.glowRoot.gameObject.SetActive(false);
            impactOwner.deadline = Time.unscaledTime + Mathf.Max(duration, prefab.effectLifetime);
            active.Add(impactOwner);
        }
        impactOwner.PlayImpact(targetPosition);
    }

    private void PlaceGlow()
    {
        var collider = source.GetComponent<Collider>();
        if (collider == null) { glowRoot.gameObject.SetActive(false); return; }
        Bounds bounds = collider.bounds;
        glowRoot.position = new Vector3(bounds.center.x, bounds.max.y + .025f, bounds.center.z);
        glowRoot.rotation = Quaternion.Euler(90, 0, 0);
        glowRoot.sizeDelta = new Vector2(288, 384);
        glowRoot.localScale = new Vector3(bounds.size.x / 288, bounds.size.z / 384, 1);
        glowRoot.GetComponent<Canvas>().worldCamera = Camera.main;
    }

    private void PlayImpact(Vector3 position)
    {
        if (targetEffectPrefab == null) return;
        var effect = Instantiate(targetEffectPrefab, position + effectOffset, Quaternion.Euler(effectRotation));
        effect.transform.localScale = effectScale;
        effect.SetActive(true);
        effects.Add(effect);
        StartCoroutine(RemoveImpact(effect));
    }

    private IEnumerator RemoveImpact(GameObject effect)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(.05f, effectLifetime));
        effects.Remove(effect);
        if (effect != null) Destroy(effect);
    }

    private void Update()
    {
        if (Time.unscaledTime >= deadline) { Destroy(gameObject); return; }
        if (source != null && source.ActiveCard?.id == cardId) PlaceGlow();
        else glowRoot.gameObject.SetActive(false);
    }
    public static void StopAll()
    {
        foreach (var item in active.ToArray())
            if (item != null) { item.gameObject.SetActive(false); Destroy(item.gameObject); }
        active.Clear();
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        foreach (var effect in effects)
            if (effect != null) { effect.SetActive(false); Destroy(effect); }
        effects.Clear();
    }
    private void OnDestroy() => active.Remove(this);
}
