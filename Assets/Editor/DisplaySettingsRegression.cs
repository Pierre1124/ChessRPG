using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;

/// <summary>在預覽場景檢查解析度下拉模板，不切換螢幕、不修改目前場景或使用者設定。</summary>
public static class DisplaySettingsRegression
{
    [MenuItem("Tools/Chess/Run Display Settings Regression")]
    public static void Run()
    {
        var results = new List<string>();
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            results.Add("PASS: " + label);
        }
        try
        {
            foreach (string path in new[] { "Assets/Scenes/StartScene.unity", "Assets/Scenes/ChessScene.unity" })
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    TMP_Dropdown dropdown = path.Contains("StartScene")
                        ? roots.SelectMany(root => root.GetComponentsInChildren<StartMenuController>(true)).Single().resolutionDropdown
                        : roots.SelectMany(root => root.GetComponentsInChildren<SettingsUI>(true)).Single().resolutionDropdown;
                    Check(dropdown != null && dropdown.template != null, path + " has resolution template");
                    Check(dropdown.template.IsChildOf(dropdown.transform), path + " owns independent template");
                    var item = dropdown.template.GetComponentInChildren<Toggle>(true);
                    Check(item != null && item.transform != dropdown.template, path + " contains option Toggle");
                    Check(dropdown.itemText != null && dropdown.itemText.transform.IsChildOf(item.transform), path + " option text belongs to Toggle");
                    var options = DisplaySettingsController.PopulateResolutionDropdown(dropdown);
                    Check(options.Length > 0 && dropdown.options.Count == options.Length, path + " populates available resolutions");
                    // Invoke the exact TMP validation that previously logged the user's error.
                    typeof(TMP_Dropdown).GetMethod("SetupTemplate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dropdown, null);
                    Check((bool)typeof(TMP_Dropdown).GetField("validTemplate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dropdown),
                        path + " passes TMP runtime template validation");
                    Check(!dropdown.template.gameObject.activeSelf, path + " keeps template hidden until opening");
                    int calls = 0;
                    dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
                    dropdown.onValueChanged.AddListener(_ => calls++);
                    dropdown.SetValueWithoutNotify(0);
                    if (options.Length > 1)
                    {
                        dropdown.value = options.Length - 1;
                        Check(calls == 1 && dropdown.captionText.text == dropdown.options[options.Length - 1].text,
                            path + " selection updates caption and emits one callback");
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }
        catch (Exception error) { results.Add("FAIL: " + error); throw; }
        finally
        {
            Directory.CreateDirectory("output");
            File.WriteAllLines("output/display-settings-regression.txt", results);
        }
        UnityEngine.Debug.Log("Display settings: " + results.Count + " passed");
    }
}
