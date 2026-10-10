using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>在計算目標頭上逐步顯示來源與累計，只呈現數字，不修改血量。</summary>
public sealed class DamageResolutionPanel : MonoBehaviour
{
    [SerializeField] private RectTransform bubble;
    [SerializeField] private TMP_Text source, total;
    [SerializeField] private Image icon;
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
        source.text = value.isHealing ? "治療計算" : "傷害計算";
        total.text = "0"; icon.enabled = false;
        total.color = value.isHealing ? new Color(0.4f, 1f, 0.65f) : new Color(1f, 0.78f, 0.38f);
        gameObject.SetActive(true); changedAt = Time.unscaledTime;
        LateUpdate();
    }

    /// <summary>在修正來源頭上顯示圖示與名稱，不逐步顯示累計數字。</summary>
    public void ShowSource(DamageCalculationStep step, Sprite sprite, int stack)
    {
        anchor = step.worldPosition; sourceStack = stack; PresentedSteps = 1;
        source.text = SourceTitle(step); total.text = ""; total.gameObject.SetActive(false);
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
    public void Hide() { gameObject.SetActive(false); }

    /// <summary>跟隨目標格的投影位置，切換來源時稍微上飄。</summary>
    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null || bubble == null) return;
        Vector3 screen = camera.WorldToScreenPoint(anchor + Vector3.up * 1.1f);
        bubble.gameObject.SetActive(screen.z > 0);
        if (screen.z <= 0) return;
        RectTransform root = (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 point);
        point.y += sourceStack * (bubble.rect.height + 8f) + 48 + Mathf.Clamp01((Time.unscaledTime - changedAt) / 0.3f) * 10;
        point.x = Mathf.Clamp(point.x, root.rect.xMin + 110, root.rect.xMax - 110);
        point.y = Mathf.Clamp(point.y, root.rect.yMin + 44, root.rect.yMax - 44);
        bubble.anchoredPosition = point;
    }

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
