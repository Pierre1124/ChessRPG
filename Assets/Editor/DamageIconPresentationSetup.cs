using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>將純圖示提示與固定傷害文字保存至 Scene，供 Inspector 編輯。</summary>
public static class DamageIconPresentationSetup
{
    /// <summary>沿用現有 FinalDMG；只有缺少時建立，保留既有文字排版與材質。</summary>
    [MenuItem("Tools/Chess/Presentation/Setup Icon Damage Steps")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("請先離開 Play Mode。");
        Scene original = SceneManager.GetActiveScene();
        string originalPath = original.path;
        if (original.isDirty) EditorSceneManager.SaveScene(original);
        try
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/ChessScene.unity");
            var visualizer = Object.FindFirstObjectByType<DamageCalculationVisualizer>(FindObjectsInactive.Include);
            var data = new SerializedObject(visualizer);
            TMP_Text label = visualizer.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "FinalDMG");
            if (label == null)
            {
                TMP_Text template = (TMP_Text)data.FindProperty("damageResultFlyText").objectReferenceValue;
                label = Object.Instantiate(template, visualizer.transform, false); label.name = "FinalDMG";
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                label.rectTransform.anchoredPosition = new Vector2(0, 210);
                label.rectTransform.sizeDelta = new Vector2(520, 150);
                label.alignment = TextAlignmentOptions.Center;
                label.text = "最終傷害:0";
            }
            label.raycastTarget = false; label.gameObject.SetActive(true);
            data.FindProperty("finalDamageText").objectReferenceValue = label;
            data.ApplyModifiedPropertiesWithoutUndo();
            var library = Object.FindFirstObjectByType<SceneObjectTemplates>(FindObjectsInactive.Include);
            var panel = library.templates.First(e => e.key == "Damage calculation").template.GetComponent<DamageResolutionPanel>();
            var panelData = new SerializedObject(panel);
            var bubble = (RectTransform)panelData.FindProperty("bubble").objectReferenceValue;
            var icon = (Image)panelData.FindProperty("icon").objectReferenceValue;
            bubble.sizeDelta = new Vector2(64, 64);
            if (bubble.TryGetComponent<Image>(out var background)) background.enabled = false;
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.rectTransform.sizeDelta = new Vector2(64, 64);
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true)) text.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("output");
            File.WriteAllText("output/damage-icon-setup.txt", "PASS saved FinalDMG reference and editable icon-only template\n");
        }
        finally { if (!string.IsNullOrEmpty(originalPath)) EditorSceneManager.OpenScene(originalPath); }
    }
}
