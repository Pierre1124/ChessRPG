using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DamageCalculationSequence
{
    public DamageContext damageContext;
    public Piece attacker;
    public Piece target;
    public bool damagedWhitePlayer;
    public bool isHealing;
    public bool isCapture;
    public int finalDamage;
    public Vector3 resultStartWorldPosition;
    public readonly List<DamageCalculationStep> steps =
        new List<DamageCalculationStep>();
}

public enum DamageCountingIcon
{
    None,
    Attack,
    Defense,
    Heal,
    AntiAttack,
    AntiHeal,
    Fire,
    Poison,
    Curse,
    Cost
}

public enum DamageStepSide
{
    Neutral,
    Attack,
    Defense
}

public enum DamageModifierPhase { None, Increase, Decrease }

public class DamageCalculationStep
{
    public DamageModifierPhase modifierPhase;
    public string sourceCardId;
    public bool hasContribution;
    public int contribution;
    public Sprite icon;
    public Piece iconPiece;
    public string title;
    public string detail;
    public string displayText;
    public Vector3 worldPosition;
    public Color color = Color.white;
    public bool usePieceIcon;
    public bool useTargetPieceIcon;
    public DamageCountingIcon countingIcon;
    public DamageStepSide side;
}

[RequireComponent(typeof(RectTransform))]
public class DamageCalculationVisualizer : MonoBehaviour
{
    /// <summary>
    /// 停止傷害演出並隱藏所有步驟及結果物件，供普通西洋棋模式使用。
    /// </summary>
    public void DisableForClassicChess()
    {
        StopAllCoroutines();
        RestoreHealthPositions();
        HideDamageCalcSteps();
        HideDamageResultFlies();
        HidePrebuiltObjects();
        gameObject.SetActive(false);
    }

    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform root;
    [SerializeField] private Transform whiteHealthTarget;
    [SerializeField] private Transform blackHealthTarget;
    [SerializeField] public ChessCard imageDatabase;

    [Header("Step Prefab")]
    [SerializeField] private GameObject damageCalcStepPrefab;

    [Header("Prebuilt Result UI")]
    [SerializeField] private RectTransform damageResultFly;
    [SerializeField] private Image damageResultFlyBackground;
    [SerializeField] private TMP_Text damageResultFlyText;
    [SerializeField] private TMP_FontAsset resolutionFont;

    [Header("Counting Icons")]
    [SerializeField] private Sprite attackIcon;
    [SerializeField] private Sprite defenseIcon;
    [SerializeField] private Sprite healIcon;
    [SerializeField] private Sprite antiAttackIcon;
    [SerializeField] private Sprite antiHealIcon;
    [SerializeField] private Sprite fireIcon;
    [SerializeField] private Sprite poisonIcon;
    [SerializeField] private Sprite curseIcon;
    [SerializeField] private Sprite costIcon;

    [Header("Timing")]
    [SerializeField, Min(0.05f)] private float stepDuration = 0.65f;
    [SerializeField, Min(0f)] private float captureDelay = 0.35f;
    [SerializeField, Min(0.05f)] private float flyDuration = 0.55f;
    [SerializeField, Min(0.05f)] private float clashDuration = 0.4f;

    [Header("Final result and impact")]
    [SerializeField, Min(0f)] private float resultHoldDuration = 0.35f;
    [SerializeField] private Vector2 resultOffset = new Vector2(0f, 80f);
    [SerializeField, Min(1f)] private float flightAcceleration = 2.5f;
    [SerializeField, Min(0.01f)] private float healthShakeDuration = 0.25f;
    [SerializeField, Min(0f)] private float healthShakeDistance = 8f;
    private readonly Dictionary<Transform, Vector3> healthOrigins = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Coroutine> healthShakes = new Dictionary<Transform, Coroutine>();
    public DamageModifierPhase CurrentModifierPhase { get; private set; }
    public int VisibleModifierCount { get; private set; }

    [Header("Layout")]
    [SerializeField] private Vector2 worldOffset = new Vector2(0f, 80f);
    [SerializeField] private Vector2 repeatedStepOffset = new Vector2(0f, 44f);
    [SerializeField] private Color damageColor = new Color(1f, 0.24f, 0.18f, 1f);
    [SerializeField] private Color healColor = new Color(0.4f, 1f, 0.55f, 1f);

