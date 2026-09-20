using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OperateLogUI : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Button operateLogButton;
    [SerializeField] private GameObject logListRoot;
    [SerializeField] private GameObject logTextTemplate;
    [SerializeField] private ScrollRect logScrollRect;
    [SerializeField] private RectTransform logContent;

    [Header("Text Colors")]
    [SerializeField] private Color whiteTextColor = Color.white;
    [SerializeField] private Color blackTextColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Animation")]
    [SerializeField, Min(0f)] private float fallbackToggleSeconds = 0.35f;
    [SerializeField] private bool autoScrollToLatest = true;
    [SerializeField] private Animator logListAnimator;
    [SerializeField] private Animation logListAnimation;
    [SerializeField] private string animatorStateName = "";

    private static OperateLogUI instance;

    private int turnIndex;
    private int lineSerial;
    private bool isExpanded;
    private float normalizedTime;
    private Coroutine animationRoutine;
    private LogEntry lastEffectEntry;
    private string lastEffectKey;
    private readonly List<RaycastResult> uiRaycastResults =
        new List<RaycastResult>();
    private readonly Dictionary<string, bool> cardActorSides =
        new Dictionary<string, bool>();

    /// <summary>
    /// 登錄操作紀錄實例、綁定按鈕並初始化面板動畫。
    /// </summary>
    private void Awake()
    {
        instance = this;
        AutoBindReferences();
        BindButton();
        HideTemplate();
        InitializeToggleAnimation();
    }

    /// <summary>
    /// 編輯器重設元件時補齊操作紀錄介面引用。
    /// </summary>
    private void Reset()
    {
        AutoBindReferences();
    }

    /// <summary>
    /// Inspector 變更時補齊操作紀錄介面引用。
    /// </summary>
    private void OnValidate()
    {
        AutoBindReferences();
    }

    /// <summary>
    /// 清空對局操作紀錄並建立起始回合資料。
    /// </summary>
    public static void ResetLog(bool isWhiteTurn)
    {
        OperateLogUI ui = ResolveInstance();
        if (ui == null) return;

        ui.ClearEntries();
        ui.BeginTurnEntry(isWhiteTurn);
    }

    /// <summary>
    /// 為新回合建立操作紀錄區段。
    /// </summary>
    public static void BeginTurn(bool isWhiteTurn)
    {
        ResolveInstance()?.BeginTurnEntry(isWhiteTurn);
    }

    /// <summary>
    /// 記錄棋子的移動資訊。
    /// </summary>
    public static void LogMove(Piece piece, Vector2 from, Vector2 to)
    {
        OperateLogUI ui = ResolveInstance();
        if (ui == null) return;

        bool isWhiteActor = piece == null || piece.IsWhite;
        string text = $"[{ui.turnIndex}]{FormatCell(from)}->{FormatCell(to)}";
        ui.CreateEntry(text, isWhiteActor);
        ui.ClearEffectLink();
    }

    /// <summary>
    /// 記錄卡牌使用者、卡牌與目標資訊。
    /// </summary>
    public static void LogCard(
        bool isWhitePlayer,
        CardDefinition card,
        string targetText
    )
    {
        OperateLogUI ui = ResolveInstance();
        if (ui == null) return;

        string cardName = card != null ? card.cardName : "卡片";
        string target = ExtractFirstCell(targetText);
        string text = string.IsNullOrEmpty(target)
            ? $"[{ui.turnIndex}]{cardName}"
            : $"[{ui.turnIndex}]{cardName}{target}";

        string key = ui.CreateActionKey(card);
        LogEntry entry = ui.CreateEntry(text, isWhitePlayer);
        ui.RecordCardActor(card, isWhitePlayer);
        ui.SetEffectLink(key, entry);
    }

    /// <summary>
    /// 將傷害結果加入對應操作紀錄。
    /// </summary>
    public static void LogDamage(
        bool damagedWhitePlayer,
        int amount,
        DamageContext context = null
    )
    {
        if (amount <= 0) return;

        OperateLogUI ui = ResolveInstance();
        if (ui == null) return;

        string key = GetDamageKey(context);
        LogEntry entry = ui.ResolveEffectEntry(key, context);
        if (damagedWhitePlayer)
        {
            entry.whiteDamage += amount;
        }
        else
        {
            entry.blackDamage += amount;
        }

        entry.Refresh();
    }

    /// <summary>
    /// 將治療結果加入對應操作紀錄。
    /// </summary>
    public static void LogHeal(bool healedWhitePlayer, int amount)
    {
        if (amount <= 0) return;

        OperateLogUI ui = ResolveInstance();
        if (ui == null) return;

        LogEntry entry = ui.ResolveEffectEntry(string.Empty, null);
        if (healedWhitePlayer)
        {
            entry.whiteHeal += amount;
        }
        else
        {
            entry.blackHeal += amount;
        }

        entry.Refresh();
    }

    /// <summary>
    /// 取得目前場景中的共用 UI 實例。
    /// </summary>
    private static OperateLogUI ResolveInstance()
    {
        if (instance != null) return instance;

        instance = FindFirstObjectByType<OperateLogUI>();
        if (instance != null)
        {
            instance.AutoBindReferences();
            instance.BindButton();
            return instance;
        }

        GameObject cardGameUi = GameObject.Find("CardGameUI");
        if (cardGameUi == null) return null;

        instance = cardGameUi.GetComponent<OperateLogUI>();
        if (instance == null) return null;

        instance.AutoBindReferences();
        instance.BindButton();
        instance.HideTemplate();
        instance.InitializeToggleAnimation();
        return instance;
    }

    /// <summary>
    /// 依既有命名與階層規則補齊 UI 或動畫引用。
    /// </summary>
    private void AutoBindReferences()
    {
        Transform root = transform;
        GameObject cardGameUi = GameObject.Find("CardGameUI");
        if (cardGameUi != null)
        {
            root = cardGameUi.transform;
        }

        if (operateLogButton == null)
        {
            Transform buttonRoot = FindChildRecursive(root, "OperateLog");
            if (buttonRoot != null)
            {
                operateLogButton = buttonRoot.GetComponent<Button>();
            }
        }

        if (logListRoot == null)
        {
            Transform listRoot = FindChildRecursive(root, "LogList");
            if (listRoot != null)
            {
                logListRoot = listRoot.gameObject;
            }
        }

        if (logListRoot != null)
        {
            if (logScrollRect == null)
            {
                logScrollRect = logListRoot.GetComponentInChildren<ScrollRect>(true);
                if (logScrollRect == null)
                {
                    logScrollRect = logListRoot.GetComponentInParent<ScrollRect>();
                }
            }

            if (logListAnimator == null)
            {
                logListAnimator = logListRoot.GetComponent<Animator>();
            }

            if (logListAnimation == null)
            {
                logListAnimation = logListRoot.GetComponent<Animation>();
            }
        }

        if (logTextTemplate == null)
        {
            Transform textRoot = FindChildRecursive(root, "LogText");
            if (textRoot != null)
            {
                logTextTemplate = textRoot.gameObject;
                if (logContent == null)
                {
                    logContent = logTextTemplate.transform.parent as RectTransform;
                }
            }
        }
    }

    /// <summary>
    /// 綁定紀錄面板的開關按鈕。
    /// </summary>
    private void BindButton()
    {
        if (operateLogButton == null) return;

        operateLogButton.onClick.RemoveListener(ToggleLogList);
        operateLogButton.onClick.AddListener(ToggleLogList);
    }

    /// <summary>
    /// 隱藏用來建立紀錄項目的樣板。
    /// </summary>
    private void HideTemplate()
    {
        if (logTextTemplate != null)
        {
            logTextTemplate.SetActive(false);
        }
    }

    /// <summary>
    /// 移除既有紀錄項目並清空索引。
    /// </summary>
    private void ClearEntries()
    {
        turnIndex = 0;
        lineSerial = 0;
        ClearEffectLink();
        cardActorSides.Clear();

        if (logTextTemplate == null || logTextTemplate.transform.parent == null)
        {
            return;
        }

        Transform parent =
            logContent != null ? logContent : logTextTemplate.transform.parent;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.gameObject == logTextTemplate) continue;

            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// 建立目前回合的紀錄項目。
    /// </summary>
    private void BeginTurnEntry(bool isWhiteTurn)
    {
        turnIndex++;
        ClearEffectLink();
    }

    /// <summary>
    /// 建立並登錄一筆操作紀錄視圖。
    /// </summary>
    private LogEntry CreateEntry(string prefix, bool isWhiteActor)
    {
        if (logTextTemplate == null)
        {
            return new LogEntry(null, null, prefix);
        }

        GameObject entryObject = Instantiate(
            logTextTemplate,
            logContent != null ? logContent : logTextTemplate.transform.parent,
            false
        );
        entryObject.name = $"LogText_{lineSerial + 1:000}";
        entryObject.SetActive(true);
        lineSerial++;

        TMP_Text tmpText = entryObject.GetComponentInChildren<TMP_Text>(true);
        Text legacyText = entryObject.GetComponentInChildren<Text>(true);
        Color color = isWhiteActor ? whiteTextColor : blackTextColor;
        if (tmpText != null)
        {
            tmpText.color = color;
        }
        if (legacyText != null)
        {
            legacyText.color = color;
        }

        LogEntry entry = new LogEntry(tmpText, legacyText, prefix);
        entry.Refresh();
        ScrollToLatest();
        return entry;
    }

    /// <summary>
    /// 將紀錄捲動位置移至最新項目。
    /// </summary>
    private void ScrollToLatest()
    {
        if (!autoScrollToLatest || logScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        logScrollRect.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// 尋找目前效果應附加到的操作紀錄項目。
    /// </summary>
    private LogEntry ResolveEffectEntry(string key, DamageContext context)
    {
        if (
            lastEffectEntry != null &&
            (
                string.IsNullOrEmpty(key) ||
                string.IsNullOrEmpty(lastEffectKey) ||
                lastEffectKey == key ||
                lastEffectKey.StartsWith(key + "#")
            )
        )
        {
            return lastEffectEntry;
        }

        bool actorIsWhite = ResolveActorIsWhite(context);
        string cardKey = GetDamageKey(context);
        if (
            !string.IsNullOrEmpty(cardKey) &&
            cardActorSides.TryGetValue(cardKey, out bool recordedSide)
        )
        {
            actorIsWhite = recordedSide;
        }

        string source = ResolveDamageSourceName(context);
        string prefix = string.IsNullOrEmpty(source)
            ? $"[{turnIndex}]"
            : $"[{turnIndex}]{source}";

        LogEntry entry = CreateEntry(prefix, actorIsWhite);
        SetEffectLink(key, entry);
        return entry;
    }

    /// <summary>
    /// 建立效果與來源操作紀錄的關聯。
    /// </summary>
    private void SetEffectLink(string key, LogEntry entry)
    {
        lastEffectKey = key;
        lastEffectEntry = entry;
    }

    /// <summary>
    /// 清除效果與來源操作紀錄的關聯。
    /// </summary>
    private void ClearEffectLink()
    {
        lastEffectKey = string.Empty;
        lastEffectEntry = null;
    }

    /// <summary>
    /// 依操作識別資料建立紀錄索引鍵。
    /// </summary>
    private string CreateActionKey(CardDefinition card)
    {
        string baseKey = GetCardKey(card);
        if (string.IsNullOrEmpty(baseKey)) return string.Empty;

        lineSerial++;
        return $"{baseKey}#{lineSerial}";
    }

    /// <summary>
    /// 記錄卡牌操作所屬的玩家陣營。
    /// </summary>
    private void RecordCardActor(CardDefinition card, bool isWhitePlayer)
    {
        string key = GetCardKey(card);
        if (string.IsNullOrEmpty(key)) return;

        cardActorSides[key] = isWhitePlayer;
    }

    /// <summary>
    /// 切換操作紀錄面板的展開狀態。
    /// </summary>
    private void ToggleLogList()
    {
        if (logListRoot == null) return;
        if (PointerHitLogList()) return;

        SetExpanded(!isExpanded);
    }

    /// <summary>
    /// 判斷游標是否命中操作紀錄面板。
    /// </summary>
    private bool PointerHitLogList()
    {
        if (
            EventSystem.current == null ||
            logListRoot == null ||
            operateLogButton == null
        )
        {
            return false;
        }

        PointerEventData pointerData =
            new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

        Transform listTransform = logListRoot.transform;
        Transform buttonTransform = operateLogButton.transform;
        foreach (RaycastResult result in uiRaycastResults)
        {
            if (result.gameObject == null) continue;

            Transform hit = result.gameObject.transform;
            if (hit == listTransform || hit.IsChildOf(listTransform))
            {
                return true;
            }

            if (hit == buttonTransform || hit.IsChildOf(buttonTransform))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// 設定面板展開狀態並啟動對應動畫。
    /// </summary>
    private void SetExpanded(bool expanded)
    {
        if (isExpanded == expanded && animationRoutine == null) return;

        isExpanded = expanded;
        logListRoot.SetActive(true);

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(AnimateToEndpoint());
    }

    /// <summary>
    /// 初始化紀錄面板的展開收合動畫。
    /// </summary>
    private void InitializeToggleAnimation()
    {
        AutoBindReferences();
        normalizedTime = 0f;

        if (logListRoot != null)
        {
            logListRoot.SetActive(true);
        }

        EvaluateToggleAnimation();
    }

    /// <summary>
    /// 將展開或收合動畫推進到指定終點。
    /// </summary>
    private IEnumerator AnimateToEndpoint()
    {
        float targetTime = isExpanded ? 1f : 0f;
        float duration = GetToggleDuration();

        while (!Mathf.Approximately(normalizedTime, targetTime))
        {
            normalizedTime = Mathf.MoveTowards(
                normalizedTime,
                targetTime,
                Time.unscaledDeltaTime / duration
            );

            EvaluateToggleAnimation();
            yield return null;
        }

        normalizedTime = targetTime;
        EvaluateToggleAnimation();
        animationRoutine = null;
    }

    /// <summary>
    /// 取得紀錄面板切換動畫的播放時間。
    /// </summary>
    private float GetToggleDuration()
    {
        if (logListAnimation != null && logListAnimation.clip != null)
        {
            return Mathf.Max(0.01f, logListAnimation.clip.length);
        }

        return Mathf.Max(0.01f, fallbackToggleSeconds);
    }

    /// <summary>
    /// 依目前進度更新紀錄面板的切換動畫。
    /// </summary>
    private void EvaluateToggleAnimation()
    {
        if (logListAnimation != null && logListAnimation.clip != null)
        {
            AnimationClip clip = logListAnimation.clip;
            AnimationState state = logListAnimation[clip.name];
            if (state == null) return;

            logListAnimation.Stop();
            state.enabled = true;
            state.weight = 1f;
            state.speed = 0f;
            state.time = Mathf.Clamp01(normalizedTime) * clip.length;
            logListAnimation.Sample();
            state.enabled = false;
            return;
        }

        if (logListAnimator == null || logListAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        logListAnimator.speed = 0f;
        if (string.IsNullOrEmpty(animatorStateName))
        {
            AnimatorStateInfo stateInfo =
                logListAnimator.GetCurrentAnimatorStateInfo(0);
            logListAnimator.Play(stateInfo.fullPathHash, 0, normalizedTime);
        }
        else
        {
            logListAnimator.Play(animatorStateName, 0, normalizedTime);
        }

        logListAnimator.Update(0f);
    }

    /// <summary>
    /// 建立卡牌操作的紀錄索引鍵。
    /// </summary>
    private static string GetCardKey(CardDefinition card)
    {
        return card != null && !string.IsNullOrEmpty(card.id)
            ? $"card:{card.id}"
            : string.Empty;
    }

    /// <summary>
    /// 建立傷害效果的紀錄索引鍵。
    /// </summary>
    private static string GetDamageKey(DamageContext context)
    {
        if (context != null && context.sourceCard != null)
        {
            return GetCardKey(context.sourceCard);
        }

        if (
            context != null &&
            context.sourceStatus != null &&
            context.sourceStatus.definition != null &&
            !string.IsNullOrEmpty(context.sourceStatus.definition.sourceCardId)
        )
        {
            return $"status:{context.sourceStatus.definition.sourceCardId}";
        }

        return string.Empty;
    }

    /// <summary>
    /// 依效果來源判斷操作所屬的白黑陣營。
    /// </summary>
    private static bool ResolveActorIsWhite(DamageContext context)
    {
        if (
            context != null &&
            context.sourceStatus != null &&
            context.sourceStatus.hasSourcePlayer
        )
        {
            return context.sourceStatus.sourcePlayerIsWhite;
        }

        if (context != null && context.source != null)
        {
            return context.source.IsWhite;
        }

        return true;
    }

    /// <summary>
    /// 取得傷害來源的顯示名稱。
    /// </summary>
    private static string ResolveDamageSourceName(DamageContext context)
    {
        if (context == null) return string.Empty;

        if (context.sourceCard != null)
        {
            return context.sourceCard.cardName;
        }

        if (
            context.sourceStatus != null &&
            context.sourceStatus.definition != null &&
            !string.IsNullOrEmpty(context.sourceStatus.definition.statusName)
        )
        {
            return context.sourceStatus.definition.statusName;
        }

        if (context.source != null)
        {
            return context.source.name;
        }

        return string.Empty;
    }

    /// <summary>
    /// 從操作描述中擷取第一個棋盤座標文字。
    /// </summary>
    private static string ExtractFirstCell(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        Match match = Regex.Match(
            text,
            @"\((-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\)"
        );
        if (!match.Success)
        {
            match = Regex.Match(
                text,
                @"\((-?\d+(?:\.\d+)?)\.(-?\d+(?:\.\d+)?)\)"
            );
        }

        if (!match.Success) return string.Empty;

        int x = Mathf.RoundToInt(
            float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
        );
        int y = Mathf.RoundToInt(
            float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
        );
        return $"({x}.{y})";
    }

    /// <summary>
    /// 將棋盤座標格式化為紀錄顯示文字。
    /// </summary>
    private static string FormatCell(Vector2 coordinate)
    {
        return $"({Mathf.RoundToInt(coordinate.x)}.{Mathf.RoundToInt(coordinate.y)})";
    }

    /// <summary>
    /// 依階層順序遞迴尋找指定名稱的 Transform；回傳第一個符合的物件。
    /// </summary>
    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null) return null;

        foreach (Transform child in root)
        {
            if (child.name == childName) return child;

            Transform result = FindChildRecursive(child, childName);
            if (result != null) return result;
        }

        return null;
    }

    private class LogEntry
    {
        private readonly TMP_Text tmpText;
        private readonly Text legacyText;
        private readonly string prefix;
        public int whiteDamage;
        public int blackDamage;
        public int whiteHeal;
        public int blackHeal;

        /// <summary>
        /// 保存紀錄文字元件與前綴，供後續更新顯示。
        /// </summary>
        public LogEntry(TMP_Text tmpText, Text legacyText, string prefix)
        {
            this.tmpText = tmpText;
            this.legacyText = legacyText;
            this.prefix = prefix;
        }

        /// <summary>
        /// 依目前資料刷新此物件的顯示內容。
        /// </summary>
        public void Refresh()
        {
            string value = Build();
            if (tmpText != null)
            {
                tmpText.text = value;
            }
            if (legacyText != null)
            {
                legacyText.text = value;
            }
        }

        /// <summary>
        /// 依紀錄項目資料組合顯示文字。
        /// </summary>
        private string Build()
        {
            StringBuilder builder = new StringBuilder(prefix);
            AppendResult(builder, "\u9ed1", -blackDamage);
            AppendResult(builder, "\u767d", -whiteDamage);
            AppendResult(builder, "\u9ed1", blackHeal);
            AppendResult(builder, "\u767d", whiteHeal);
            return builder.ToString();
        }

        /// <summary>
        /// 將傷害或治療結果附加到既有紀錄文字。
        /// </summary>
        private static void AppendResult(
            StringBuilder builder,
            string side,
            int value
        )
        {
            if (value == 0) return;

            builder.Append(' ');
            builder.Append(side);
            builder.Append(value > 0 ? "+" : "");
            builder.Append(value);
        }
    }
}
