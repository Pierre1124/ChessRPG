using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>將初次遷移的獨立副本接回 Prefab，保留場景引用及根物件擺放。</summary>
public static class ScenePrefabLinkRepair
{
    /// <summary>以目前 Prefab 作為卡面樣式來源，修復三個場景的樣板連結。</summary>
    [MenuItem("Tools/Chess/Presentation/Repair Scene Prefab Links")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先離開 Play Mode。");
        string original = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        var report = new List<string>();
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        try
        {
            foreach (string path in new[] { "Assets/Scenes/StartScene.unity", "Assets/Scenes/ChessScene.unity", "Assets/Scenes/CardScene.unity" })
            {
                if (!File.Exists(path)) continue;
                Scene scene = EditorSceneManager.OpenScene(path);
                var library = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneObjectTemplates>(true)).FirstOrDefault();
                if (library == null) continue;
                foreach (Transform child in library.transform.Cast<Transform>().ToArray())
                {
                    if (!child.name.StartsWith("Prefab — ") || PrefabUtility.IsPartOfPrefabInstance(child.gameObject)) continue;
                    string name = child.name.Substring("Prefab — ".Length);
                    GameObject[] matches = prefabs.Where(p => p.name == name).ToArray();
                    if (matches.Length == 0)
                    {
                        var candidates = library.GetComponentsInChildren<Transform>(true)
                            .Where(t => t.name == name && PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                            .Where(t => !(t is RectTransform) || !(child is RectTransform) ||
                                ((RectTransform)t).sizeDelta == ((RectTransform)child).sizeDelta).ToArray();
                        if (candidates.Length != 1) throw new InvalidOperationException("無法唯一定位 Prefab 子物件：" + name);
                        var childMap = new Dictionary<Object, Object>();
                        Map(child, candidates[0], childMap); Rewire(scene, child, childMap);
                        Object.DestroyImmediate(child.gameObject);
                        report.Add("PASS " + path + " restored nested reference " + name);
                        continue;
                    }
                    if (matches.Length != 1) throw new InvalidOperationException("無法唯一定位 Prefab：" + name);
                    GameObject old = child.gameObject;
                    var copy = library.transform.Cast<Transform>().Select(t => t.gameObject)
                        .FirstOrDefault(g => PrefabUtility.GetCorrespondingObjectFromSource(g) == matches[0]);
                    if (copy == null)
                    {
                        copy = (GameObject)PrefabUtility.InstantiatePrefab(matches[0], child.parent);
                        copy.name = old.name; copy.transform.SetSiblingIndex(child.GetSiblingIndex());
                        copy.transform.localPosition = child.localPosition; copy.transform.localRotation = child.localRotation;
                        copy.transform.localScale = child.localScale; copy.SetActive(old.activeSelf);
                    }
                    var remap = new Dictionary<Object, Object>();
                    Map(old.transform, copy.transform, remap);
                    Rewire(scene, child, remap);
                    Object.DestroyImmediate(old);
                    report.Add("PASS " + path + " linked " + name);
                }
                // 特效樣板同樣接回其資產引用。
                foreach (var entry in library.templates)
                {
                    if (entry.sourcePrefab == null || entry.template == null || PrefabUtility.IsPartOfPrefabInstance(entry.template)) continue;
                    GameObject old = entry.template;
                    entry.template = (GameObject)PrefabUtility.InstantiatePrefab(entry.sourcePrefab, library.transform);
                    entry.template.name = entry.key; Object.DestroyImmediate(old);
                }
                EditorUtility.SetDirty(library);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                VerifyCard(scene, report);
            }
        }
        catch (Exception error) { report.Add("FAIL " + error); throw; }
        finally
        {
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
            File.WriteAllLines("output/scene-prefab-links.txt", report);
        }
    }

    /// <summary>轉移場景元件引用，排除即將被替換的舊樣板。</summary>
    private static void Rewire(Scene scene, Transform old, Dictionary<Object, Object> remap)
    {
        foreach (MonoBehaviour component in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
        {
            if (component == null || component.transform.IsChildOf(old)) continue;
            var so = new SerializedObject(component); var property = so.GetIterator(); bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) continue;
                if (remap.TryGetValue(property.objectReferenceValue, out Object replacement))
                { property.objectReferenceValue = replacement; changed = true; }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>依子階層與元件順序建立引用轉移表。</summary>
    private static void Map(Transform old, Transform copy, Dictionary<Object, Object> map)
    {
        map[old.gameObject] = copy.gameObject; map[old] = copy;
        foreach (Component component in old.GetComponents<Component>())
        {
            if (component == null) continue;
            Component replacement = copy.GetComponent(component.GetType());
            if (replacement != null) map[component] = replacement;
        }
        foreach (Transform child in old)
        {
            Transform target = copy.Find(child.name);
            if (target != null) Map(child, target, map);
        }
    }

    /// <summary>確認實際手牌引用連結到 CardImage，且文字材質、字級與排版設定和 Prefab 一致。</summary>
    private static void VerifyCard(Scene scene, List<string> report)
    {
        foreach (CardHandManager hand in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CardHandManager>(true)))
        {
            var template = (GameObject)new SerializedObject(hand).FindProperty("cardPrefab").objectReferenceValue;
            if (template == null || !PrefabUtility.IsPartOfPrefabInstance(template)) throw new InvalidOperationException("手牌未連結到 Prefab。");
            foreach (TMP_Text text in template.GetComponentsInChildren<TMP_Text>(true))
            {
                TMP_Text source = PrefabUtility.GetCorrespondingObjectFromSource(text);
                if (source == null || source.font != text.font || source.fontSharedMaterial != text.fontSharedMaterial ||
                    source.fontSize != text.fontSize || source.enableAutoSizing != text.enableAutoSizing ||
                    source.characterSpacing != text.characterSpacing || source.fontStyle != text.fontStyle)
                    throw new InvalidOperationException("卡面文字與 Prefab 不一致：" + text.name);
            }
            report.Add("PASS " + scene.path + " actual hand template font/material/size/auto-size/spacing/style match Prefab");
        }
    }
}