    private readonly List<RectTransform> activeStepViews =
        new List<RectTransform>();
    private readonly List<RectTransform> damageCalcStepPool =
        new List<RectTransform>();
    private readonly List<DamageStepLayout> damageCalcStepLayouts =
        new List<DamageStepLayout>();
    private readonly List<DamageResultFlyView> damageResultFlyPool =
        new List<DamageResultFlyView>();
    private readonly List<DamageResolutionPanel> resolutionPanels = new List<DamageResolutionPanel>();
    [SerializeField] private Font resolutionSourceFont;
    private TMP_FontAsset runtimeResolutionFont;

    /// <summary>
    /// 補齊傷害演出所需的 UI 引用及數值圖示。
    /// </summary>
    private void Awake()
    {
        AutoBindReferences();
        AutoBindCountingIcons();
    }

    /// <summary>
    /// 重設元件時補齊引用與圖示，並隱藏預建樣板。
    /// </summary>
    private void Reset()
    {
        AutoBindReferences();
        AutoBindCountingIcons();
        HidePrebuiltObjects();
    }

    /// <summary>
    /// Inspector 資料變更時補齊演出引用及圖示。
    /// </summary>
    private void OnValidate()
    {
        AutoBindReferences();
        AutoBindCountingIcons();
    }

    /// <summary>
    /// 依既有命名與階層規則補齊 UI 或動畫引用。
    /// </summary>
    [ContextMenu("Auto Bind References")]
    private void AutoBindReferences()
    {
        if (root == null)
        {
            root = transform as RectTransform;
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }

        if (whiteHealthTarget == null)
        {
            whiteHealthTarget = FindNamedTransform("WhiteHP");
        }

        if (blackHealthTarget == null)
        {
            blackHealthTarget = FindNamedTransform("BlackHP");
        }

        if (damageResultFly == null)
        {
            Transform fly = FindChildRecursive(transform, "DamageResultFly");
            damageResultFly = fly as RectTransform;
        }

        if (damageResultFly != null)
        {
            if (damageResultFlyBackground == null)
            {
                damageResultFlyBackground =
                    damageResultFly.GetComponent<Image>();
            }

            if (damageResultFlyText == null)
            {
                damageResultFlyText =
                    FindChildComponent<TMP_Text>(
                        damageResultFly,
                        "DamageText"
                    );
            }
        }
    }

    /// <summary>
    /// 啟動單一傷害演出；缺少必要引用時仍呼叫結算回呼。
    /// </summary>
    public void Play(
        DamageCalculationSequence sequence,
        Action onCaptureVisual,
        Action onDamageApplied
    )
    {
        AutoBindCountingIcons();

        if (!HasRequiredReferences())
        {
            Debug.LogError(
                "DMGSystem DamageCalculationVisualizer references are not assigned. " +
                $"Canvas={canvas != null}, Root={root != null}, " +
                $"WhiteHP={whiteHealthTarget != null}, " +
                $"BlackHP={blackHealthTarget != null}, " +
                $"ImageDatabase={imageDatabase != null}, " +
                $"DamageCalcStepPrefab={damageCalcStepPrefab != null}, " +
                $"DamageResultFly={damageResultFly != null}, " +
                $"DamageResultFlyText={damageResultFlyText != null}"
            );
            onCaptureVisual?.Invoke();
            onDamageApplied?.Invoke();
            return;
        }

        HidePrebuiltObjects();
        StartCoroutine(PlayRoutine(sequence, onCaptureVisual, onDamageApplied));
    }

    /// <summary>
    /// 啟動一批傷害計算序列的演出。
    /// </summary>
    public void PlayBatch(
        IReadOnlyList<DamageCalculationSequence> sequences,
        IReadOnlyList<Action> onCaptureVisuals,
        IReadOnlyList<Action> onDamageApplied,
        Action onComplete
    )
    {
        AutoBindCountingIcons();

        if (!HasRequiredReferences())
        {
            Debug.LogError(
                "DMGSystem DamageCalculationVisualizer references are not assigned. " +
                $"Canvas={canvas != null}, Root={root != null}, " +
                $"WhiteHP={whiteHealthTarget != null}, " +
                $"BlackHP={blackHealthTarget != null}, " +
                $"ImageDatabase={imageDatabase != null}, " +
                $"DamageCalcStepPrefab={damageCalcStepPrefab != null}, " +
                $"DamageResultFly={damageResultFly != null}, " +
                $"DamageResultFlyText={damageResultFlyText != null}"
            );
            InvokeAll(onCaptureVisuals);
            InvokeAll(onDamageApplied);
            onComplete?.Invoke();
            return;
        }

        HidePrebuiltObjects();
        StartCoroutine(
            PlayBatchRoutine(
                sequences,
                onCaptureVisuals,
                onDamageApplied,
                onComplete
            )
        );
    }

