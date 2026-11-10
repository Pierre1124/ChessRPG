using System.Collections;
using UnityEngine;

/// <summary>讓既有 UI 物件短暫放大後回復；可由回合文字與傷害文字共用。</summary>
[DisallowMultipleComponent]
public sealed class UiScalePulse : MonoBehaviour
{
    [SerializeField, Min(1f)] private float peakScale = 2f;
    [SerializeField, Min(0.05f)] private float duration = 0.75f;

    private Coroutine playing;
    private Vector3 baseScale;

    /// <summary>從物件目前比例開始播放；重複呼叫會先還原，避免比例逐次放大。</summary>
    public void Play()
    {
        if (!isActiveAndEnabled) return;

        if (playing != null)
        {
            StopCoroutine(playing);
            transform.localScale = baseScale;
        }

        baseScale = transform.localScale;
        playing = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float scale = 1f + (peakScale - 1f) * Mathf.Sin(progress * Mathf.PI);
            transform.localScale = new Vector3(
                baseScale.x * scale,
                baseScale.y * scale,
                baseScale.z
            );
            yield return null;
        }

        transform.localScale = baseScale;
        playing = null;
    }

    private void OnDisable()
    {
        if (playing == null) return;
        StopCoroutine(playing);
        transform.localScale = baseScale;
        playing = null;
    }
}
