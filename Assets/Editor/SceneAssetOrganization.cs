using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>將呈現樣板按功能歸位並保存成 Prefab，不重建使用者設計的 UI。</summary>
public static class SceneAssetOrganization
{
    /// <summary>整理正式場景，保留原物件、引用及排版並輸出可檢查的階層報告。</summary>
    [MenuItem("Tools/Chess/Presentation/Organize Editable Assets")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先離開 Play Mode。");
        string original = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        var report = new List<string>();
        Directory.CreateDirectory("output/organization-backup");
        try
        {
            foreach (string path in new[] { "Assets/Scenes/ChessScene.unity", "Assets/Scenes/StartScene.unity", "Assets/Scenes/CardScene.unity" })
            {
                if (!File.Exists(path)) continue;
                string backup = "output/organization-backup/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
                Scene scene = EditorSceneManager.OpenScene(path);
                Transform[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                var typography = all.Select(t => t.GetComponent<TMPro.TMP_Text>()).Where(t => t != null)
                    .ToDictionary(t => t, t => $"{t.font?.GetInstanceID()}|{t.fontSharedMaterial?.GetInstanceID()}|{t.fontSize}|{t.enableAutoSizing}|{t.fontSizeMin}|{t.fontSizeMax}");
                foreach (var settings in all.Select(t => t.GetComponent<SettingsUI>()).Where(s => s != null))
                {
                    if (settings.panel != null && settings.panel.transform != settings.transform)
                    {
                        settings.panel.transform.SetParent(settings.transform, true);
                        if (settings.panel.TryGetComponent<UnityEngine.UI.CanvasScaler>(out var sourceScaler) &&
                            settings.TryGetComponent<UnityEngine.UI.CanvasScaler>(out var ownerScaler))
                            EditorUtility.CopySerialized(sourceScaler, ownerScaler);
                        FitNestedCanvas(settings.panel);
                    }
                    if (settings.confirmationPopup != null && !settings.confirmationPopup.transform.IsChildOf(settings.transform))
                        settings.confirmationPopup.transform.SetParent(settings.transform, true);
                }
                var library = all.Select(t => t.GetComponent<SceneObjectTemplates>()).FirstOrDefault(c => c != null);
                if (library == null) continue;
                var damage = all.FirstOrDefault(t => t.name == "DMGSystem");
                var cardUi = all.FirstOrDefault(t => t.name == "CardGameUI");
                var effects = Group("BoardEffects", null, false);
                var audio = Group("AudioSystem", null, false);
                if (scene.name == "ChessScene") BakeTokens(library, all);
                foreach (var entry in library.templates)
                {
                    bool dmg = entry.key.Contains("Damage");
                    bool sound = entry.key == "One shot audio";
                    bool world = entry.key.Contains("token") || entry.key.StartsWith("Card effect") || entry.key == "Legal card target" || entry.key == "Card target feedback" || entry.key == "Card played successfully";
                    Transform owner = dmg && damage != null ? damage : sound ? audio : world ? effects : cardUi != null ? cardUi : library.transform;
                    Transform templates = Group("Templates", owner, owner is RectTransform);
                    templates.gameObject.SetActive(false);
                    entry.template.transform.SetParent(templates, false);
                    entry.runtimeParent = owner == library.transform ? null : owner;
                    if (dmg) FitNestedCanvas(entry.template);
                    SavePrefab(entry.template, scene.name, dmg ? "Damage" : sound ? "Audio" : world ? "BoardEffects" : "Cards");
                    if (dmg && entry.template.TryGetComponent<Canvas>(out var canvas)) canvas.overrideSorting = true;
                }
                // 沒有登錄 key 的手牌、卡背與傷害步驟樣板也按用途歸類。
                foreach (Transform child in library.transform.Cast<Transform>().ToArray())
                {
                    if (child.name == "Templates") continue;
                    Transform owner = child.name.Contains("Damage") && damage != null ? damage : cardUi != null ? cardUi : library.transform;
                    if (owner == library.transform) continue;
                    child.SetParent(Group("Templates", owner, owner is RectTransform), false);
                    SavePrefab(child.gameObject, scene.name, owner == damage ? "Damage" : "Cards");
                }
                library.name = "SceneTemplates";
                // Netcode NetworkManager 必須是根物件，否則 Editor 會顯示阻擋對話框。
                foreach (var network in all.Where(t => t.name == "NetworkManager")) network.SetParent(null, true);
                var systems = Group("Systems", null, false);
                foreach (var root in scene.GetRootGameObjects())
                    if (new[] { "LogicManager", "InputManager", "CardManager", "MultiplayerGameController", "EventSystem", "SceneTemplates" }.Contains(root.name))
                        root.transform.SetParent(systems, true);
                if (damage != null)
                    foreach (var child in damage.Cast<Transform>().Where(t => t.name == "DamageResultFly").ToArray()) SavePrefab(child.gameObject, scene.name, "Damage");
                foreach (var item in all.Where(t => new[] { "LogText", "PhaseChange", "Alarm", "UsingCard" }.Contains(t.name)))
                    SavePrefab(item.gameObject, scene.name, "UI");
                foreach (var pair in typography)
                {
                    var t = pair.Key;
                    string current = $"{t.font?.GetInstanceID()}|{t.fontSharedMaterial?.GetInstanceID()}|{t.fontSize}|{t.enableAutoSizing}|{t.fontSizeMin}|{t.fontSizeMax}";
                    if (current != pair.Value) throw new InvalidOperationException("文字樣式發生改變: " + t.name);
                }
                report.Add("PASS preserved " + typography.Count + " text font/material/size settings");
                EditorUtility.SetDirty(library);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                foreach (var entry in library.templates)
                    if (entry.template == null || !PrefabUtility.IsPartOfPrefabInstance(entry.template)) throw new InvalidOperationException("樣板未連結 Prefab: " + entry.key);
                report.Add("PASS " + scene.name + ": " + library.templates.Count + " editable prefab templates");
                foreach (var root in scene.GetRootGameObjects()) Describe(root.transform, "", report);
            }
        }
        catch (Exception ex) { report.Add("FAIL " + ex); throw; }
        finally
        {
            File.WriteAllLines("output/scene-organization.txt", report);
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
        }
    }

    /// <summary>根 Canvas 改為巢狀時恢復滿版矩形，避免零縮放及失去原本排序。</summary>
    private static void FitNestedCanvas(GameObject go)
    {
        if (!go.TryGetComponent<Canvas>(out var canvas) || !(go.transform is RectTransform rect)) return;
        canvas.overrideSorting = true;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.anchoredPosition3D = Vector3.zero;
    }

    /// <summary>建立穩定的功能容器；UI 容器保持滿版座標。</summary>
    private static Transform Group(string name, Transform parent, bool ui)
    {
        var existing = parent != null ? parent.Find(name) : SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name)?.transform;
        if (existing != null) return existing;
        var go = new GameObject(name, ui ? typeof(RectTransform) : typeof(Transform));
        go.transform.SetParent(parent, false);
        if (go.transform is RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        return go.transform;
    }

    /// <summary>保留已有 Prefab 連結；新樣板保存後直接連回同一場景物件。</summary>
    private static void SavePrefab(GameObject go, string scene, string category)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(go)) return;
        string folder = "Assets/Prefabs/SceneTemplates/" + scene + "/" + category;
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        string name = string.Concat(go.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        PrefabUtility.SaveAsPrefabAssetAndConnect(go, folder + "/" + name + ".prefab", InteractionMode.AutomatedAction);
    }

