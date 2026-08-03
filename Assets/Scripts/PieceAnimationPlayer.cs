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

    private void LateUpdate()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition =
            Vector3.Lerp(moveStartLocalOffset, Vector3.zero, moveBlend);
    }

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

    public void PlayCapture()
    {
        PlayTrigger(captureTrigger);
    }

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

    private void PlayTrigger(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }
}
