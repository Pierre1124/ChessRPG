using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>拖曳期间顯示合法目標及游標提示，獨立管理視覺資源的生命週期。</summary>
public sealed class CardTargetFeedback : MonoBehaviour
{
    private CardHandManager hand;
    private CardDefinition card;
    private readonly List<Component> targets = new List<Component>();
    private readonly Dictionary<Component, LineRenderer> markers = new Dictionary<Component, LineRenderer>();
    private readonly List<Component> removed = new List<Component>();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private Material material;
    private TMP_Text hint;
    private RectTransform hintPanel;
    private CardGlowGraphic recycleGlow;
    private float refreshAt;
    private MaterialPropertyBlock tintProperties;
    [SerializeField] private Color ValidColor = new Color(0.12f, 0.86f, 0.94f);
    [SerializeField] private Color FocusColor = new Color(1f, 0.76f, 0.25f);
    [SerializeField, Min(0.001f)] private float normalWidth = 0.035f;
    [SerializeField, Min(0.001f)] private float focusWidth = 0.07f;

    /// <summary>在 Unity 主執行緒生命週期內建立原生材質屬性，避免建構時呼叫原生 API。</summary>
    private void Awake()
    {
        tintProperties = new MaterialPropertyBlock();
    }

    /// <summary>建立單次拖曳使用的獨立提示物件與回收區外光暈。</summary>
    public static CardTargetFeedback Create(CardHandManager manager, CardDefinition definition, TMP_FontAsset font)
    {
        var root = SceneObjectTemplates.Spawn("Card target feedback", manager.transform);
        root.transform.SetParent(manager.transform, false);
        var feedback = root.GetComponent<CardTargetFeedback>();
        feedback.hand = manager;
        feedback.card = definition;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) feedback.material = new Material(shader);
        feedback.CreateHint(font);
        if (manager.RecycleCardRoot != null) feedback.recycleGlow = CardGlowGraphic.Create(manager.RecycleCardRoot, false);
        return feedback;
    }

    /// <summary>更新目標集合、游標焦點與放置提示；不修改棋盤格原有高亮。</summary>
    public void ShowAt(PointerEventData pointer)
    {
        if (hand == null || !hand.CanPreviewCard(card)) { ClearVisuals(); return; }
        if (Time.unscaledTime >= refreshAt)
        {
            hand.CollectCardTargets(card, targets);
            removed.Clear();
            foreach (var entry in markers)
                if (entry.Key == null || !targets.Contains(entry.Key)) removed.Add(entry.Key);
            foreach (Component target in removed)
            {
                Destroy(markers[target].gameObject);
                markers.Remove(target);
            }
            foreach (Component target in targets)
                if (!markers.ContainsKey(target)) AddMarker(target);
            refreshAt = Time.unscaledTime + 0.15f;
        }

        bool overRecycle = hand.RecycleCardRoot != null && hand.RecycleCardRoot.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(hand.RecycleCardRoot, pointer.position, pointer.pressEventCamera);
        if (recycleGlow != null)
        {
            recycleGlow.enabled = true;
            recycleGlow.color = overRecycle ? FocusColor : ValidColor;
        }
        Component hovered = null;
        uiHits.Clear();
        if (EventSystem.current != null) EventSystem.current.RaycastAll(pointer, uiHits);
        if (!overRecycle && uiHits.Count == 0 && Camera.main != null &&
            Physics.Raycast(Camera.main.ScreenPointToRay(pointer.position), out RaycastHit hit))
        {
            hovered = card.cardType == CardType.Field
                ? (Component)hit.transform.GetComponentInParent<FieldCardPlace>()
                : hit.transform.GetComponentInParent<Piece>();
        }
        bool validHover = hovered != null && markers.ContainsKey(hovered);
        foreach (var entry in markers)
        {
            entry.Value.gameObject.SetActive(true);
            bool focused = entry.Key == hovered;
            entry.Value.startColor = entry.Value.endColor = focused ? FocusColor : ValidColor;
            tintProperties.SetColor("_BaseColor", focused ? FocusColor : ValidColor);
            tintProperties.SetColor("_Color", focused ? FocusColor : ValidColor);
            entry.Value.SetPropertyBlock(tintProperties);
            entry.Value.widthMultiplier = focused ? focusWidth : normalWidth;
        }
        hint.text = overRecycle ? "放開以回收" : validHover ? "放開以使用" :
            hovered != null ? "此目標不符合卡牌條件" : targets.Count == 0 ? "目前沒有可使用目標，可拖至回收區" :
            card.cardType == CardType.Field ? "拖至亮起的場地欄位" : "拖至有光圈的棋子";
        hint.color = overRecycle || validHover ? FocusColor : Color.white;
        hintPanel.gameObject.SetActive(true);
        RectTransform parent = (RectTransform)hintPanel.parent;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pointer.position, pointer.pressEventCamera, out Vector2 local))
        {
            float x = Mathf.Clamp(local.x + 24f, parent.rect.xMin, Mathf.Max(parent.rect.xMin, parent.rect.xMax - hintPanel.rect.width));
            float y = Mathf.Clamp(local.y + 35f, parent.rect.yMin + hintPanel.rect.height, parent.rect.yMax);
            hintPanel.localPosition = new Vector3(x, y, 0);
        }
    }

    /// <summary>依棋子或場地碰撞範圍畫出圓環或矩形框線。</summary>
    private void AddMarker(Component target)
    {
        if (material == null) return;
        var go = SceneObjectTemplates.Spawn("Legal card target", transform);
        go.transform.SetParent(transform);
        var line = go.GetComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.loop = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // URP Unlit 使用基底色而非頂點色，個別目標以材質屬性覆寫顏色。
        if (target is FieldCardPlace)
        {
            Bounds b = target.GetComponent<Collider>().bounds;
            float y = b.max.y + 0.025f;
            line.positionCount = 4;
            line.SetPositions(new[] { new Vector3(b.min.x,y,b.min.z), new Vector3(b.min.x,y,b.max.z),
                new Vector3(b.max.x,y,b.max.z), new Vector3(b.max.x,y,b.min.z) });
        }
        else
        {
            Vector3 center = target.transform.position;
            center.y = Mathf.Max(0.11f, center.y);
            line.positionCount = 64;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.38f);
            }
        }
        markers.Add(target, line);
    }

    /// <summary>建立不攔截滑鼠的提示文字，沿用手牌中文字型。</summary>
    private void CreateHint(TMP_FontAsset font)
    {
        var go = SceneObjectTemplates.Spawn("Card drop hint", hand.CardGameUiRoot);
        hintPanel = go.GetComponent<RectTransform>();
        hint = go.GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>立即隱藏提示，避免 Destroy 延後一幀留下可見物件。</summary>
    public void ClearVisuals()
    {
        foreach (var entry in markers) if (entry.Value != null) entry.Value.gameObject.SetActive(false);
        if (hintPanel != null) hintPanel.gameObject.SetActive(false);
        if (recycleGlow != null) recycleGlow.enabled = false;
    }

    /// <summary>停用時同步清除所有附掛在其他 UI 階層的顯示。</summary>
    private void OnDisable() { ClearVisuals(); }

    /// <summary>回收此拖曳專用的材質、提示與回收區光暈。</summary>
    private void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (hintPanel != null) Destroy(hintPanel.gameObject);
        if (recycleGlow != null) Destroy(recycleGlow.gameObject);
    }
}