    /// <summary>把原本執行時複製城堡外觀的 Token 烘焙成獨立網格與材質樣板。</summary>
    private static void BakeTokens(SceneObjectTemplates library, Transform[] all)
    {
        var board = all.Select(t => t.GetComponent<Board>()).First(b => b != null);
        foreach (string key in new[] { "Electric net token", "Moat token" })
        {
            if (library.templates.Any(e => e.key == key)) continue;
            var source = board.PiecePrefabs[2];
            var root = new GameObject(key); root.transform.SetParent(library.transform, false);
            root.transform.localScale = source.transform.localScale;
            string folder = "Assets/Prefabs/SceneTemplates/ChessScene/BoardEffects";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            int i = 0;
            foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mesh = renderer.GetComponent<MeshFilter>(); if (mesh == null) continue;
                var part = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = source.transform.InverseTransformPoint(renderer.transform.position);
                part.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * renderer.transform.rotation;
                part.transform.localScale = renderer.transform.localScale;
                part.GetComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var material = new Material(renderer.sharedMaterial);
                Color color = key.StartsWith("Electric") ? new Color(.55f,.18f,1f,.8f) : new Color(.15f,.45f,1f,.75f);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                AssetDatabase.CreateAsset(material, folder + "/" + key + (i++) + ".mat");
                part.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            library.templates.Add(new SceneObjectTemplates.Entry { key = key, template = root });
        }
    }

    /// <summary>輸出分類階層供後續尋找可編輯樣板。</summary>
    private static void Describe(Transform node, string indent, List<string> report)
    {
        report.Add(indent + node.name);
        foreach (Transform child in node) Describe(child, indent + "  ", report);
    }
}
