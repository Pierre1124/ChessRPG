using UnityEngine;

public class CardAnimationReceiver : MonoBehaviour, ICardAnimationReceiver
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform effectRoot;
    [SerializeField] private AudioSource audioSource;

    /// <summary>
    /// 補齊 Animator、特效掛點與 AudioSource 引用。
    /// </summary>
    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (effectRoot == null)
        {
            effectRoot = transform;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    /// <summary>
    /// 接收卡牌動畫情境並依事件時機與接收對象處理演出。
    /// </summary>
    public void PlayCardAnimation(CardAnimationContext context)
    {
        if (context == null || context.card == null)
        {
            return;
        }

        foreach (CardAnimationData animation in context.card.animations)
        {
            if (
                animation == null ||
                animation.timing != context.timing ||
                !MatchesRecipient(animation.recipient, context.recipient)
            )
            {
                continue;
            }

            Play(animation);
        }
    }

    /// <summary>
    /// 依動畫設定播放 Animator、特效 Prefab 與音效。
    /// </summary>
    private void Play(CardAnimationData animation)
    {
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(animation.animatorTrigger))
            {
                animator.SetTrigger(animation.animatorTrigger);
            }

            if (!string.IsNullOrEmpty(animation.animatorState))
            {
                animator.Play(animation.animatorState, 0, 0f);
            }
        }

        if (animation.effectPrefab != null)
        {
            Vector3 position =
                effectRoot.TransformPoint(animation.effectPositionOffset);

            Quaternion rotation =
                effectRoot.rotation *
                Quaternion.Euler(animation.effectRotationEuler);

            GameObject effectObject =
                Object.Instantiate(
                animation.effectPrefab,
                position,
                rotation,
                animation.attachEffectToReceiver ? effectRoot : null
            );

            effectObject.transform.localScale = animation.effectScale;

            if (animation.effectLifetime > 0f)
            {
                Object.Destroy(effectObject, animation.effectLifetime);
            }
        }

        if (animation.sound == null)
        {
            return;
        }

        if (audioSource != null)
        {
            audioSource.PlayOneShot(animation.sound, animation.soundVolume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(
                animation.sound,
                effectRoot.position,
                animation.soundVolume
            );
        }
    }

    /// <summary>
    /// 判斷動畫設定是否適用於目前的來源或目標接收器。
    /// </summary>
    private bool MatchesRecipient(
        CardAnimationRecipient expected,
        CardAnimationRecipient actual
    )
    {
        return
            expected == CardAnimationRecipient.Both ||
            expected == actual;
    }
}