    /// <summary>
    /// 依序執行此元件的演出步驟，完成後處理回呼與清理。
    /// </summary>
    private IEnumerator PlayRoutine(
        DamageCalculationSequence sequence,
        Action onCaptureVisual,
        Action onDamageApplied
    )
    {
        if (sequence == null)
        {
            onCaptureVisual?.Invoke();
            onDamageApplied?.Invoke();
            yield break;
        }

        yield return PlayBatchRoutine(new[] { sequence }, new[] { onCaptureVisual }, new[] { onDamageApplied }, null);
    }

    /// <summary>每個目標同時播放自己的計算步驟，所有目標完成後才結算血量。</summary>
    private IEnumerator PlayBatchRoutine(
        IReadOnlyList<DamageCalculationSequence> sequences,
        IReadOnlyList<Action> onCaptureVisuals,
        IReadOnlyList<Action> onDamageApplied,
        Action onComplete)
    {
        if (sequences == null || sequences.Count == 0)
        {
            InvokeAll(onCaptureVisuals); InvokeAll(onDamageApplied); onComplete?.Invoke();
            yield break;
        }
        HideDamageCalcSteps();
        // 出牌展示與傷害佇列可同時啟動；先等既有出牌動畫完成再播來源階段。
        CardHandManager hand = FindFirstObjectByType<CardHandManager>();
        while (hand != null && hand.isActiveAndEnabled && hand.IsUsingCardAnimationPlaying) yield return null;
        InvokeAll(onCaptureVisuals);
        float initialDuration = captureDelay;
        foreach (var sequence in sequences)
        {
            CardDefinition sourceCard = sequence?.damageContext?.sourceCard;
            if (sourceCard == null && sequence != null)
                foreach (var step in sequence.steps)
                    if (step != null && step.modifierPhase == DamageModifierPhase.None && !string.IsNullOrEmpty(step.sourceCardId))
                    { sourceCard = imageDatabase.GetCard(step.sourceCardId); break; }
            if (sourceCard == null) continue;
            foreach (var animation in sourceCard.animations)
                if (animation != null && animation.timing == CardAnimationTiming.OnDamageDealt)
                    initialDuration = Mathf.Max(initialDuration, animation.effectLifetime);
        }
        yield return new WaitForSecondsRealtime(initialDuration);
        yield return PlayModifierPhase(sequences, DamageModifierPhase.Increase);
        yield return PlayModifierPhase(sequences, DamageModifierPhase.Decrease);
        var flyingSequences = new List<DamageCalculationSequence>();
        var flyingViews = new List<DamageResultFlyView>();
        for (int i = 0; i < sequences.Count; i++)
        {
            DamageCalculationSequence sequence = sequences[i];
            if (sequence == null) continue;
            DamageResultFlyView view = GetOrCreateResultFlyView(flyingViews.Count);
            ShowDamageResultFly(sequence, view);
            flyingSequences.Add(sequence); flyingViews.Add(view);
        }
        if (flyingViews.Count > 0)
        {
            yield return new WaitForSecondsRealtime(resultHoldDuration);
            // 歸零仍顯示最終 0，但不飛向血條或產生受傷震動。
            for (int i = flyingViews.Count - 1; i >= 0; i--)
                if (flyingSequences[i].finalDamage <= 0)
                { flyingViews[i].root.gameObject.SetActive(false); flyingViews.RemoveAt(i); flyingSequences.RemoveAt(i); }
            if (flyingViews.Count > 0) yield return FlyBatchToHealthTargets(flyingSequences, flyingViews);
        }
        HideDamageResultFlies();
        var impacted = new HashSet<Transform>();
        foreach (var sequence in flyingSequences)
        {
            Transform health = sequence.damagedWhitePlayer ? whiteHealthTarget : blackHealthTarget;
            if (!sequence.isHealing && health != null && impacted.Add(health)) StartHealthShake(health);
        }
        InvokeAll(onDamageApplied);
        onComplete?.Invoke();
    }

