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

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        Initialize(animator);
    }

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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        Toggle();
    }

    public void Collapse()
    {
        SetExpanded(false);
    }

    public void Toggle()
    {
        SetExpanded(!isExpanded);
    }

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
