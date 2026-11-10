using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

/// <summary>Exercises real Input System touch events in the saved game scene.</summary>
[InitializeOnLoad]
public static class MobilePrototypeRegression
{
    private const string Key = "ChessRPG.MobileRegression";
    private const string Output = "output/mobile-prototype-regression.txt";
    private static IEnumerator routine;
    private static Touchscreen screen;
    private static double deadline;
    private static string runtimeError;
    private static InputDevice[] suspendedPointers;
    static MobilePrototypeRegression()
    {
        EditorApplication.playModeStateChanged += Changed;
        AssemblyReloadEvents.beforeAssemblyReload += CleanupInput;
        EditorApplication.quitting += CleanupInput;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (SessionState.GetBool(Key, false) && runtimeError == null &&
                (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) runtimeError = message + "\n" + stack;
        };
    }

    [MenuItem("Tools/Chess/Mobile/Run Touch Regression")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Photon.Pun.PhotonNetwork.IsConnected)
            throw new InvalidOperationException("Run from idle, disconnected Edit Mode.");
        SessionState.SetString(Key + "Scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key + "Preview", EditorPrefs.GetBool("ChessRPG.MobilePrototypePreview", false));
        SessionState.SetInt(Key + "Size", (int)GameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(EditorWindow.GetWindow(GameViewType)));
        SetResolution(1080, 2340);
        runtimeError = null;
        SessionState.SetBool(Key, true);
        EditorPrefs.SetBool("ChessRPG.MobilePrototypePreview", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/StartScene.unity");
        LogicManager.SetNextGameMode(false);
        Directory.CreateDirectory("output"); File.WriteAllText(Output, "Mobile touch Play Mode checks\n");
        EditorApplication.isPlaying = true;
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.ExitingPlayMode) CleanupInput();
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            routine = Checks(); deadline = EditorApplication.timeSinceStartup + 180;
            var runner = new GameObject("Mobile regression runner");
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<MobileRegressionRunner>();
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            string path = SessionState.GetString(Key + "Scene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            EditorPrefs.SetBool("ChessRPG.MobilePrototypePreview", SessionState.GetBool(Key + "Preview", false));
            GameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(EditorWindow.GetWindow(GameViewType), SessionState.GetInt(Key + "Size", 0));
            SessionState.SetBool(Key, false); routine = null; screen = null;
            if (Environment.GetCommandLineArgs().Contains("-mobileRegressionExit"))
                EditorApplication.Exit(File.ReadAllText(Output).Contains("PASS: all mobile touch checks completed") ? 0 : 1);
        }
    }
    public static void Tick()
    {
        if (routine == null || !EditorApplication.isPlaying) return;
        try
        {
            if (runtimeError != null) throw new Exception("Runtime error during touch regression: " + runtimeError);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out");
            if (routine.MoveNext()) return;
            File.AppendAllText(Output, "PASS: all mobile touch checks completed\n"); Finish();
        }
        catch (Exception error) { File.AppendAllText(Output, "FAIL: " + error + "\n"); Debug.LogException(error); Finish(); }
    }
    private static void Finish()
    {
        CleanupInput();
        EditorApplication.isPlaying = false;
    }
    private static void CleanupInput()
    {
        routine = null;
        if (screen != null && screen.added) InputSystem.RemoveDevice(screen);
        screen = null;
        if (suspendedPointers != null) foreach (var device in suspendedPointers) if (device.added) InputSystem.EnableDevice(device);
        suspendedPointers = null;
    }
    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        File.AppendAllText(Output, "PASS: " + description + "\n");
    }
    private static IEnumerator Wait(float seconds)
    {
        float until = Time.unscaledTime + seconds;
        int first = Time.frameCount;
        while (Time.unscaledTime < until || Time.frameCount <= first + 2) yield return null;
    }
    private static void Touch(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
    {
        InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = phase, position = position });
    }
    private static object Selected(InputManager input) => typeof(InputManager).GetField("selectedPiece", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
    private static Button MenuButton(string method)
    {
        return Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(button =>
            (method != "StartGame" || button.name == "StartGame") &&
            Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i => button.onClick.GetPersistentMethodName(i) == method));
    }
    private static IEnumerator TapButton(Button button, int id)
    {
        var canvas = button.GetComponentInParent<Canvas>();
        var rect = (RectTransform)button.transform;
        Vector2 position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        File.AppendAllText(Output, $"Tap {button.name} at {position}; screen {Screen.width}x{Screen.height}; hits: {string.Join(",",hits.Select(hit=>hit.gameObject.name))}\n");
        var trace = button.gameObject.AddComponent<MobileRegressionPointerTrace>();
        Touch(id, UnityEngine.InputSystem.TouchPhase.Began, position);
        var wait = Wait(.15f); while (wait.MoveNext()) yield return null;
        File.AppendAllText(Output, $"Touch held={screen.primaryTouch.press.isPressed}, position={screen.primaryTouch.position.ReadValue()}, frame={Time.frameCount}\n");
        Touch(id, UnityEngine.InputSystem.TouchPhase.Ended, position);
        wait = Wait(.25f); while (wait.MoveNext()) yield return null;
        if (trace != null) Object.Destroy(trace);
    }
    private static IEnumerator Checks()
    {
        // Real desktop mouse movement otherwise replaces UI touch pointers mid-gesture.
        suspendedPointers = InputSystem.devices.Where(device => device.enabled && (device is Mouse || device is Pen)).ToArray();
        foreach (var device in suspendedPointers) InputSystem.DisableDevice(device);
        var wait = Wait(3f); while (wait.MoveNext()) yield return null;
        Check(SceneManager.GetActiveScene().name == "StartScene", "Regression starts at the shipped startup scene");
        Check(Object.FindObjectsByType<StandaloneInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0, "Startup scene contains no legacy UI input module");
        Check(EventSystem.current != null && EventSystem.current.currentInputModule is InputSystemUIInputModule, "Startup UI uses Input System");
        screen = InputSystem.AddDevice<Touchscreen>();
        // Let the UI module resolve the newly added device before the first press.
        wait = Wait(.5f); while (wait.MoveNext()) yield return null;
        var menu = Object.FindFirstObjectByType<StartMenuController>();
        Check(menu != null, "Main menu initialized");
        Check(MobilePortraitLayout.IsPortrait, "Startup runs at portrait phone resolution");
        ScreenCapture.CaptureScreenshot("output/mobile-portrait-menu.png");
        wait = Wait(.3f); while (wait.MoveNext()) yield return null;
        var tap = TapButton(MenuButton("OpenDeckEditor"), 20); while (tap.MoveNext()) yield return null;
        Check(menu.deckEditorPanel.activeInHierarchy, "Finger tap opens deck editor from main menu");
        ScreenCapture.CaptureScreenshot("output/mobile-portrait-deck.png");
        wait = Wait(.3f); while (wait.MoveNext()) yield return null;
        tap = TapButton(MenuButton("CloseDeckEditor"), 21); while (tap.MoveNext()) yield return null;
        Check(!menu.deckEditorPanel.activeInHierarchy, "Finger tap closes deck editor");
        tap = TapButton(MenuButton("StartGame"), 22); while (tap.MoveNext()) yield return null;
        while (SceneManager.GetActiveScene().name != "ChessScene") yield return null;
        wait = Wait(3f); while (wait.MoveNext()) yield return null;
        Check(EventSystem.current.currentInputModule is InputSystemUIInputModule, "Touch start button loads playable game scene with new UI input");
        var input = Object.FindFirstObjectByType<InputManager>();
        var logic = Object.FindFirstObjectByType<LogicManager>();
        Check(input != null && logic != null && !logic.IsOperationLocked, "Game scene initializes and accepts input");
        Check(Object.FindFirstObjectByType<MobilePrototype>() != null, "Mobile shell created");
        Check(Object.FindObjectsByType<MobileSafeArea>(FindObjectsSortMode.None).Length > 0, "Safe-area adapters created");
        ScreenCapture.CaptureScreenshot("output/mobile-portrait-board.png");
        Piece pawn = logic.boardMap[4, 1];
        Vector2 origin = Camera.main.WorldToScreenPoint(pawn.transform.position + Vector3.up * .25f);
        Vector2 destination = Camera.main.WorldToScreenPoint(new Vector3(4, .05f, 2));
        Touch(1, UnityEngine.InputSystem.TouchPhase.Began, origin);
        wait = Wait(.1f); while (wait.MoveNext()) yield return null;
        Check(GameplayPointer.IsTouch && GameplayPointer.Held, "Touchscreen drives the shared pointer");
        Touch(1, UnityEngine.InputSystem.TouchPhase.Ended, origin);
        wait = Wait(.15f); while (wait.MoveNext()) yield return null;
        Check(Selected(input) == pawn, "Finger tap selects a real pawn");
        Touch(2, UnityEngine.InputSystem.TouchPhase.Began, destination);
        wait = Wait(.1f); while (wait.MoveNext()) yield return null;
        Touch(2, UnityEngine.InputSystem.TouchPhase.Ended, destination);
        wait = Wait(3f); while (wait.MoveNext()) yield return null;
        Check(logic.boardMap[4, 2] == pawn && !logic.isWhiteTurn, "Finger tap moves pawn and advances turn");
        while (logic.IsOperationLocked) yield return null;
        Piece blackPawn = logic.boardMap[4, 6];
        origin = Camera.main.WorldToScreenPoint(blackPawn.transform.position + Vector3.up * .25f);
        Touch(3, UnityEngine.InputSystem.TouchPhase.Began, origin);
        wait = Wait(.1f); while (wait.MoveNext()) yield return null;
        Touch(4, UnityEngine.InputSystem.TouchPhase.Began, origin + new Vector2(60, 0));
        wait = Wait(.15f); while (wait.MoveNext()) yield return null;
        Check(GameplayPointer.Canceled, "Second finger cancels gesture");
        Touch(4, UnityEngine.InputSystem.TouchPhase.Ended, origin + new Vector2(60, 0));
        Touch(3, UnityEngine.InputSystem.TouchPhase.Ended, origin);
        wait = Wait(.2f); while (wait.MoveNext()) yield return null;
        Check(Selected(input) == null && logic.boardMap[4, 6] == blackPawn, "Canceled gesture never selects or moves a piece");
        Touch(5, UnityEngine.InputSystem.TouchPhase.Began, origin);
        wait = Wait(.8f); while (wait.MoveNext()) yield return null;
        Touch(5, UnityEngine.InputSystem.TouchPhase.Ended, origin);
        wait = Wait(.2f); while (wait.MoveNext()) yield return null;
        var info = (CardInfoUI)typeof(InputManager).GetField("cardInfoUI", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
        var inspected = typeof(CardInfoUI).GetField("inspectedPiece", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info);
        Check(inspected == blackPawn && Selected(input) == null, "Long press inspects without selecting a move");
        info.Hide();
        var toggle = Object.FindFirstObjectByType<HandCardToggle>();
        Check(toggle != null, "Active hand toggle exists");
        typeof(HandCardToggle).GetMethod("SetExpanded", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(toggle, new object[] { true });
        wait = Wait(.8f); while (wait.MoveNext()) yield return null;
        var card = toggle.GetComponentInChildren<CardDragHandler>();
        Check(card != null, "Active hand contains a card");
        ScreenCapture.CaptureScreenshot("output/mobile-portrait-hand.png");
        var rect = (RectTransform)card.transform;
        var canvas = card.GetComponentInParent<UnityEngine.Canvas>();
        Vector2 cardPoint = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center));
        Touch(6, UnityEngine.InputSystem.TouchPhase.Began, cardPoint);
        wait = Wait(.8f); while (wait.MoveNext()) yield return null;
        Touch(6, UnityEngine.InputSystem.TouchPhase.Ended, cardPoint);
        wait = Wait(.2f); while (wait.MoveNext()) yield return null;
        Check(HandCardPreview.IsOpen && !card.IsSelected, "Long press opens card preview without picking it up");
        HandCardPreview.CloseCurrent();
        wait = Wait(.2f); while (wait.MoveNext()) yield return null;
        Touch(7, UnityEngine.InputSystem.TouchPhase.Began, cardPoint);
        wait = Wait(.1f); while (wait.MoveNext()) yield return null;
        Touch(7, UnityEngine.InputSystem.TouchPhase.Ended, cardPoint);
        wait = Wait(.2f); while (wait.MoveNext()) yield return null;
        Check(card.IsSelected, "Touch tap picks up a hand card");
        CardDragHandler.CancelActiveInteraction();
        wait = Wait(.2f); while (wait.MoveNext()) yield return null;
        Check(!card.IsSelected && !CardDragHandler.BlocksBoardInput, "Cancel returns card and releases board input");
        Touch(8, UnityEngine.InputSystem.TouchPhase.Began, cardPoint);
        wait = Wait(.1f); while (wait.MoveNext()) yield return null;
        Touch(8, UnityEngine.InputSystem.TouchPhase.Ended, cardPoint);
        wait = Wait(.2f); while (wait.MoveNext()) yield return null;
        Check(card.IsSelected, "Card can be selected again after cancel");
        var hand = Object.FindFirstObjectByType<CardHandManager>();
        int recycleChances = hand.RecycleDrawChances;
        var recycle = hand.RecycleCardRoot;
        var recycleCanvas = recycle.GetComponentInParent<UnityEngine.Canvas>();
        Vector2 recyclePoint = RectTransformUtility.WorldToScreenPoint(recycleCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : recycleCanvas.worldCamera, recycle.TransformPoint(recycle.rect.center));
        Touch(9, UnityEngine.InputSystem.TouchPhase.Began, recyclePoint);
        wait = Wait(.1f); while (wait.MoveNext()) yield return null;
        Touch(9, UnityEngine.InputSystem.TouchPhase.Ended, recyclePoint);
        wait = Wait(.5f); while (wait.MoveNext()) yield return null;
        Check(card == null && hand.RecycleDrawChances == recycleChances + 1, "Second touch recycles the card exactly once");
        Check(!CardDragHandler.BlocksBoardInput, "Board input released after card resolution");
        ScreenCapture.CaptureScreenshot("output/mobile-prototype-preview.png");
        wait = Wait(.5f); while (wait.MoveNext()) yield return null;
        SetResolution(2340,1080);
        wait = Wait(1f); while (wait.MoveNext()) yield return null;
        Check(!MobilePortraitLayout.IsPortrait, "Rotation restores landscape layout");
        Check(Mathf.Abs(Camera.main.fieldOfView - 60f) < 1f, "Landscape camera field of view restored");
        ScreenCapture.CaptureScreenshot("output/mobile-landscape-board.png");
        var blackOrigin = Camera.main.WorldToScreenPoint(blackPawn.transform.position + Vector3.up * .25f);
        Touch(30, UnityEngine.InputSystem.TouchPhase.Began, blackOrigin);
        wait = Wait(.1f); while (wait.MoveNext()) yield return null;
        Touch(30, UnityEngine.InputSystem.TouchPhase.Ended, blackOrigin);
        wait = Wait(.2f); while (wait.MoveNext()) yield return null;
        Check(Selected(input) == blackPawn, "Board remains touchable after rotation");
        SetResolution(1080,1920);
        wait = Wait(1f); while (wait.MoveNext()) yield return null;
        Check(MobilePortraitLayout.IsPortrait, "Rotating back supports 9:16 portrait");
        var overflowChecks = CheckMobileOverflow(logic);
        while (overflowChecks.MoveNext()) yield return null;
        ScreenCapture.CaptureScreenshot("output/mobile-portrait-16x9.png");
        wait = Wait(.5f); while (wait.MoveNext()) yield return null;
        var feedbackChecks = CheckFieldFeedback();
        while (feedbackChecks.MoveNext()) yield return null;
    }

    private static Type GameViewType => typeof(Editor).Assembly.GetType("UnityEditor.GameView");
    private static IEnumerator CheckFieldFeedback()
    {
        foreach (string mode in new[] { "mobile-portrait", "mobile-landscape", "desktop" })
        {
            if (mode == "desktop")
            {
                EditorPrefs.SetBool("ChessRPG.MobilePrototypePreview", false);
                var mobile = Object.FindFirstObjectByType<MobilePrototype>();
                if (mobile != null) Object.Destroy(mobile.gameObject);
                var pause = Wait(.2f); while (pause.MoveNext()) yield return null;
                SceneManager.LoadScene("ChessScene");
            }
            SetResolution(mode == "mobile-portrait" ? 1080 : 1920, mode == "mobile-portrait" ? 2340 : 1080);
            var wait = Wait(2f); while (wait.MoveNext()) yield return null;
            var logic = Object.FindFirstObjectByType<LogicManager>();
            var card = Object.FindFirstObjectByType<ChessCard>().GetCard("F02");
            var places = logic.GetFieldPlaces();
            foreach (var place in places) place.SetCard(card);
            var visualizer = Object.FindFirstObjectByType<DamageCalculationVisualizer>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(DamageCalculationVisualizer).GetField("stepDuration", flags).SetValue(visualizer, 1.5f);
            var boardCamera = Object.FindFirstObjectByType<CameraController>();
            foreach (bool black in new[] { false, true })
            {
                if (black) boardCamera.BlackPerspective(); else boardCamera.WhitePerspective();
                wait = Wait(.3f); while (wait.MoveNext()) yield return null;
                var sequence = new DamageCalculationSequence { attacker = logic.boardMap[0,1], target = logic.boardMap[0,6], presentationOnly = true };
                sequence.steps.Add(new DamageCalculationStep { sourceCardId = card.id, title = card.cardName, modifierPhase = DamageModifierPhase.Activation });
                var phase = (IEnumerator)typeof(DamageCalculationVisualizer).GetMethod("PlayModifierPhase", flags).Invoke(visualizer, new object[] { new[] { sequence }, DamageCuePhase.AttackSources });
                visualizer.StartCoroutine(phase);
                wait = Wait(.4f); while (wait.MoveNext()) yield return null;
                var glows = Object.FindObjectsByType<FieldEffectFeedback>(FindObjectsSortMode.None);
                string label = mode + (black ? " black" : " white");
                Check(glows.Length > 0 && glows.All(glow => glow.glowRoot.gameObject.activeInHierarchy && glow.glowRoot.GetComponentInChildren<CardGlowGraphic>().canvasRenderer.GetMesh().vertexCount > 0), label + ": actual activation creates visible source glow");
                var panel = Object.FindObjectsByType<DamageResolutionPanel>(FindObjectsSortMode.None).First(p => p.AttachedPiece != null && p.AttachedPiece.GetComponent<FieldCardPlace>() != null);
                var corners = new Vector3[4]; ((RectTransform)panel.transform).GetWorldCorners(corners);
                Vector3 normal = Vector3.Cross(corners[1]-corners[0], corners[3]-corners[0]).normalized;
                Check(Mathf.Abs(Vector3.Dot(normal, Camera.main.transform.forward)) > .999f && Mathf.Abs(Vector3.Dot((corners[1]-corners[0]).normalized, (corners[3]-corners[0]).normalized)) < .001f, label + ": field icon faces camera without shear");
                ScreenCapture.CaptureScreenshot("output/field-feedback-" + mode + (black ? "-black" : "-white") + ".png");
                wait = Wait(1.8f); while (wait.MoveNext()) yield return null;
                Check(Object.FindObjectsByType<FieldEffectFeedback>(FindObjectsSortMode.None).Length == 0, label + ": glow expires after activation");
            }
        }
    }
    private static IEnumerator CheckMobileOverflow(LogicManager logic)
    {
        var log = Object.FindFirstObjectByType<OperateLogUI>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var button = (Button)typeof(OperateLogUI).GetField("operateLogButton", flags).GetValue(log);
        var panel = (GameObject)typeof(OperateLogUI).GetField("logListRoot", flags).GetValue(log);
        foreach (var resolution in new[] { new Vector2Int(1080,1920), new Vector2Int(1080,2340), new Vector2Int(2340,1080) })
        {
            SetResolution(resolution.x, resolution.y);
            var wait = Wait(.7f); while (wait.MoveNext()) yield return null;
            var tap = TapButton(button, 40); while (tap.MoveNext()) yield return null;
            wait = Wait(.8f); while (wait.MoveNext()) yield return null;
            var canvas = panel.GetComponentInParent<Canvas>();
            Rect bounds = MobilePanelBounds.ScreenBounds((RectTransform)panel.transform, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);
            Rect safe = Screen.safeArea;
            Check(bounds.width > 100 && bounds.height > 100 && bounds.xMin >= safe.xMin-1 && bounds.xMax <= safe.xMax+1 && bounds.yMin >= safe.yMin-1 && bounds.yMax <= safe.yMax+1, "Expanded move log fits safe area at " + resolution);
            ScreenCapture.CaptureScreenshot($"output/mobile-log-{resolution.x}x{resolution.y}.png");
            tap = TapButton(button, 41); while (tap.MoveNext()) yield return null;
            wait = Wait(.5f); while (wait.MoveNext()) yield return null;
        }
        SetResolution(1080,1920);
        var settle = Wait(.7f); while (settle.MoveNext()) yield return null;
        var places = logic.GetFieldPlaces();
        var card = Object.FindFirstObjectByType<ChessCard>().GetCard("F01");
        Check(logic.TryPlayFieldCard(card, places[0]) && logic.TryPlayFieldCard(card, places[1]), "Two field cards trigger real fusion");
        float until = Time.unscaledTime + 2.5f;
        while (Time.unscaledTime < until) { AssertFieldsVisible(places); yield return null; }
        Check(places.All(place => place.ActiveCard != null && place.ActiveCard.id == "F02"), "Field fusion finishes with F02");
        AssertFieldsVisible(places);
        ScreenCapture.CaptureScreenshot("output/mobile-field-fusion.png");
        settle = Wait(.3f); while (settle.MoveNext()) yield return null;
        SetResolution(2340,1080);
        settle = Wait(.7f); while (settle.MoveNext()) yield return null;
        SetResolution(1080,2340);
        settle = Wait(.7f); while (settle.MoveNext()) yield return null;
        AssertFieldsVisible(places);
        Check(true, "Fused field remains on screen during animation and after rotation");
    }
    private static void AssertFieldsVisible(System.Collections.Generic.IReadOnlyList<FieldCardPlace> places)
    {
        foreach (var place in places)
        {
            Bounds bounds = place.GetComponent<Collider>().bounds;
            for (int i=0;i<8;i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 p = Camera.main.WorldToScreenPoint(corner);
                if (p.z <= 0 || !Screen.safeArea.Contains(p)) throw new Exception("Field outside safe area: " + place.name + " at " + p);
            }
        }
    }
    private static void SetResolution(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance").GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var activeGroup = sizesType.GetProperty("currentGroupType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(sizes);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { activeGroup });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var kind = assembly.GetType("UnityEditor.GameViewSizeType");
        var entry = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { Enum.Parse(kind,"FixedResolution"), width, height, "Mobile regression" }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { entry });
        int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        GameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(EditorWindow.GetWindow(GameViewType), count-1);
    }
}

public class MobileRegressionRunner : MonoBehaviour
{
    private void Update() => MobilePrototypeRegression.Tick();
}

public class MobileRegressionPointerTrace : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public void OnPointerDown(PointerEventData data) => Trace("down", data);
    public void OnPointerUp(PointerEventData data) => Trace("up", data);
    public void OnPointerClick(PointerEventData data) => Trace("click", data);
    private void Trace(string phase, PointerEventData data) => File.AppendAllText("output/mobile-prototype-regression.txt", $"UI {name} {phase} {data.position} {data.pointerId}\n");
}
