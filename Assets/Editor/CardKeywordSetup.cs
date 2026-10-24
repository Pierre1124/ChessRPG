using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CardKeywordSetup
{
    public const string PrefabPath = "Assets/Resources/UI/CardKeywordPopup.prefab";

    [MenuItem("Tools/Chess/Keywords/Create Missing Popup Prefab")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        Directory.CreateDirectory("Assets/Resources/UI");
        var root = new GameObject("CardKeywordPopup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CardKeywordPopup));
        try
        {
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32010;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            var popup = root.GetComponent<CardKeywordPopup>();
            var shade = Rect("Backdrop", root.transform);
            Stretch(shade);
            shade.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .7f);
            popup.backdropButton = shade.gameObject.AddComponent<Button>();
            var panel = Rect("Panel", root.transform);
            panel.anchorMin = new Vector2(.15f, .15f); panel.anchorMax = new Vector2(.85f, .85f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(.07f, .1f, .14f, 1);
            var title = Label("Title", panel);
            title.fontSize = 32;
            title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.pivot = new Vector2(.5f, 1);
            title.rectTransform.offsetMin = new Vector2(28, -70); title.rectTransform.offsetMax = new Vector2(-28, -20);
            popup.titleText = title;
            popup.closeButton = GameFlowUI.CreateActionButton(panel, "Close", "關閉", Vector2.zero, new Vector2(180, 48), title.font, () => { });
            var close = (RectTransform)popup.closeButton.transform;
            close.anchorMin = close.anchorMax = new Vector2(.5f, 0);
            close.anchoredPosition = new Vector2(0, 40);
            var viewport = Rect("RulesScroll", panel);
            Stretch(viewport); viewport.offsetMin = new Vector2(28, 82); viewport.offsetMax = new Vector2(-28, -86);
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .01f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            var detail = Label("Description", viewport);
            var rect = detail.rectTransform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero;
            detail.fontSize = 26; detail.richText = false; detail.text = "名詞詳細說明";
            detail.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = rect; scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            popup.scroll = scroll; popup.detailText = detail;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("Keyword popup prefab created: " + PrefabPath);
    }
    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); return rect;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static TextMeshProUGUI Label(string name, Transform parent)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.color = Color.white; label.raycastTarget = false;
        return label;
    }
}
