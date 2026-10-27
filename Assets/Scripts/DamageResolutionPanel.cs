using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>在計算目標頭上逐步顯示來源與累計，只呈現數字，不修改血量。</summary>
public sealed class DamageResolutionPanel : MonoBehaviour
{
    [SerializeField] private RectTransform bubble;
    [SerializeField] private TMP_Text source, total;
    [SerializeField] private Image icon;
    [Header("Piece anchored icon")]
    [SerializeField] private Vector3 pieceOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField, Min(0.001f)] private float worldUnitsPerPixel = 0.0125f;
    [SerializeField, Min(0f)] private float stackSpacing = 0.9f;
    [SerializeField, Min(0f)] private float floatDistance = 0.12f;
    private Transform poolParent;
    private Transform attachedPiece;
    public Transform AttachedPiece => attachedPiece;
    private DamageCalculationSequence sequence;
    private Vector3 anchor;
    private long running;
    private bool known;
    private float changedAt;
    private int sourceStack;
    public string DisplayedTotal => total != null ? total.text : "";
    public int PresentedSteps { get; private set; }

    /// <summary>建立不攔截操作的棋子浮動 UI。</summary>
    public static DamageResolutionPanel Create(TMP_FontAsset font)
    {
        if (Application.isPlaying) return SceneObjectTemplates.Spawn("Damage calculation", null, false).GetComponent<DamageResolutionPanel>();
        var go = new GameObject("Piece damage calculation", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
        var view = go.AddComponent<DamageResolutionPanel>();
        view.bubble = Rect("Floating calculation", go.transform, Vector2.zero, new Vector2(210, 78));
        var background = view.bubble.gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.075f, 0.92f); background.raycastTarget = false;
        view.icon = Rect("Source icon", view.bubble, new Vector2(-82, 16), new Vector2(27, 27)).gameObject.AddComponent<Image>();
        view.icon.preserveAspect = true; view.icon.raycastTarget = false;
        view.source = Label(view.bubble, font, new Vector2(16, 17), new Vector2(168, 31), 21);
        view.total = Label(view.bubble, font, new Vector2(0, -18), new Vector2(200, 38), 28);
        go.SetActive(false); return view;
    }

    /// <summary>在攻擊移除棋子前保存位置，後續仍可在同一格頭上顯示。</summary>
    public void Begin(DamageCalculationSequence value, int index, int count)
    {
        sourceStack = 0; total.gameObject.SetActive(true);
        sequence = value; running = 0; known = true; PresentedSteps = 0;
        anchor = value.target != null ? value.target.transform.position : value.resultStartWorldPosition;
        AttachToPiece(value.target != null ? value.target.transform : null);
        source.text = value.isHealing ? "治療計算" : "傷害計算";
        total.text = "0"; icon.enabled = false;
        total.color = value.isHealing ? new Color(0.4f, 1f, 0.65f) : new Color(1f, 0.78f, 0.38f);
        gameObject.SetActive(true); changedAt = Time.unscaledTime;
        LateUpdate();
    }

    /// <summary>在修正來源頭上只顯示圖示，計算數字交由固定 FinalDMG 呈現。</summary>
    public void ShowSource(DamageCalculationStep step, Sprite sprite, int stack, Piece target = null, Transform sourceOverride = null)
    {
        anchor = step.worldPosition; sourceStack = stack; PresentedSteps = 1;
        AttachToPiece(sourceOverride != null ? sourceOverride : step.iconPiece != null ? step.iconPiece.transform : target != null ? target.transform : null);
        source.text = ""; source.gameObject.SetActive(false);
        total.text = ""; total.gameObject.SetActive(false);
        Image background = bubble.GetComponent<Image>();
        if (background != null) background.enabled = false;
        icon.sprite = sprite; icon.enabled = sprite != null;
        gameObject.SetActive(true); changedAt = Time.unscaledTime;
        LateUpdate();
    }

    /// <summary>依照原始順序更新同一浮動框，顯示本次增減與累計。</summary>
    public void Append(DamageCalculationStep step, Sprite sprite, int index, int count)
    {
        if (step == null) return;
        long previous = running;
        if (step.hasContribution) running += step.contribution; else known = false;
        source.text = SourceTitle(step);
        string change = step.hasContribution ? (step.contribution >= 0 ? "+" : "−") + System.Math.Abs((long)step.contribution) : step.displayText;
        total.text = known ? (PresentedSteps == 0 ? running.ToString() : previous + " " + change + " = " + running) : change;
        icon.sprite = sprite; icon.enabled = sprite != null;
        PresentedSteps++; changedAt = Time.unscaledTime;
    }

    /// <summary>顯示正式結果，等待所有目標計算完才由外部結算。</summary>
    public void Complete()
    {
        source.text = sequence.isHealing ? "回復生命" : "最終傷害";
        total.text = sequence.finalDamage.ToString(); icon.enabled = false;
    }

    /// <summary>隱藏此目標的演出並保留物件供下次重用。</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
        attachedPiece = null;
        transform.SetParent(poolParent, false);
    }

    /// <summary>圖示成為來源棋子的子物件；棋子已移除時改用保存的棋盤座標。</summary>
    private void AttachToPiece(Transform owner)
    {
        if (poolParent == null) poolParent = transform.parent;
        attachedPiece = owner;
        transform.SetParent(owner, false);
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.overrideSorting = true;
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = bubble.sizeDelta;
        bubble.anchoredPosition = Vector2.zero;
    }

    /// <summary>以棋子的世界座標定位並面向鏡頭；抵消棋子縮放，避免不同棋種圖示大小不一致。</summary>
    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null || bubble == null) return;
        if (attachedPiece != null) anchor = attachedPiece.position;
        float rise = Mathf.Clamp01((Time.unscaledTime - changedAt) / 0.3f) * floatDistance;
        transform.position = anchor + pieceOffset + Vector3.up * (sourceStack * stackSpacing + rise);
        transform.rotation = camera.transform.rotation;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(SafeScale(parentScale.x), SafeScale(parentScale.y), SafeScale(parentScale.z));
        bubble.gameObject.SetActive(camera.WorldToScreenPoint(transform.position).z > 0);
    }

    /// <summary>將 Prefab 像素尺寸換算為世界尺寸，並避開零縮放除法。</summary>
    private float SafeScale(float scale) => Mathf.Abs(scale) > 0.0001f ? worldUnitsPerPixel / Mathf.Abs(scale) : worldUnitsPerPixel;

    /// <summary>以正式來源名稱優先，舊資料提供可讀替代名稱。</summary>
    public static string SourceTitle(DamageCalculationStep step)
    {
        if (!string.IsNullOrEmpty(step.title)) return step.title;
        if (step.iconPiece != null) return step.iconPiece.PieceType;
        return step.countingIcon == DamageCountingIcon.Heal ? "治療來源" : "傷害修正";
    }

    /// <summary>建立置中的 UI 矩形。</summary>
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }

    /// <summary>建立可縮放的中文文字，不攔截棋盤操作。</summary>
    private static TMP_Text Label(Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize)
    {
        var text = Rect("Text", parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = fontSize; text.enableAutoSizing = true; text.fontSizeMin = 14; text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.richText = false;
        return text;
    }
}
