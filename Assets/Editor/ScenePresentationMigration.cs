using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>將程式建立的呈現物件烘焙至正式場景，並統一可動態補字的思源黑體。</summary>
public static class ScenePresentationMigration
{
    private const string FontPath = "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/思源黑體-Medium.asset";
    private static TMP_FontAsset font;
    private static readonly List<string> report = new List<string>();

    /// <summary>重新讀取已保存的場景，驗證固定介面、樣板引用、字型與元件完整性。</summary>
    [MenuItem("Tools/Chess/Presentation/Verify Saved Scene Objects")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先離開 Play Mode。");
        string original = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        var lines = new List<string>();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        try
        {
            if (TMP_Settings.defaultFontAsset != font || font.sourceFontFile == null ||
                font.atlasPopulationMode != AtlasPopulationMode.Dynamic || !font.isMultiAtlasTexturesEnabled)
                throw new InvalidOperationException("字型預設或動態補字設定不完整。");
            foreach (string path in new[] { "Assets/Scenes/StartScene.unity", "Assets/Scenes/ChessScene.unity", "Assets/Scenes/CardScene.unity" })
            {
                if (!File.Exists(path)) continue;
                Scene scene = EditorSceneManager.OpenScene(path);
                var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                var library = objects.Select(t => t.GetComponent<SceneObjectTemplates>()).FirstOrDefault(c => c != null);
                if (library == null || library.templates.Any(e => e.template == null)) throw new InvalidOperationException("缺少場景樣板：" + path);
                foreach (Transform item in objects)
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) != 0) throw new InvalidOperationException("Missing script: " + item.name);
                var texts = objects.Select(t => t.GetComponent<TMP_Text>()).Where(t => t != null).ToArray();
                foreach (TMP_Text text in texts)
                {
                    if (text.font != font) throw new InvalidOperationException("未統一字型：" + text.name);
                    string chinese = new string(text.text.Where(c => c >= '\u3400' && c <= '\u9fff').ToArray());
                    if (!font.HasCharacters(chinese, out uint[] missing, true, true)) throw new InvalidOperationException("中文缺字：" + text.name);
                }
                foreach (SettingsUI settings in objects.Select(t => t.GetComponent<SettingsUI>()).Where(s => s != null))
                {
                    if (settings.panel == null || settings.panel.GetComponent<SettingsPanelView>() == null) throw new InvalidOperationException("設定頁未保存。");
                    var data = new SerializedObject(settings.panel.GetComponent<SettingsPanelView>());
                    foreach (string name in new[] { "pages", "tabs", "bindingButtons", "actionButtons" })
                    {
                        var array = data.FindProperty(name);
                        if (array.arraySize == 0) throw new InvalidOperationException("空引用：" + name);
                        for (int i = 0; i < array.arraySize; i++) if (array.GetArrayElementAtIndex(i).objectReferenceValue == null) throw new InvalidOperationException("遺失引用：" + name);
                    }
                }
                foreach (CardDragHandler drag in objects.Select(t => t.GetComponent<CardDragHandler>()).Where(d => d != null))
                    if (drag.GetComponent<CardUnavailableHint>() == null || drag.GetComponentInChildren<CardGlowGraphic>(true) == null)
                        throw new InvalidOperationException("手牌樣板缺少提示或光暈。");
                lines.Add("PASS " + path + ": " + library.templates.Count + " templates, " + texts.Length + " Source Han Sans texts, no missing scripts or Chinese glyphs, saved UI references");
            }
        }
        catch (Exception error) { lines.Add("FAIL " + error); throw; }
        finally
        {
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
            File.WriteAllLines("output/scene-presentation-verification.txt", lines);
        }
    }

    /// <summary>保留場景備份後遷移正式場景，重跑時沿用現有樣板與玩家編輯。</summary>
    [MenuItem("Tools/Chess/Presentation/Bake Editable Scene Objects")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先離開 Play Mode。");
        report.Clear();
        Scene original = SceneManager.GetActiveScene();
        if (original.isDirty) EditorSceneManager.SaveScene(original);
        string originalPath = original.path;
        Directory.CreateDirectory("output/scene-presentation-backup");
        ConfigureFont();
        // Prefab 仍可獨立編輯，供未來新場景使用時也有完整字型與手牌元件。
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { PrepareCards(root); SetFonts(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string path in new[] { "Assets/Scenes/StartScene.unity", "Assets/Scenes/ChessScene.unity", "Assets/Scenes/CardScene.unity" })
        {
            if (!File.Exists(path)) continue;
            string backup = "output/scene-presentation-backup/" + Path.GetFileName(path);
            if (!File.Exists(backup)) File.Copy(path, backup);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            BakeScene(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            report.Add("PASS baked " + path);
        }
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(originalPath)) EditorSceneManager.OpenScene(originalPath);
        File.WriteAllLines("output/scene-presentation-migration.txt", report);
        Debug.Log("Scene presentation migration completed. " + string.Join("\n", report));
    }

    /// <summary>綁定原始 OTF、動態多圖集與 TMP 預設，並驗證目前專案使用的中文字。</summary>
    private static void ConfigureFont()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TmpFont/Fonts/Chinese/SourceHanSansTC-Medium.otf");
        if (font == null || source == null) throw new InvalidOperationException("缺少思源黑體或原始字型。");
        var data = new SerializedObject(font);
        data.FindProperty("m_SourceFontFile").objectReferenceValue = source;
        data.ApplyModifiedPropertiesWithoutUndo();
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.isMultiAtlasTexturesEnabled = true;
        font.ReadFontAssetDefinition();
        UnityEngine.TextCore.LowLevel.FontEngine.InitializeFontEngine();
        var faceError = UnityEngine.TextCore.LowLevel.FontEngine.LoadFontFace(source, Mathf.RoundToInt(font.faceInfo.pointSize));
        if (faceError != UnityEngine.TextCore.LowLevel.FontEngineError.Success)
            throw new InvalidOperationException("來源字型載入失敗：" + faceError);
        var settings = new SerializedObject(TMP_Settings.instance);
        settings.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
        var fallback = settings.FindProperty("m_fallbackFontAssets");
        bool present = false;
        for (int i = 0; i < fallback.arraySize; i++) present |= fallback.GetArrayElementAtIndex(i).objectReferenceValue == font;
        if (!present) { int index = fallback.arraySize++; fallback.GetArrayElementAtIndex(index).objectReferenceValue = font; }
        settings.ApplyModifiedPropertiesWithoutUndo();
        var characters = new HashSet<char>();
        foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
            .Concat(new[] { "Assets/Editor/CardSpreadsheetSnapshot.json" }))
        {
            string text = File.ReadAllText(path);
            text = Regex.Replace(text, @"\\u([0-9a-fA-F]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
            foreach (char c in text) if ((c >= '\u3400' && c <= '\u9fff') || (c >= '\u3000' && c <= '\u303f') || (c >= 32 && c <= 126)) characters.Add(c);
        }
        string required = new string(characters.OrderBy(c => c).ToArray());
        font.HasCharacters(required, out uint[] missing, false, true);
        string missingChinese = new string((missing ?? new uint[0]).Where(c => c >= '\u3400' && c <= '\u9fff').Select(c => (char)c).ToArray());
        if (missingChinese.Length != 0) throw new InvalidOperationException("思源黑體缺少現用中文字：" + missingChinese);
        EditorUtility.SetDirty(font); EditorUtility.SetDirty(TMP_Settings.instance);
        report.Add("PASS Source Han Sans dynamic multi-atlas, default/fallback, " + characters.Count + " characters checked; no missing Chinese");
    }

    /// <summary>建立固定介面，並將可變數量的項目保存在清楚命名的場景樣板庫。</summary>
    private static void BakeScene(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var library = roots.SelectMany(r => r.GetComponentsInChildren<SceneObjectTemplates>(true)).FirstOrDefault();
        if (library == null)
        {
            var root = new GameObject("Editable Scene Templates — 可變數量物件樣板");
            library = root.AddComponent<SceneObjectTemplates>(); root.SetActive(false);
        }
        Add(library, "Card glow", () => {
            var parent = new GameObject("Temporary", typeof(RectTransform));
            GameObject glow = CardGlowGraphic.Create((RectTransform)parent.transform).gameObject;
            glow.transform.SetParent(null, false); Object.DestroyImmediate(parent); return glow;
        });
        Add(library, "Damage calculation", () => DamageResolutionPanel.Create(font).gameObject);
        Add(library, "Status icon", () => Ui("Status icon", new Vector2(80, 80), typeof(Image), typeof(StatusIconHover)));
        Add(library, "Returning card slot", () => Ui("Returning card slot", new Vector2(160, 240), typeof(LayoutElement)));
        Add(library, "Card unavailable hint", () => Hint("Card unavailable hint", new Vector2(440, 84)));
        Add(library, "Card drop hint", () => Hint("Card drop hint", new Vector2(440, 52)));
        Add(library, "Legal card target", () => new GameObject("Legal card target", typeof(LineRenderer)));
        Add(library, "Card target feedback", () => new GameObject("Card target feedback", typeof(CardTargetFeedback)));
        Add(library, "Card played successfully", () => new GameObject("Card played successfully", typeof(LineRenderer), typeof(CardPlayFeedback)));
        Add(library, "One shot audio", () => {
            var sound = new GameObject("One shot audio", typeof(AudioSource));
            sound.GetComponent<AudioSource>().playOnAwake = false; sound.GetComponent<AudioSource>().spatialBlend = 1; return sound;
        });
        foreach (CardAsset card in Resources.LoadAll<CardAsset>("Cards"))
        {
            foreach (GameObject effect in card.definition.animations.Select(a => a.effectPrefab)
                .Concat(new[] { card.definition.skillEffectPrefab }).Where(p => p != null).Distinct())
            {
                if (library.templates.Any(e => e.sourcePrefab == effect)) continue;
                string key = "Card effect — " + card.definition.id + " — " + effect.name;
                Add(library, key, () => (GameObject)PrefabUtility.InstantiatePrefab(effect, library.transform));
                library.templates.Find(e => e.key == key).sourcePrefab = effect;
            }
        }

        foreach (SettingsUI settings in roots.SelectMany(r => r.GetComponentsInChildren<SettingsUI>(true)))
        {
            if (settings.panel == null) continue;
            SettingsPanelView view = SettingsPanelView.Build(settings); view.BakeActionReferences();
            settings.panel.SetActive(false); EditorUtility.SetDirty(settings); EditorUtility.SetDirty(view);
        }
        foreach (CardInfoUI info in roots.SelectMany(r => r.GetComponentsInChildren<CardInfoUI>(true)))
        { info.BakeControls(); info.Hide(); EditorUtility.SetDirty(info); }
        foreach (StartMenuController menu in roots.SelectMany(r => r.GetComponentsInChildren<StartMenuController>(true)))
        {
            var so = new SerializedObject(menu); var property = so.FindProperty("statusText");
            if (property.objectReferenceValue != null) continue;
            Canvas canvas = menu.GetComponentInParent<Canvas>() ?? roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).FirstOrDefault();
            var go = Ui("MenuStatus", new Vector2(900, 90), typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0); rect.pivot = new Vector2(0.5f, 0); rect.anchoredPosition = new Vector2(0, 20);
            var text = go.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 24; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            go.SetActive(false); property.objectReferenceValue = text; so.ApplyModifiedPropertiesWithoutUndo();
        }
        GameObject gameUi = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "CardGameUI")?.gameObject;
        if (gameUi != null && gameUi.GetComponent<GameFlowUI>() == null) gameUi.AddComponent<GameFlowUI>();
        foreach (Transform item in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
        {
            if (item.name == "DamageResultFly" && item.GetComponent<CanvasGroup>() == null) item.gameObject.AddComponent<CanvasGroup>();
            if (item.name == "UsingCardRoot" && item.GetComponent<Canvas>() == null) item.gameObject.AddComponent<Canvas>();
        }
        // 將已引用的 UI Prefab 實例也放進場景，讓手牌、牌背、紀錄及演出都能從 Hierarchy 編輯。
        var copies = new Dictionary<GameObject, GameObject>();
        foreach (MonoBehaviour component in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray())
        {
            if (component == null || component is Board || component is ChessCard || component is SceneObjectTemplates) continue;
            var so = new SerializedObject(component); var property = so.GetIterator(); bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object reference = property.objectReferenceValue;
                GameObject prefab = reference as GameObject ?? (reference as Component)?.gameObject;
                if (prefab == null || !EditorUtility.IsPersistent(prefab) || prefab.GetComponentInChildren<Piece>(true) != null) continue;
                string path = AssetDatabase.GetAssetPath(prefab);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                GameObject assetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!copies.TryGetValue(assetRoot, out GameObject copy))
                {
                    copy = library.transform.Cast<Transform>().Select(t => t.gameObject)
                        .FirstOrDefault(g => PrefabUtility.GetCorrespondingObjectFromSource(g) == assetRoot);
                    if (copy == null)
                    {
                        copy = (GameObject)PrefabUtility.InstantiatePrefab(assetRoot, library.transform);
                        copy.name = "Prefab — " + assetRoot.name; PrepareCards(copy);
                    }
                    copies.Add(assetRoot, copy);
                }
                GameObject target = copy.GetComponentsInChildren<Transform>(true)
                    .Select(t => t.gameObject).First(g => PrefabUtility.GetCorrespondingObjectFromSource(g) == prefab);
                property.objectReferenceValue = reference is GameObject ? (Object)target :
                    target.GetComponents<Component>().First(c => PrefabUtility.GetCorrespondingObjectFromSource(c) == reference);
                changed = true;
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (GameObject root in scene.GetRootGameObjects()) { PrepareCards(root); SetFonts(root); }
        EditorUtility.SetDirty(library);
    }

    /// <summary>僅建立缺少的樣板，重跑不重置使用者排版。</summary>
    private static void Add(SceneObjectTemplates library, string key, Func<GameObject> create)
    {
        if (library.templates.Any(e => e.key == key && e.template != null)) return;
        GameObject go = create(); go.name = key; go.transform.SetParent(library.transform, false);
        go.SetActive(true); library.templates.Add(new SceneObjectTemplates.Entry { key = key, template = go });
    }

    /// <summary>建立帶完整 CanvasRenderer 的 UI 樣板。</summary>
    private static GameObject Ui(string name, Vector2 size, params Type[] components)
    {
        var go = new GameObject(name, new[] { typeof(RectTransform), typeof(CanvasRenderer) }.Concat(components).ToArray());
        ((RectTransform)go.transform).sizeDelta = size; return go;
    }

    /// <summary>建立可在 Scene 編輯的提示底板與思源黑體文字。</summary>
    private static GameObject Hint(string name, Vector2 size)
    {
        var go = Ui(name, size, typeof(Image)); ((RectTransform)go.transform).pivot = new Vector2(0, 1);
        var image = go.GetComponent<Image>(); image.color = new Color(0.02f, 0.04f, 0.06f, 0.96f); image.raycastTarget = false;
        var label = Ui("Text", size, typeof(TextMeshProUGUI)); label.transform.SetParent(go.transform, false);
        var text = label.GetComponent<TMP_Text>(); text.font = font; text.fontSize = 24; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        var rect = text.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(16, 8); rect.offsetMax = new Vector2(-16, -8); return go;
    }

    /// <summary>手牌本體預先放置提示元件與光暈，遊戲只控制顯示狀態。</summary>
    private static void PrepareCards(GameObject root)
    {
        foreach (CardDragHandler drag in root.GetComponentsInChildren<CardDragHandler>(true))
        {
            if (drag.GetComponent<CardUnavailableHint>() == null) drag.gameObject.AddComponent<CardUnavailableHint>();
            CardGlowGraphic glow = CardGlowGraphic.Create((RectTransform)drag.transform); glow.enabled = false;
        }
    }

    /// <summary>統一 TMP 與舊版 UGUI Text 字型，保留字級、顏色與對齊。</summary>
    private static void SetFonts(GameObject root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font == font) continue;
            text.font = font; text.fontSharedMaterial = font.material; EditorUtility.SetDirty(text);
        }
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        { text.font = font.sourceFontFile; EditorUtility.SetDirty(text); }
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;
            var so = new SerializedObject(behaviour); var property = so.GetIterator(); bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (property.objectReferenceValue is TMP_FontAsset) { property.objectReferenceValue = font; changed = true; }
                else if (property.objectReferenceValue is Font) { property.objectReferenceValue = font.sourceFontFile; changed = true; }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
