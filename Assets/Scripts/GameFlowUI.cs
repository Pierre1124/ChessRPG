using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameFlowUI : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GameObject alarmTemplate;
    [SerializeField] private GameObject phaseChangeRoot;

    [Header("Alarm")]
    [SerializeField, Min(0.1f)] private float alarmLifetimeSeconds = 3f;
    [SerializeField] private Vector2 alarmStackOffset = new Vector2(0f, -36f);

    [Header("Phase Change")]
    [SerializeField, Min(0f)] private float fallbackPhaseSeconds = 1f;
    [SerializeField] private string animatorTriggerName = "Play";

    private static GameFlowUI instance;

    private readonly List<RectTransform> activeOneShotAlarms =
        new List<RectTransform>();
    private readonly Dictionary<string, GameObject> persistentAlarms =
        new Dictionary<string, GameObject>();

    /// <summary>
    /// 登錄共用提示實例，補齊引用並隱藏樣板。
    /// </summary>
    private void Awake()
    {
        instance = this;
        AutoBindReferences();
        HideTemplateObjects();
    }

    /// <summary>
    /// 編輯器重設元件時補齊提示介面引用。
    /// </summary>
    private void Reset()
    {
        AutoBindReferences();
    }

    /// <summary>
    /// Inspector 變更時補齊提示介面引用。
    /// </summary>
    private void OnValidate()
    {
        AutoBindReferences();
    }

    /// <summary>
    /// 透過目前場景的提示實例顯示一次性訊息。
    /// </summary>
    public static void Show(string message)
    {
        ResolveInstance()?.ShowAlarm(message);
    }

    /// <summary>
    /// 以識別鍵更新持續顯示的對局提示。
    /// </summary>
    public static void SetPersistent(string key, string message, bool visible)
    {
        ResolveInstance()?.SetPersistentAlarm(key, message, visible);
    }

    /// <summary>
    /// 啟動回合階段提示的演出。
    /// </summary>
    public static Coroutine PlayPhase(MonoBehaviour owner, string message, Action onComplete)
    {
        GameFlowUI ui = ResolveInstance();
        if (ui == null || owner == null)
        {
            onComplete?.Invoke();
            return null;
        }

        return owner.StartCoroutine(ui.PlayPhaseRoutine(message, onComplete));
    }

    /// <summary>
    /// 判斷回合階段提示是否已具備播放條件。
    /// </summary>
    public static bool IsPhaseReady()
    {
        GameFlowUI ui = ResolveInstance();
        return ui != null && ui.phaseChangeRoot != null;
    }

    /// <summary>
    /// 取得目前場景中的共用 UI 實例。
    /// </summary>
    private static GameFlowUI ResolveInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<GameFlowUI>();
        if (instance != null)
        {
            instance.AutoBindReferences();
            return instance;
        }

        GameObject cardGameUi = GameObject.Find("CardGameUI");
        if (cardGameUi == null)
        {
            return null;
        }

        instance = cardGameUi.GetComponent<GameFlowUI>();
        if (instance == null)
        {
            instance = cardGameUi.AddComponent<GameFlowUI>();
        }

        instance.AutoBindReferences();
        return instance;
    }

    /// <summary>
    /// 依既有命名與階層規則補齊 UI 或動畫引用。
    /// </summary>
    private void AutoBindReferences()
    {
        if (alarmTemplate == null)
        {
            Transform alarm = FindChildRecursive(transform, "Alarm");
            if (alarm == null)
            {
                GameObject cardGameUi = GameObject.Find("CardGameUI");
                if (cardGameUi != null)
                {
                    alarm = FindChildRecursive(cardGameUi.transform, "Alarm");
                }
            }

            if (alarm != null)
            {
                alarmTemplate = alarm.gameObject;
            }
        }

        if (phaseChangeRoot == null)
        {
            Transform phase = FindChildRecursive(transform, "PhaseChange");
            if (phase == null)
            {
                GameObject cardGameUi = GameObject.Find("CardGameUI");
                if (cardGameUi != null)
                {
                    phase = FindChildRecursive(cardGameUi.transform, "PhaseChange");
                }
            }

            if (phase != null)
            {
                phaseChangeRoot = phase.gameObject;
            }
        }
    }

    /// <summary>
    /// 隱藏用於複製提示內容的樣板物件。
    /// </summary>
    private void HideTemplateObjects()
    {
        if (alarmTemplate != null)
        {
            alarmTemplate.SetActive(false);
        }

        if (phaseChangeRoot != null)
        {
            phaseChangeRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 顯示操作或對局提示訊息。
    /// </summary>
    private void ShowAlarm(string message)
    {
        if (alarmTemplate == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        GameObject instanceObject = Instantiate(
            alarmTemplate,
            alarmTemplate.transform.parent,
            false
        );
        instanceObject.name = "Alarm";
        SetText(instanceObject, message);

        RectTransform rectTransform =
            instanceObject.transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition +=
                alarmStackOffset * activeOneShotAlarms.Count;
            activeOneShotAlarms.Add(rectTransform);
        }

        instanceObject.SetActive(true);
        StartCoroutine(DestroyAlarmAfterDelay(instanceObject, rectTransform));
    }

    /// <summary>
    /// 等待指定時間後移除一次性提示。
    /// </summary>
    private IEnumerator DestroyAlarmAfterDelay(
        GameObject alarm,
        RectTransform rectTransform
    )
    {
        yield return new WaitForSecondsRealtime(alarmLifetimeSeconds);

        if (rectTransform != null)
        {
            activeOneShotAlarms.Remove(rectTransform);
            ReflowOneShotAlarms();
        }

        if (alarm != null)
        {
            Destroy(alarm);
        }
    }

    /// <summary>
    /// 重新排列目前的一次性提示。
    /// </summary>
    private void ReflowOneShotAlarms()
    {
        for (int i = 0; i < activeOneShotAlarms.Count; i++)
        {
            RectTransform alarm = activeOneShotAlarms[i];
            if (alarm == null)
            {
                continue;
            }

            alarm.anchoredPosition =
                (alarmTemplate.transform as RectTransform).anchoredPosition +
                alarmStackOffset * i;
        }
    }

    /// <summary>
    /// 建立、更新或移除指定識別鍵的持續提示。
    /// </summary>
    private void SetPersistentAlarm(string key, string message, bool visible)
    {
        if (alarmTemplate == null || string.IsNullOrEmpty(key))
        {
            return;
        }

        if (!visible)
        {
            if (persistentAlarms.TryGetValue(key, out GameObject oldAlarm))
            {
                persistentAlarms.Remove(key);
                if (oldAlarm != null)
                {
                    Destroy(oldAlarm);
                }
            }

            return;
        }

        if (!persistentAlarms.TryGetValue(key, out GameObject alarm) || alarm == null)
        {
            alarm = Instantiate(alarmTemplate, alarmTemplate.transform.parent, false);
            alarm.name = $"Alarm_{key}";
            persistentAlarms[key] = alarm;
        }

        SetText(alarm, message);
        alarm.SetActive(true);
    }

    /// <summary>
    /// 播放階段提示並等待動畫結束。
    /// </summary>
    private IEnumerator PlayPhaseRoutine(string message, Action onComplete)
    {
        if (phaseChangeRoot == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        SetText(phaseChangeRoot, message);
        phaseChangeRoot.SetActive(true);

        float duration = PlayPhaseAnimation();
        if (duration > 0f)
        {
            yield return new WaitForSecondsRealtime(duration);
        }

        phaseChangeRoot.SetActive(false);
        onComplete?.Invoke();
    }

    /// <summary>
    /// 從指定狀態播放階段提示動畫。
    /// </summary>
    private float PlayPhaseAnimation()
    {
        Animation legacyAnimation = phaseChangeRoot.GetComponent<Animation>();
        if (legacyAnimation != null)
        {
            AnimationClip clip = legacyAnimation.clip;
            if (clip != null)
            {
                legacyAnimation.Stop();
                legacyAnimation.Play(clip.name);
                return clip.length;
            }
        }

        Animator animator = phaseChangeRoot.GetComponent<Animator>();
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (
                    parameter.type == AnimatorControllerParameterType.Trigger &&
                    parameter.name == animatorTriggerName
                )
                {
                    animator.SetTrigger(animatorTriggerName);
                    animator.Update(0f);
                    return GetCurrentAnimatorStateDuration(animator);
                }
            }

            AnimatorStateInfo currentState =
                animator.GetCurrentAnimatorStateInfo(0);
            animator.Play(currentState.fullPathHash, 0, 0f);
            animator.Update(0f);
            return GetCurrentAnimatorStateDuration(animator);
        }

        return fallbackPhaseSeconds;
    }

    /// <summary>
    /// 取得目前 Animator 狀態的播放時間。
    /// </summary>
    private float GetCurrentAnimatorStateDuration(Animator animator)
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.length > 0f)
        {
            return stateInfo.length;
        }

        return fallbackPhaseSeconds;
    }

    /// <summary>
    /// 尋找指定文字元件並更新其顯示內容。
    /// </summary>
    private static void SetText(GameObject root, string message)
    {
        TMP_Text tmpText = root.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = message;
            return;
        }

        Text legacyText = root.GetComponentInChildren<Text>(true);
        if (legacyText != null)
        {
            legacyText.text = message;
        }
    }

    /// <summary>
    /// 依階層順序遞迴尋找指定名稱的 Transform；回傳第一個符合的物件。
    /// </summary>
    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