    /// <summary>同一階段所有來源在同一幀啟動，等待最長的來源演出後一起收起。</summary>
    private IEnumerator PlayModifierPhase(IReadOnlyList<DamageCalculationSequence> sequences, DamageModifierPhase phase)
    {
        CurrentModifierPhase = phase;
        var shown = new HashSet<string>();
        var positions = new Dictionary<Vector3, int>();
        float duration = Mathf.Max(0.05f, stepDuration);
        foreach (var sequence in sequences)
        {
            if (sequence == null) continue;
            foreach (var step in sequence.steps)
            {
                if (step == null || step.modifierPhase != phase) continue;
                string key = step.sourceCardId + "|" + step.title + "|" + step.worldPosition.ToString("R");
                if (!shown.Add(key)) continue;
                positions.TryGetValue(step.worldPosition, out int stack);
                positions[step.worldPosition] = stack + 1;
                CardDefinition card = string.IsNullOrEmpty(step.sourceCardId) ? null : imageDatabase.GetCard(step.sourceCardId);
                Sprite sprite = step.icon;
                if (sprite == null && card != null) sprite = card.skillImage != null ? card.skillImage : card.cardImage;
                if (sprite == null) sprite = ResolveIcon(step, sequence) ?? GetCountingIcon(step.countingIcon);
                GetResolutionPanel(VisibleModifierCount++).ShowSource(step, sprite, stack);
                if (card == null) continue;
                CardAnimationEvents.Play(step.iconPiece, sequence.target, card, CardAnimationTiming.OnDamageModifier);
                foreach (var animation in card.animations)
                    if (animation != null && animation.timing == CardAnimationTiming.OnDamageModifier)
                        duration = Mathf.Max(duration, animation.effectLifetime);
            }
        }
        if (VisibleModifierCount > 0) yield return new WaitForSecondsRealtime(duration);
        HideResolutionPanels(); VisibleModifierCount = 0;
        CurrentModifierPhase = DamageModifierPhase.None;
    }

    /// <summary>同一血條重複受擊時重啟震動，先還原原始位置避免漂移。</summary>
    private void StartHealthShake(Transform health)
    {
        if (healthShakes.TryGetValue(health, out Coroutine previous)) StopCoroutine(previous);
        if (!healthOrigins.ContainsKey(health)) healthOrigins.Add(health, health.localPosition);
        health.localPosition = healthOrigins[health];
        healthShakes[health] = StartCoroutine(ShakeHealth(health));
    }

