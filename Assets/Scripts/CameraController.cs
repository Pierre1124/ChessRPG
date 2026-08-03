using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public Transform boardTransform;

    [Header("Default View")]
    public float height = 6f;
    public float angle = 50f;
    public float xPosition = 3.5f;
    public float yPosition = -2f;

    [Header("Rotation")]
    public float rotationSpeed = 5f;
    public float minPitch = 20f;
    public float maxPitch = 80f;

    private float currentYaw;
    private float currentPitch;

    private bool isDragging = false;

    void Start()
    {
        StartCoroutine(ApplyDefaultPerspective());
    }

    private IEnumerator ApplyDefaultPerspective()
    {
        yield return null;

        MultiplayerGameController multiplayer =
            FindFirstObjectByType<MultiplayerGameController>();
        if (
            multiplayer != null &&
            multiplayer.IsOnline &&
            multiplayer.LocalSide == PlayerSide.Black
        )
        {
            BlackPerspective();
            yield break;
        }

        WhitePerspective();
    }

    void Update()
    {
        HandleRotation();

        // 空白鍵回到本地玩家所屬視角
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ApplyLocalPlayerPerspective();
        }
    }

    //==============================
    // 右鍵旋轉
    //==============================
    void HandleRotation()
    {
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();

            currentYaw += delta.x * rotationSpeed * Time.deltaTime;

            currentPitch -= delta.y * rotationSpeed * Time.deltaTime;

            currentPitch = Mathf.Clamp(
                currentPitch,
                minPitch,
                maxPitch
            );

            UpdateRotation();
        }
    }

    //==============================
    // 更新旋轉
    //==============================
    void UpdateRotation()
    {
        Vector3 center = new Vector3(
            boardTransform.position.x + 3.5f,
            boardTransform.position.y,
            boardTransform.position.z + 3.5f
        );

        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);

        Vector3 offset = rotation * new Vector3(0, 0, -10f);

        transform.position = center + offset;

        transform.LookAt(center);
    }

    //==============================
    // 黑方視角
    //==============================
    public void BlackPerspective()
    {
        currentYaw = 180f;
        currentPitch = angle;

        Vector3 boardCenter = new Vector3(
            boardTransform.position.x + 3.5f,
            boardTransform.position.y,
            boardTransform.position.z + 3.5f
        );

        transform.position = new Vector3(
            boardCenter.x,
            height,
            9f
        );

        transform.rotation = Quaternion.Euler(angle, 180f, 0f);
    }

    //==============================
    // 白方視角
    //==============================
    public void WhitePerspective()
    {
        currentYaw = 0f;
        currentPitch = angle;

        Vector3 boardCenter = new Vector3(
            boardTransform.position.x + xPosition,
            boardTransform.position.y,
            boardTransform.position.z + yPosition
        );

        transform.position = new Vector3(
            boardCenter.x,
            height,
            boardCenter.z
        );

        transform.rotation = Quaternion.Euler(angle, 0f, 0f);
    }

    //==============================
    // 回到本地玩家所屬視角
    //==============================
    public void ApplyLocalPlayerPerspective()
    {
        MultiplayerGameController multiplayer =
            FindFirstObjectByType<MultiplayerGameController>();
        if (
            multiplayer != null &&
            multiplayer.IsOnline &&
            multiplayer.LocalSide == PlayerSide.Black
        )
        {
            BlackPerspective();
            return;
        }

        WhitePerspective();
    }
}
