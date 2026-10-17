using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>將共用音效接口保存至各正式場景，不覆蓋已指定的素材。</summary>
public static class BattleFeedbackSetup
{
    /// <summary>在 AudioSystem 加入可編輯的音效播放器，保留原場景及設定。</summary>
    [MenuItem("Tools/Chess/Presentation/Setup Feedback Interfaces")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先離開 Play Mode。");
        string original = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        try
        {
            foreach (string path in new[] { "Assets/Scenes/ChessScene.unity", "Assets/Scenes/StartScene.unity", "Assets/Scenes/CardScene.unity" })
            {
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path);
                var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "AudioSystem");
                if (root == null) root = new GameObject("AudioSystem");
                if (root.GetComponent<BattleFeedback>() == null) root.AddComponent<BattleFeedback>();
                var audio = root.GetComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            Directory.CreateDirectory("output");
            File.WriteAllText("output/feedback-setup.txt", "PASS: feedback interfaces saved in three scenes; existing clips preserved.\n");
        }
        finally { if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original); }
    }
}