    /// <summary>命中時衰減震動；正常完成或取消均還原血條座標。</summary>
    private IEnumerator ShakeHealth(Transform health)
    {
        Vector3 origin = healthOrigins[health];
        float elapsed = 0f;
        while (elapsed < healthShakeDuration && health != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, healthShakeDuration));
            health.localPosition = origin + Vector3.right * Mathf.Sin(t * Mathf.PI * 8f) * (1f - t) * healthShakeDistance;
            yield return null;
        }
        if (health != null) health.localPosition = origin;
        healthOrigins.Remove(health); healthShakes.Remove(health);
    }

    /// <summary>取消時還原所有正在震動的血條。</summary>
    private void RestoreHealthPositions()
    {
        foreach (var pair in healthOrigins) if (pair.Key != null) pair.Key.localPosition = pair.Value;
        healthOrigins.Clear(); healthShakes.Clear(); VisibleModifierCount = 0; CurrentModifierPhase = DamageModifierPhase.None;
    }

    /// <summary>重用各目標的浮動框，中文字型由專案字型動態產生。</summary>
    private DamageResolutionPanel GetResolutionPanel(int index)
    {
        // 傷害字型由場景樣板指定，不再於執行期建立另一套字型。
        while (resolutionPanels.Count <= index)
            resolutionPanels.Add(DamageResolutionPanel.Create(runtimeResolutionFont != null ? runtimeResolutionFont : resolutionFont));
        return resolutionPanels[index];
    }

    /// <summary>隱藏所有目標的浮動 UI。</summary>
    private void HideResolutionPanels()
    {
        foreach (DamageResolutionPanel panel in resolutionPanels) if (panel != null) panel.Hide();
    }

    /// <summary>停用時停止演出及隱藏外部 Canvas，避免延遲結算。</summary>
    private void OnDisable() { StopAllCoroutines(); RestoreHealthPositions(); HideDamageCalcSteps(); HideDamageResultFlies(); HideResolutionPanels(); }

    /// <summary>離開場景時回收浮動 UI 及動態中文字型。</summary>
    private void OnDestroy()
    {
        foreach (DamageResolutionPanel panel in resolutionPanels) if (panel != null) Destroy(panel.gameObject);
        if (runtimeResolutionFont != null)
        {
            foreach (Texture2D texture in runtimeResolutionFont.atlasTextures) if (texture != null) Destroy(texture);
            Destroy(runtimeResolutionFont.material);
            Destroy(runtimeResolutionFont);
        }
    }

    /// <summary>
    /// 顯示單一步驟的數值、圖示與位置。
    /// </summary>
    private void ShowDamageCalcStep(
        RectTransform view,
        DamageCalculationStep step,
        DamageCalculationSequence sequence,
        int stackIndex
    )
    {
        Sprite countingSprite = GetCountingIcon(step.countingIcon);
        if (step.countingIcon != DamageCountingIcon.None &&
            countingSprite == null)
        {
            Debug.LogWarning(
                $"[DMGSystem] Missing CountingIcon sprite: {step.countingIcon}"
            );
        }
        Image icon = FindChildComponent<Image>(view, "Icon");
        Image countingIcon = FindChildComponent<Image>(view, "CountingIcon");
        TMP_Text text = FindChildComponent<TMP_Text>(view, "Text");
        RestoreStepLayout(view);
        view.SetAsLastSibling();
        view.anchoredPosition =
            WorldToRootAnchoredPosition(step.worldPosition) +
            worldOffset +
            repeatedStepOffset * stackIndex;

        if (icon != null)
        {
            Sprite sprite = ResolveIcon(step, sequence);
            CardDefinition card = string.IsNullOrEmpty(step.sourceCardId) ? null : imageDatabase.GetCard(step.sourceCardId);
            if (sprite == null && card != null) sprite = card.skillImage != null ? card.skillImage : card.cardImage;
            icon.sprite = sprite; icon.preserveAspect = true;
            icon.gameObject.SetActive(sprite != null);
        }

        if (countingIcon != null)
        {
            countingIcon.sprite = countingSprite;
            countingIcon.preserveAspect = true;
            countingIcon.color = Color.white;
            countingIcon.transform.SetAsLastSibling();
            countingIcon.gameObject.SetActive(countingSprite != null);
        }

        if (text != null)
        {
            text.color = step.color;
            text.text = DamageResolutionPanel.SourceTitle(step);
        }

        view.gameObject.SetActive(true);
    }

    /// <summary>
    /// 隱藏本次傷害計算使用的步驟物件。
    /// </summary>
    private void HideDamageCalcSteps()
    {
        foreach (RectTransform view in damageCalcStepPool)
        {
            if (view != null) view.gameObject.SetActive(false);
        }
        activeStepViews.Clear();
    }

    /// <summary>
    /// 取得可重用的計算步驟視圖，必要時建立新物件。
    /// </summary>
    private RectTransform GetOrCreateStepView(int index)
    {
        while (damageCalcStepPool.Count <= index)
        {
            GameObject instance = Instantiate(
                damageCalcStepPrefab,
                root,
                false
            );
            instance.name = "DamageCalcStep";
            instance.SetActive(false);

            RectTransform view = instance.GetComponent<RectTransform>();
            if (view == null)
            {
                Destroy(instance);
                throw new InvalidOperationException(
                    "DamageCalcStep prefab requires RectTransform."
                );
            }

            damageCalcStepPool.Add(view);
            damageCalcStepLayouts.Add(new DamageStepLayout(view));
        }

        return damageCalcStepPool[index];
    }

    /// <summary>
    /// 將計算步驟視圖還原到記錄的版面配置。
    /// </summary>
    private void RestoreStepLayout(RectTransform view)
    {
        int index = damageCalcStepPool.IndexOf(view);
        if (index < 0 || index >= damageCalcStepLayouts.Count)
        {
            return;
        }

        damageCalcStepLayouts[index].ApplyTo(view);
    }

    /// <summary>
    /// 播放傷害計算中雙方碰撞的視覺效果。
    /// </summary>
    private IEnumerator PlayClash(
        DamageCalculationSequence sequence,
        Action onCaptureVisual
    )
    {
        List<RectTransform> attackViews = new List<RectTransform>();
        List<Vector3> startPositions = new List<Vector3>();

        for (int i = 0; i < sequence.steps.Count && i < activeStepViews.Count; i++)
        {
            if (sequence.steps[i].side != DamageStepSide.Attack) continue;
            attackViews.Add(activeStepViews[i]);
            startPositions.Add(activeStepViews[i].position);
        }

        if (attackViews.Count == 0)
        {
            onCaptureVisual?.Invoke();
            yield return new WaitForSecondsRealtime(captureDelay);
            yield break;
        }

        Vector3 targetPosition =
            WorldToCanvasPosition(sequence.target.transform.position) +
            (Vector3)worldOffset;
        onCaptureVisual?.Invoke();
        float elapsed = 0f;

        while (elapsed < clashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / clashDuration);
            float strike = Mathf.Sin(t * Mathf.PI) * 0.65f;

            for (int i = 0; i < attackViews.Count; i++)
            {
                attackViews[i].position = Vector3.Lerp(
                    startPositions[i],
                    targetPosition,
                    strike
                );
            }

            yield return null;
        }

        for (int i = 0; i < attackViews.Count; i++)
        {
            attackViews[i].position = startPositions[i];
        }
    }

    /// <summary>
    /// 建立或顯示飛向血條的傷害或治療結果。
    /// </summary>
    private void ShowDamageResultFly(DamageCalculationSequence sequence)
    {
        ShowDamageResultFly(sequence, GetOrCreateResultFlyView(0));
    }

    /// <summary>
    /// 建立或顯示飛向血條的傷害或治療結果。
    /// </summary>
    private void ShowDamageResultFly(
        DamageCalculationSequence sequence,
        DamageResultFlyView view
    )
    {
        if (view == null || view.root == null || view.text == null)
        {
            return;
        }

        view.root.position = WorldToCanvasPosition(sequence.resultStartWorldPosition) + (Vector3)resultOffset;

        if (view.canvasGroup != null)
        {
            view.canvasGroup.alpha = 1f;
        }

        view.text.color =
            sequence.isHealing ? healColor : damageColor;
        view.text.text = sequence.isHealing
            ? $"+{sequence.finalDamage}"
            : sequence.finalDamage.ToString();

        view.root.gameObject.SetActive(true);
    }

    /// <summary>
    /// 隱藏指定的結果飛行視圖。
    /// </summary>
    private void HideDamageResultFly()
    {
        DamageResultFlyView view = GetOrCreateResultFlyView(0);
        if (view != null && view.root != null)
        {
            view.root.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 隱藏所有結果飛行視圖。
    /// </summary>
    private void HideDamageResultFlies()
    {
        if (damageResultFlyPool.Count == 0 && damageResultFly != null)
        {
            damageResultFly.gameObject.SetActive(false);
            return;
        }

        for (int i = 0; i < damageResultFlyPool.Count; i++)
        {
            if (
                damageResultFlyPool[i] != null &&
                damageResultFlyPool[i].root != null
            )
            {
                damageResultFlyPool[i].root.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 將單一結果視圖移向對應玩家的血條目標。
    /// </summary>
    private IEnumerator FlyToHealthTarget(
        DamageCalculationSequence sequence
    )
    {
        DamageResultFlyView view = GetOrCreateResultFlyView(0);
        if (view == null || view.root == null)
        {
            yield break;
        }

        Vector3 start = view.root.position;
        Vector3 end = GetHealthTargetPosition(sequence.damagedWhitePlayer);
        CanvasGroup group = view.canvasGroup;
        float elapsed = 0f;

        while (elapsed < flyDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            float eased = Mathf.Pow(t, Mathf.Max(1f, flightAcceleration));

            view.root.position = Vector3.Lerp(start, end, eased);
            if (group != null)
            {
                group.alpha = 1f - Mathf.Clamp01((t - 0.75f) / 0.25f);
            }

            yield return null;
        }
    }

    /// <summary>
    /// 將批次結果視圖移向各自的血條目標。
    /// </summary>
    private IEnumerator FlyBatchToHealthTargets(
        List<DamageCalculationSequence> sequences,
        List<DamageResultFlyView> views
    )
    {
        List<Vector3> starts = new List<Vector3>();
        List<Vector3> ends = new List<Vector3>();
        for (int i = 0; i < views.Count; i++)
        {
            starts.Add(views[i].root.position);
            ends.Add(GetHealthTargetPosition(sequences[i].damagedWhitePlayer));
        }

        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            float eased = Mathf.Pow(t, Mathf.Max(1f, flightAcceleration));
            float alpha = 1f - Mathf.Clamp01((t - 0.75f) / 0.25f);

            for (int i = 0; i < views.Count; i++)
            {
                views[i].root.position = Vector3.Lerp(
                    starts[i],
                    ends[i],
                    eased
                );

                if (views[i].canvasGroup != null)
                {
                    views[i].canvasGroup.alpha = alpha;
                }
            }

            yield return null;
        }
    }

    /// <summary>
    /// 取得可重用的結果飛行視圖，必要時建立新物件。
    /// </summary>
    private DamageResultFlyView GetOrCreateResultFlyView(int index)
    {
        while (damageResultFlyPool.Count <= index)
        {
            RectTransform viewRoot;
            if (damageResultFlyPool.Count == 0)
            {
                viewRoot = damageResultFly;
            }
            else
            {
                viewRoot = Instantiate(damageResultFly, root, false);
                viewRoot.name = "DamageResultFly";
            }

            if (viewRoot == null)
            {
                return null;
            }

            CanvasGroup group = viewRoot.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = viewRoot.gameObject.AddComponent<CanvasGroup>();
            }

            TMP_Text text = damageResultFlyPool.Count == 0
                ? damageResultFlyText
                : FindChildComponent<TMP_Text>(viewRoot, "DamageText");

            damageResultFlyPool.Add(new DamageResultFlyView
            {
                root = viewRoot,
                canvasGroup = group,
                text = text
            });
        }

        return damageResultFlyPool[index];
    }

    /// <summary>
    /// 將世界座標轉換為 Canvas 使用的位置。
    /// </summary>
    private Vector3 WorldToCanvasPosition(Vector3 worldPosition)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return worldPosition;
        }

        return camera.WorldToScreenPoint(worldPosition);
    }

    /// <summary>
    /// 將世界座標轉換為指定 UI 根物件的錨點位置。
    /// </summary>
    private Vector2 WorldToRootAnchoredPosition(Vector3 worldPosition)
    {
        Vector2 screenPosition = WorldToCanvasPosition(worldPosition);
        RectTransform referenceRoot = root != null
            ? root
            : transform as RectTransform;

        if (referenceRoot == null)
        {
            return screenPosition;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            referenceRoot,
            screenPosition,
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null,
            out Vector2 localPoint
        );

        return localPoint;
    }

    /// <summary>
    /// 取得指定玩家血條在目前顯示空間中的目標位置。
    /// </summary>
    private Vector3 GetHealthTargetPosition(bool isWhitePlayer)
    {
        Transform target = isWhitePlayer ? whiteHealthTarget : blackHealthTarget;
        if (target != null)
        {
            return target.position;
        }

        return isWhitePlayer
            ? new Vector3(Screen.width * 0.18f, Screen.height * 0.88f, 0f)
            : new Vector3(Screen.width * 0.82f, Screen.height * 0.88f, 0f);
    }

    /// <summary>
    /// 檢查演出所需的 UI 與動畫引用是否齊全。
    /// </summary>
    private bool HasRequiredReferences()
    {
        return
            canvas != null &&
            root != null &&
            whiteHealthTarget != null &&
            blackHealthTarget != null &&
            imageDatabase != null &&
            damageCalcStepPrefab != null &&
            damageResultFly != null &&
            damageResultFlyText != null;
    }

    /// <summary>
    /// 依計算步驟資料選取棋子或數值圖示。
    /// </summary>
    private Sprite ResolveIcon(
        DamageCalculationStep step,
        DamageCalculationSequence sequence
    )
    {
        if (
            step.usePieceIcon &&
            imageDatabase != null &&
            step.iconPiece != null
        )
        {
            Sprite pieceSprite = imageDatabase.GetPieceSprite(step.iconPiece);
            if (pieceSprite != null)
            {
                return pieceSprite;
            }
        }

        if (
            step.useTargetPieceIcon &&
            imageDatabase != null &&
            sequence != null
        )
        {
            Sprite pieceSprite = imageDatabase.GetPieceSprite(sequence.target);
            if (pieceSprite != null)
            {
                return pieceSprite;
            }
        }

        return step.icon;
    }

    /// <summary>
    /// 依計算項目取得對應的數值圖示。
    /// </summary>
    private Sprite GetCountingIcon(DamageCountingIcon icon)
    {
        switch (icon)
        {
            case DamageCountingIcon.Attack: return attackIcon;
            case DamageCountingIcon.Defense: return defenseIcon;
            case DamageCountingIcon.Heal: return healIcon;
            case DamageCountingIcon.AntiAttack: return antiAttackIcon;
            case DamageCountingIcon.AntiHeal: return antiHealIcon;
            case DamageCountingIcon.Fire: return fireIcon;
            case DamageCountingIcon.Poison: return poisonIcon;
            case DamageCountingIcon.Curse: return curseIcon;
            case DamageCountingIcon.Cost: return costIcon;
            default: return null;
        }
    }

    /// <summary>
    /// 補齊傷害計算使用的數值分類圖示。
    /// </summary>
    private void AutoBindCountingIcons()
    {
#if UNITY_EDITOR
        if (
            attackIcon != null &&
            defenseIcon != null &&
            healIcon != null &&
            antiAttackIcon != null &&
            antiHealIcon != null &&
            fireIcon != null &&
            poisonIcon != null &&
            curseIcon != null &&
            costIcon != null
        )
        {
            return;
        }

        const string path = "Assets/ChessCardImages/CountingIcon.png";
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is not Sprite sprite)
            {
                continue;
            }

            switch (sprite.name)
            {
                case "ATK":
                    if (attackIcon == null) attackIcon = sprite;
                    break;
                case "DEF":
                    if (defenseIcon == null) defenseIcon = sprite;
                    break;
                case "Heal":
                    if (healIcon == null) healIcon = sprite;
                    break;
                case "AntiATK":
                    if (antiAttackIcon == null) antiAttackIcon = sprite;
                    break;
                case "AntiHeal":
                    if (antiHealIcon == null) antiHealIcon = sprite;
                    break;
                case "Fire":
                    if (fireIcon == null) fireIcon = sprite;
                    break;
                case "Poison":
                    if (poisonIcon == null) poisonIcon = sprite;
                    break;
                case "Curse":
                    if (curseIcon == null) curseIcon = sprite;
                    break;
                case "Cost":
                    if (costIcon == null) costIcon = sprite;
                    break;
            }
        }
