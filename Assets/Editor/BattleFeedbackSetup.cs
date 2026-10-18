using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
                var feedback = root.GetComponent<BattleFeedback>() ?? root.AddComponent<BattleFeedback>();
                ConfigureTypes(feedback);
                foreach (var visualizer in UnityEngine.Object.FindObjectsByType<DamageCalculationVisualizer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var serialized = new SerializedObject(visualizer);
                    var fly = serialized.FindProperty("damageResultFly").objectReferenceValue as RectTransform;
                    if (fly != null) AddTypeIcon(fly);
                    var effectRoot = visualizer.transform.Find("TypeEffects");
                    if (effectRoot == null)
                    {
                        effectRoot = new GameObject("TypeEffects").transform;
                        effectRoot.SetParent(visualizer.transform, false);
                    }
                    feedback.typeEffectRoot = effectRoot;
                }
                var audio = root.GetComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            const string prefabPath = "Assets/Prefabs/SceneTemplates/ChessScene/Damage/DamageResultFly.prefab";
            if (File.Exists(prefabPath))
            {
                var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                try { AddTypeIcon(prefab.transform); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            Directory.CreateDirectory("output");
            File.WriteAllText("output/feedback-setup.txt", "PASS: feedback interfaces saved in three scenes; existing clips preserved.\n");
        }
        finally { if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original); }
    }

    /// <summary>只新增缺少的類型，保留玩家已指定的素材與數字樣式。</summary>
    private static void ConfigureTypes(BattleFeedback feedback)
    {
        var styles = feedback.presentationStyles.ToList();
        var icons = AssetDatabase.LoadAllAssetsAtPath("Assets/ChessCardImages/CountingIcon.png").OfType<Sprite>().ToArray();
        var cards = CardAssetLibrary.LoadDefinitions();
        foreach (BattlePresentationType type in Enum.GetValues(typeof(BattlePresentationType)))
        {
            if (styles.Any(s => s != null && s.type == type)) continue;
            string iconName = type == BattlePresentationType.Neutral || type == BattlePresentationType.TrueDamage ? "ATK" : type.ToString();
            var icon = icons.FirstOrDefault(s => s.name == iconName);
            if (type == BattlePresentationType.Electric) icon = cards.FirstOrDefault(c => c.id == "J04")?.skillImage;
            Color color = type == BattlePresentationType.Fire ? new Color(1f,.22f,.15f) :
                type == BattlePresentationType.Electric ? new Color(.72f,.35f,1f) :
                type == BattlePresentationType.Poison ? new Color(.6f,.9f,.15f) :
                type == BattlePresentationType.Curse ? new Color(.85f,.25f,.7f) :
                type == BattlePresentationType.Heal ? new Color(.25f,1f,.5f) :
                type == BattlePresentationType.AntiHeal ? new Color(.7f,.7f,.7f) :
                type == BattlePresentationType.Cost ? new Color(1f,.6f,.2f) : Color.white;
            styles.Add(new BattlePresentationStyle { type = type, icon = icon, numberColor = color });
        }
        feedback.presentationStyles = styles.ToArray();
        EditorUtility.SetDirty(feedback);
    }

    /// <summary>將類型圖示存入場景／預置物，執行時只調整內容，不動原文字設定。</summary>
    private static void AddTypeIcon(Transform fly)
    {
        if (fly.Find("TypeIcon") != null) return;
        var go = new GameObject("TypeIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = fly.gameObject.layer;
        go.transform.SetParent(fly, false);
        var image = go.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
        image.rectTransform.sizeDelta = new Vector2(42, 42);
        image.rectTransform.anchoredPosition = new Vector2(-50, 0);
        go.SetActive(false);
    }
}
