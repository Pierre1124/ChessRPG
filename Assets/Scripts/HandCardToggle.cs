using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class HandCardToggle : MonoBehaviour, IPointerClickHandler
{
    private const string StateName = "HandCardUp";
    private const float AnimationDuration = 0.5f;

    [SerializeField] private Animator animator;

    private bool isExpanded;
    private bool isInitialized;
    private float normalizedTime;
    private Coroutine animationRoutine;

    public bool IsExpanded
    {
        get { return isExpanded; }
    }

    /// <summary>
    /// 取得手牌 Animator 並初始化收合狀態。
    /// </summary>
    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        Initialize(animator);
    }

    /// <summary>
    /// 手牌展開時偵測外部點擊，必要時收合手牌。
    /// </summary>
    private void Update()
    {
        if (
            !isExpanded ||
            Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame
        )
        {
            return;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        if (!IsPointerInsideHandCard(pointerPosition))
        {
            Collapse();
        }
    }

    /// <summary>
    /// 設定手牌 Animator 並套用初始動畫進度。
    /// </summary>
    public void Initialize(Animator targetAnimator)
    {
        if (isInitialized && animator == targetAnimator)
        {
            return;
        }

        animator = targetAnimator;
        normalizedTime = 0f;
        EvaluateAnimation();
        isInitialized = true;
    }

    /// <summary>
    /// 回應手牌區點擊並切換展開狀態。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        Toggle();
    }

    /// <summary>
    /// 將手牌區收合。
    /// </summary>
    public void Collapse()
    {
        SetExpanded(false);
    }

    /// <summary>
    /// 切換手牌區的展開與收合狀態。
    /// </summary>
    public void Toggle()
    {
        SetExpanded(!isExpanded);
    }

    /// <summary>
    /// 設定面板展開狀態並啟動對應動畫。
    /// </summary>
    private void SetExpanded(bool expanded)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (isExpanded == expanded && animationRoutine == null)
        {
            return;
        }

        isExpanded = expanded;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(AnimateToEndpoint());
    }

    /// <summary>
    /// 判斷游標是否位於手牌區內。
    /// </summary>
    private bool IsPointerInsideHandCard(Vector2 screenPosition)
    {
        RectTransform rectTransform = transform as RectTransform;
        if (
            rectTransform != null &&
            RectTransformUtility.RectangleContainsScreenPoint(
                rectTransform,
                screenPosition,
                null
            )
        )
        {
            return true;
        }

        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData eventData =
            new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            Transform hitTransform = results[i].gameObject.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 將展開或收合動畫推進到指定終點。
    /// </summary>
    private IEnumerator AnimateToEndpoint()
    {
        float targetTime = isExpanded ? 1f : 0f;

        while (!Mathf.Approximately(normalizedTime, targetTime))
        {
            normalizedTime = Mathf.MoveTowards(
                normalizedTime,
                targetTime,
                Time.unscaledDeltaTime / AnimationDuration
            );

            EvaluateAnimation();
            yield return null;
        }

        normalizedTime = targetTime;
        EvaluateAnimation();
        animationRoutine = null;
    }

    /// <summary>
    /// 依目前進度更新面板動畫姿態。
    /// </summary>
    private void EvaluateAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        animator.speed = 0f;
        animator.Play(StateName, 0, normalizedTime);
        animator.Update(0f);
    }
}