#endif
    }

    /// <summary>
    /// 依計算步驟產生最終顯示文字。
    /// </summary>
    private string ResolveDisplayText(DamageCalculationStep step)
    {
        if (!string.IsNullOrEmpty(step.displayText))
        {
            return step.displayText;
        }

        if (string.IsNullOrEmpty(step.detail))
        {
            return step.title;
        }

        if (string.IsNullOrEmpty(step.title))
        {
            return step.detail;
        }

        return $"{step.title}\n{step.detail}";
    }

    /// <summary>
    /// 依序呼叫清單中的完成回呼。
    /// </summary>
    private void InvokeAll(IReadOnlyList<Action> actions)
    {
        if (actions == null)
        {
            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            actions[i]?.Invoke();
        }
    }

    /// <summary>
    /// 依名稱查找 UI 階層中的目標物件。
    /// </summary>
    private Transform FindNamedTransform(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    /// <summary>
    /// 依名稱尋找子物件，再取得所需類型的元件。
    /// </summary>
    private T FindChildComponent<T>(
        Transform searchRoot,
        string childName
    ) where T : Component
    {
        Transform child = FindChildRecursive(searchRoot, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    /// <summary>
    /// 依階層順序遞迴尋找指定名稱的 Transform；回傳第一個符合的物件。
    /// </summary>
    private Transform FindChildRecursive(
        Transform searchRoot,
        string childName
    )
    {
        if (searchRoot == null)
        {
            return null;
        }

        if (searchRoot.name == childName)
        {
            return searchRoot;
        }

        foreach (Transform child in searchRoot)
        {
            Transform match = FindChildRecursive(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// 隱藏場景中作為演出樣板的預建物件。
    /// </summary>
    private void HidePrebuiltObjects()
    {
        HideDamageCalcSteps();

        HideDamageResultFlies();
    }

    private class DamageResultFlyView
    {
        public RectTransform root;
        public CanvasGroup canvasGroup;
        public TMP_Text text;
    }

    private class DamageStepLayout
    {
        private readonly Vector2 anchorMin;
        private readonly Vector2 anchorMax;
        private readonly Vector2 pivot;
        private readonly Vector2 sizeDelta;
        private readonly Vector3 localScale;

        /// <summary>
        /// 記錄 RectTransform 的版面配置，供演出後還原。
        /// </summary>
        public DamageStepLayout(RectTransform rectTransform)
        {
            anchorMin = rectTransform.anchorMin;
            anchorMax = rectTransform.anchorMax;
            pivot = rectTransform.pivot;
            sizeDelta = rectTransform.sizeDelta;
            localScale = rectTransform.localScale;
        }

        /// <summary>
        /// 將保存的版面配置套用到指定 RectTransform。
        /// </summary>
        public void ApplyTo(RectTransform rectTransform)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.localScale = localScale;
        }
    }
}
