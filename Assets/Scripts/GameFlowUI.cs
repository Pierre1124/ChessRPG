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

    private void Awake()
    {
        instance = this;
        AutoBindReferences();
        HideTemplateObjects();
    }

    private void Reset()
    {
        AutoBindReferences();
    }

    private void OnValidate()
    {
        AutoBindReferences();
    }

    public static void Show(string message)
    {
        ResolveInstance()?.ShowAlarm(message);
    }

    public static void SetPersistent(string key, string message, bool visible)
    {
        ResolveInstance()?.SetPersistentAlarm(key, message, visible);
    }

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

    private float GetCurrentAnimatorStateDuration(Animator animator)
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.length > 0f)
        {
            return stateInfo.length;
        }

        return fallbackPhaseSeconds;
    }

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
