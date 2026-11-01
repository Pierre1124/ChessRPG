using UnityEngine;

/// <summary>僅動畫透明度及位置，保留 Alarm 樣板的大小、錨點和 pivot。</summary>
public sealed class AlarmPresentation : MonoBehaviour
{
    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 moveStart, destination;
    private float fadeSeconds, moveSeconds, moveElapsed;
    private bool leaving;
    public bool IsHidden => group == null || group.alpha <= 0f;

    public void Initialize(float fadeDuration, float moveDuration)
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        fadeSeconds = Mathf.Max(.01f, fadeDuration);
        moveSeconds = Mathf.Max(.01f, moveDuration);
        moveStart = destination = rect.anchoredPosition;
    }

    public void MoveTo(Vector2 target)
    {
        if (target == destination) return;
        moveStart = rect.anchoredPosition;
        destination = target;
        moveElapsed = 0f;
    }

    public void FadeOut() { leaving = true; }

    private void Update() { Advance(Time.unscaledDeltaTime); }

    internal void Advance(float deltaTime)
    {
        if (group == null) return;
        group.alpha = Mathf.MoveTowards(group.alpha, leaving ? 0f : 1f, deltaTime / fadeSeconds);
        moveElapsed += deltaTime;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(moveElapsed / moveSeconds));
        rect.anchoredPosition = Vector2.LerpUnclamped(moveStart, destination, t);
    }
}
