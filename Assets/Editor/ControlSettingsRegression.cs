using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>在 Play Mode 驗證分類與按鍵偏好，結束後還原玩家設定。</summary>
[InitializeOnLoad]
public static class ControlSettingsRegression
{
    private const string Running = "Chess.ControlSettingsRegression";
    private static int stage;
    private static double deadline;
    /// <summary>重載後接續測試及還原原本的起始場景。</summary>
    static ControlSettingsRegression()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Running, false)) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Running + "Scene", ""));
            SessionState.SetBool(Running, false);
        };
    }
    /// <summary>在隔離的播放場景執行設定操作，不保存場景。</summary>
    [MenuItem("Tools/Chess/Run Control Settings Regression")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先離開 Play Mode");
        SessionState.SetString(Running + "Scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Running, true); stage = 0;
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/ChessScene.unity");
        EditorApplication.isPlaying = true;
    }
    /// <summary>等待初始化，檢查偏好與實際 UI，擷取設定頁後離開播放。</summary>
    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying) return;
        if (stage == 2) { if (EditorApplication.timeSinceStartup > deadline) EditorApplication.isPlaying = false; return; }
        if (stage != 0) return;
        SettingsUI settings = UnityEngine.Object.FindFirstObjectByType<SettingsUI>();
        if (settings == null || settings.panel == null || settings.panel.GetComponent<SettingsPanelView>() == null) return;
        stage = 1;
        var keys = new[] { "Controls.InspectPiece", "Controls.RotateCamera", "Controls.ResetCamera", "Controls.LongPress" };
        bool[] exists = keys.Select(PlayerPrefs.HasKey).ToArray();
        int[] values = keys.Select(key => PlayerPrefs.GetInt(key)).ToArray();
        var lines = new List<string>();
        Action<bool, string> check = (valid, name) => { if (!valid) throw new Exception(name); lines.Add("PASS " + name); };
        try
        {
            ControlBindings.ResetDefaults();
            check(ControlBindings.Get(GameControl.InspectPiece) == (int)Key.I && ControlBindings.Get(GameControl.RotateCamera) == -1,
                "Default inspection and rotation use distinct inputs");
            check(!ControlBindings.LongPressEnabled, "Left-click inspection defaults off");
            check(!ControlBindings.TrySet(GameControl.InspectPiece, -1, out _), "Duplicate binding rejected");
            check(!ControlBindings.TrySet(GameControl.InspectPiece, (int)Key.Escape, out _), "Escape remains reserved");
            check(ControlBindings.TrySet(GameControl.InspectPiece, (int)Key.F, out _) && PlayerPrefs.GetInt(keys[0]) == (int)Key.F,
                "Rebinding writes persistent preference");
            ControlBindings.ResetDefaults();
            settings.ShowPanel();
            check(ControlBindings.SuppressGameplay, "Settings suppress gameplay shortcuts");
            Transform root = settings.panel.transform.Find("Settings content");
            foreach (string name in new[] { "操作", "音效", "顯示", "卡牌外觀", "對局" })
            {
                Button tab = root.GetComponentsInChildren<Button>(true).First(b => b.name == name);
                tab.onClick.Invoke();
                check(root.Cast<Transform>().Count(t => t.name == name && t.GetComponent<Button>() == null && t.gameObject.activeSelf) == 1,
                    "Category is accessible: " + name);
            }
            root.GetComponentsInChildren<Button>(true).First(b => b.name == "操作").onClick.Invoke();
            Directory.CreateDirectory("output");
            ScreenCapture.CaptureScreenshot("output/control-settings.png");
        }
        catch (Exception error) { lines.Add("FAIL " + error); Debug.LogException(error); }
        finally
        {
            for (int i = 0; i < keys.Length; i++)
                if (exists[i]) PlayerPrefs.SetInt(keys[i], values[i]); else PlayerPrefs.DeleteKey(keys[i]);
            PlayerPrefs.Save();
            Directory.CreateDirectory("output"); File.WriteAllLines("output/control-settings-regression.txt", lines);
            stage = 2; deadline = EditorApplication.timeSinceStartup + 1;
        }
    }
}
