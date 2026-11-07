using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Portrait overrides of authored UI. Restores the exact landscape values on rotation.</summary>
[DefaultExecutionOrder(1000)]
public sealed class MobilePortraitLayout : MonoBehaviour
{
    public static bool IsPortrait => MobilePrototype.Enabled && Screen.height > Screen.width;
    private readonly List<Action> restore = new List<Action>();
    private readonly HashSet<UnityEngine.Object> saved = new HashSet<UnityEngine.Object>();
    private Canvas[] canvases;
    private bool portrait;
    private Vector2 size;

    public static void Install()
    {
        var host = new GameObject("Mobile orientation layout");
        host.AddComponent<MobilePortraitLayout>();
    }
    private void Start()
    {
        canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Refresh();
    }
    private void LateUpdate()
    {
        if (portrait != IsPortrait || size != new Vector2(Screen.width, Screen.height)) Refresh();
    }
    private void Refresh()
    {
        CardDragHandler.CancelActiveInteraction();
        FindFirstObjectByType<InputManager>()?.CancelPointerGesture();
        HandCardPreview.CloseCurrent();
        foreach (var undo in restore) undo();
        restore.Clear(); saved.Clear();
        portrait = IsPortrait; size = new Vector2(Screen.width, Screen.height);
        if (portrait)
        {
            foreach (var canvas in canvases)
            {
                if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) continue;
                var mode = scaler.uiScaleMode; var resolution = scaler.referenceResolution;
                var match = scaler.screenMatchMode; float fraction = scaler.matchWidthOrHeight;
                restore.Add(() => { if (scaler == null) return; scaler.uiScaleMode = mode; scaler.referenceResolution = resolution; scaler.screenMatchMode = match; scaler.matchWidthOrHeight = fraction; });
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0;
            }
            LayoutMenu(); LayoutGame(); LayoutControls();
        }
        foreach (var toggle in FindObjectsByType<HandCardToggle>(FindObjectsInactive.Include, FindObjectsSortMode.None)) toggle.RefreshMobileLayout();
        Canvas.ForceUpdateCanvases();
    }
    private RectTransform Find(string path)
    {
        foreach (var canvas in canvases)
        {
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            string prefix = canvas.name + "/";
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string relative = path.Substring(prefix.Length);
            var found = canvas.transform.Find("Mobile safe area/" + relative) ?? canvas.transform.Find(relative);
            if (found != null) return found as RectTransform;
        }
        return null;
    }
    private void Save(RectTransform rect)
    {
        if (rect == null || !saved.Add(rect)) return;
        Vector2 min = rect.anchorMin, max = rect.anchorMax, pivot = rect.pivot, position = rect.anchoredPosition, delta = rect.sizeDelta;
        Vector3 scale = rect.localScale; Quaternion rotation = rect.localRotation;
        restore.Add(() => { if (rect == null) return; rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.sizeDelta = delta; rect.anchoredPosition = position; rect.localScale = scale; rect.localRotation = rotation; });
    }
    private RectTransform Place(string path, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 dimensions)
    {
        var rect = Find(path); if (rect == null) return null;
        Save(rect); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
        rect.anchoredPosition = position; rect.sizeDelta = dimensions; rect.localScale = Vector3.one;
        return rect;
    }
    private void Font(RectTransform rect, float points)
    {
        if (rect == null) return;
        foreach (var text in rect.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!saved.Add(text)) continue;
            float original = text.fontSize; bool auto = text.enableAutoSizing;
            Vector4 margin = text.margin;
            restore.Add(() => { if (text != null) { text.fontSize = original; text.enableAutoSizing = auto; text.margin = margin; } });
            text.enableAutoSizing = false; text.fontSize = points; text.margin = Vector4.zero;
        }
    }
    private void LayoutMenu()
    {
        Font(Place("Canvas/Title", new Vector2(.5f,.84f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(950,180)), 64);
        Place("Canvas/MainMenu", new Vector2(.5f,.53f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(880,520));
        Place("Canvas/StartMenu", new Vector2(.5f,.53f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(880,650));
        Font(Find("Canvas/MainMenu/StartGame"), 40); Font(Find("Canvas/MainMenu/StartMuitiGame"), 40); Font(Find("Canvas/MainMenu/EditDeck"), 40);
        Font(Place("Canvas/MainMenu/Saved deck summary", new Vector2(.5f,0), new Vector2(.5f,1), new Vector2(0,-50), new Vector2(860,160)), 32);
        Place("Canvas/MenuStatus", new Vector2(.5f,.12f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(920,160));
        var body = Place("Canvas/DeckEditorPanel/PanelBody", new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(1020,1660));
        if (body == null) return;
        // Let the existing vertical layout and scroll view use the taller portrait area.
        foreach (var element in body.GetComponentsInChildren<LayoutElement>(true))
        {
            float width = element.preferredWidth;
            if (width <= 0) continue;
            restore.Add(() => { if (element != null) element.preferredWidth = width; });
            element.preferredWidth = width * .62f;
        }
        var grid = body.GetComponentInChildren<GridLayoutGroup>(true);
        if (grid != null)
        {
            var cell = grid.cellSize; var constraint = grid.constraint; int columns = grid.constraintCount;
            restore.Add(() => { if (grid != null) { grid.cellSize = cell; grid.constraint = constraint; grid.constraintCount = columns; } });
            grid.cellSize = new Vector2(450,330); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
        }
    }
    private void LayoutGame()
    {
        Font(Place("CardGameUI/TurnText", new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,-24), new Vector2(600,90)),48);
        Health("WhiteHP", new Vector2(.04f,1), new Vector2(0,1));
        Health("BlackHP", new Vector2(.96f,1), new Vector2(1,1));
        Font(Place("CardGameUI/Draw", new Vector2(.04f,0), new Vector2(0,0), new Vector2(0,520), new Vector2(220,100)),40);
        Place("CardGameUI/RecycleCard", new Vector2(.96f,0), new Vector2(1,0), new Vector2(0,520), new Vector2(100,100));
        Place("CardGameUI/OperateLog", new Vector2(1,1), new Vector2(1,1), new Vector2(-24,-20), new Vector2(80,80));
        Place("CardGameUI/OperateLog/LogList", new Vector2(1,0), new Vector2(1,1), new Vector2(0,-10), new Vector2(700,700));
        Place("LayoutUI/Button (Legacy)", new Vector2(0,1), new Vector2(0,1), new Vector2(24,-20), new Vector2(90,90));
        var hand = Find("CardGameUI/HandCard"); Save(hand);
        Save(Find("CardGameUI/HandCard/HandToggleButton"));
        Font(Find("CardGameUI/HandCard/HandToggleButton"),32);
        Place("CardGameUI/CardInfro", new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(450,600));
        var info = Find("CardGameUI/CardInfro"); if (info != null) info.localScale = Vector3.one * 1.7f;
        Place("DMGSystem/FinalDMG", new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,-210), new Vector2(650,80));
        Place("PromotionUI/Panel", new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(1000,1100));
        foreach (var field in FindObjectsByType<FieldCardPlace>(FindObjectsSortMode.None))
        {
            var target = field.transform; Vector3 position = target.position;
            restore.Add(() => { if (target != null) target.position = position; });
            target.position = new Vector3(field.name.EndsWith("2") ? 5.6f : 1.4f, position.y, -2.1f);
        }
    }
    private void Health(string name, Vector2 anchor, Vector2 pivot)
    {
        var rect = Place("CardGameUI/"+name, anchor, pivot, new Vector2(0,-140), new Vector2(440,44));
        if (rect == null) return;
        var slider = rect.GetComponent<Slider>();
        if (slider != null)
        {
            var direction = slider.direction; restore.Add(() => { if (slider != null) slider.SetDirection(direction,false); });
            slider.SetDirection(Slider.Direction.LeftToRight,false);
        }
        Font(Place("CardGameUI/"+name+"/HPText", new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(400,65)),36);
    }
    private void LayoutControls()
    {
        Font(Place("Mobile controls/Safe mobile controls/Touch instructions", new Vector2(.5f,0), new Vector2(.5f,0), new Vector2(0,640), new Vector2(960,120)),30);
        string[] buttons = { "取消出牌", "重設視角", "操作說明" };
        for (int i=0;i<buttons.Length;i++)
            Font(Place("Mobile controls/Safe mobile controls/"+buttons[i],new Vector2(.5f,0),new Vector2(.5f,0),new Vector2((i-1)*330,16),new Vector2(310,88)),36);
    }
}
