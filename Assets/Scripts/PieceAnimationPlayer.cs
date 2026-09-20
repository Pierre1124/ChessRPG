using UnityEngine;

public class PieceAnimationPlayer : MonoBehaviour
{
    [Header("Visual Root")]
    [SerializeField] private Transform visualRoot;

    [Header("Animator Triggers")]
    [SerializeField] private Animator animator;
    [SerializeField] private string moveTrigger = "Move";
    [SerializeField] private string captureTrigger = "Capture";
    [SerializeField] private string capturedTrigger = "Captured";

    [Header("Destroy After Captured")]
    [SerializeField, Min(0f)] private float capturedDestroyDelay = 0.25f;
    [SerializeField] private bool disableCollidersWhenCaptured = true;

    [SerializeField, Range(0f, 1f)] private float moveBlend = 1f;

    private Vector3 moveStartLocalOffset;

    /// <summary>
    /// 補齊棋子 Animator 與視覺根物件引用。
    /// </summary>
    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (visualRoot == null)
        {
            Transform child = transform.Find("VisualRoot");
            visualRoot = child != null ? child : transform;
        }
    }

    /// <summary>
    /// 在一般更新完成後維持棋子視覺物件的定位。
    /// </summary>
    private void LateUpdate()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition =
            Vector3.Lerp(moveStartLocalOffset, Vector3.zero, moveBlend);
    }

    /// <summary>
    /// 觸發棋子的移動動畫。
    /// </summary>
    public void PlayMove(Vector3 fromWorldPosition, Vector3 toWorldPosition)
    {
        if (visualRoot != null)
        {
            moveStartLocalOffset =
                transform.InverseTransformVector(
                    fromWorldPosition - toWorldPosition
                );
            moveBlend = 0f;
            visualRoot.localPosition = moveStartLocalOffset;
        }

        PlayTrigger(moveTrigger);
    }

    /// <summary>
    /// 觸發棋子的吃子動畫。
    /// </summary>
    public void PlayCapture()
    {
        PlayTrigger(captureTrigger);
    }

    /// <summary>
    /// 播放被吃動畫並依設定延後銷毀物件。
    /// </summary>
    public void PlayCapturedAndDestroy()
    {
        PlayTrigger(capturedTrigger);

        if (disableCollidersWhenCaptured)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider pieceCollider in colliders)
            {
                pieceCollider.enabled = false;
            }
        }

        Destroy(gameObject, capturedDestroyDelay);
    }

    /// <summary>
    /// 向 Animator 發送指定觸發參數。
    /// </summary>
    private void PlayTrigger(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }
}
