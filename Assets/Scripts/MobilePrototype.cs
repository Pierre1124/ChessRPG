using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Mobile-only prototype shell. Desktop scene layouts remain unchanged.</summary>
public sealed class MobilePrototype : MonoBehaviour
{
    public static bool Enabled
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool("ChessRPG.MobilePrototypePreview", false);
#else
            return Application.isMobilePlatform;
#endif
        }
    }
    private static MobilePrototype current;
    private RectTransform controls;
    private TMP_Text hint;
    private Button cancel;
    private InputManager input;
    private CameraController boardCamera;
    private SettingsUI settings;
    private float hintUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!Enabled) return;
        var root = new GameObject("Mobile prototype");
        DontDestroyOnLoad(root);
        current = root.AddComponent<MobilePrototype>();
        Application.targetFrameRate = 30;
        if (Application.isMobilePlatform)
        {
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
        SceneManager.sceneLoaded += current.OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { StartCoroutine(ConfigureScene()); }
    private IEnumerator ConfigureScene()
    {
        // Allow scene Start methods to finish configuring their existing UI.
        yield return null;
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace || canvas.transform.IsChildOf(transform)) continue;
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (canvas.GetComponent<MobileSafeArea>() == null) canvas.gameObject.AddComponent<MobileSafeArea>();
        }
        input = FindFirstObjectByType<InputManager>();
        boardCamera = FindFirstObjectByType<CameraController>();
        settings = FindFirstObjectByType<SettingsUI>();
        if (controls != null) Destroy(controls.root.gameObject);
        CreateControls(input != null);
        MobilePortraitLayout.Install();
        hintUntil = Time.unscaledTime + 14f;
        if (EventSystem.current != null)
            EventSystem.current.pixelDragThreshold = Mathf.RoundToInt(Mathf.Max(20f, Screen.dpi > 0 ? Screen.dpi * .12f : 32f));
    }

    private void CreateControls(bool inGame)
    {
        var root = new GameObject("Mobile controls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(root, SceneManager.GetActiveScene());
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        controls = MakeRect("Safe mobile controls", root.transform);
        controls.anchorMin = Vector2.zero; controls.anchorMax = Vector2.one;
        controls.offsetMin = controls.offsetMax = Vector2.zero;
        root.AddComponent<MobileSafeArea>();
        if (inGame)
        {
            cancel = MakeButton("取消出牌", 0, Cancel);
            MakeButton("重設視角", 1, () => { Cancel(); boardCamera?.ApplyLocalPlayerPerspective(); });
            MakeButton("操作說明", 2, () => hintUntil = Time.unscaledTime + 14f);
        }
        hint = new GameObject("Touch instructions", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        hint.transform.SetParent(controls, false);
        var rect = hint.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(0, -12); rect.sizeDelta = new Vector2(1100, 88);
        hint.font = TMP_Settings.defaultFontAsset; hint.fontSize = 25; hint.alignment = TextAlignmentOptions.Center;
        hint.color = Color.white; hint.raycastTarget = false;
        hint.text = inGame ? "點棋子 → 點落點　｜　點手牌 → 點目標\n長按棋子或卡牌查看資訊 · 雙指觸碰取消本次操作" : "Android 真機原型 · 支援直式／橫式\n先選擇本機對戰，或使用相同暗語建立／加入房間";
    }

    private static RectTransform MakeRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); return rect;
    }

    private Button MakeButton(string title, int index, UnityEngine.Events.UnityAction action)
    {
        var rect = MakeRect(title, controls);
        rect.anchorMin = rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-16, -180 - index * 88); rect.sizeDelta = new Vector2(190, 76);
        var background = rect.gameObject.AddComponent<Image>(); background.color = new Color(.06f, .18f, .23f, .94f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = background; button.onClick.AddListener(action);
        var labelRect = MakeRect("Label", rect);
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset; label.text = title; label.fontSize = 28;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return button;
    }

    public static bool IsOverControls(Vector2 position)
    {
        if (current == null || current.controls == null) return false;
        foreach (var button in current.controls.GetComponentsInChildren<Button>())
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, position, null)) return true;
        return false;
    }

    private void Cancel()
    {
        CardDragHandler.CancelActiveInteraction(); HandCardPreview.CloseCurrent(); input?.CancelPointerGesture();
    }
    private void Update()
    {
        if (hint != null) hint.gameObject.SetActive(input != null && Time.unscaledTime < hintUntil);
        if (cancel != null) cancel.interactable = CardDragHandler.BlocksBoardInput || HandCardPreview.IsOpen;
        if (GameplayPointer.CancelPressed && !ControlBindings.SuppressGameplay && !CardKeywordPopup.IsOpen)
        {
            if (CardDragHandler.BlocksBoardInput || HandCardPreview.IsOpen) Cancel();
            else settings?.ShowPanel();
        }
    }
    private void OnApplicationPause(bool paused) { if (paused) { Cancel(); PlayerPrefs.Save(); } }
    private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; if (current == this) current = null; }
}
