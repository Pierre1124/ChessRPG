using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

public static class FieldEffectFeedbackSetup
{
    private const string Path = "Assets/Resources/UI/FieldEffectFeedback.prefab";
    [MenuItem("Tools/Chess/Presentation/Create Field Effect Feedback")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) != null) return;
        var slash = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/slash5-HungNguyen/prefab/slash/white-blue bolder.prefab");
        if (slash == null) throw new InvalidOperationException("找不到 slash 測試特效");
        var root = new GameObject("Field effect feedback", typeof(FieldEffectFeedback));
        try
        {
            var canvasObject = new GameObject("Source card glow", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            var rect = canvasObject.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(288, 384);
            CardGlowGraphic.Create(rect);
            var feedback = root.GetComponent<FieldEffectFeedback>();
            feedback.glowRoot = rect; feedback.targetEffectPrefab = slash;
            PrefabUtility.SaveAsPrefabAsset(root, Path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Chess/Presentation/Verify Field Effect Feedback")]
    public static void Verify()
    {
        var results = new List<string>();
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            results.Add("PASS: " + label);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        Check(prefab != null, "Field feedback prefab is bundled in Resources");
        var feedback = prefab.GetComponent<FieldEffectFeedback>();
        Check(feedback.glowRoot.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace, "Source glow follows board card in world space");
        var glow = feedback.glowRoot.GetComponentInChildren<CardGlowGraphic>();
        Check(glow != null && !glow.raycastTarget, "Field source uses the same non-interactive glow as playable cards");
        Check(feedback.targetEffectPrefab != null && feedback.targetEffectPrefab.GetComponentInChildren<VisualEffect>(true) != null, "Target effect references imported slash VFX");
        Check(feedback.targetEffectPrefab.GetComponentInChildren<CardGlowGraphic>(true) == null, "Target VFX contains no hand-card glow");
        Check(FieldEffectFeedback.Applies(new CardDefinition { cardType = CardType.Field }) &&
            !FieldEffectFeedback.Applies(new CardDefinition { cardType = CardType.JobChange }) &&
            !FieldEffectFeedback.Applies(new CardDefinition { cardType = CardType.Event }), "Routing changes only field cards");
        var copy = UnityEngine.Object.Instantiate(prefab);
        var placeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            placeObject.transform.position = new Vector3(3, 0, 4); placeObject.transform.localScale = new Vector3(2, .2f, 3);
            var place = placeObject.AddComponent<FieldCardPlace>();
            var instance = copy.GetComponent<FieldEffectFeedback>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(FieldEffectFeedback).GetField("source", flags).SetValue(instance, place);
            typeof(FieldEffectFeedback).GetMethod("PlaceGlow", flags).Invoke(instance, null);
            var bounds = place.GetComponent<Collider>().bounds;
            Check(Mathf.Abs(instance.glowRoot.position.y - bounds.max.y - .025f) < .001f &&
                Mathf.Abs(instance.glowRoot.rect.width * instance.glowRoot.lossyScale.x - bounds.size.x) < .001f,
                "Glow fits the source card bounds above its surface");
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); UnityEngine.Object.DestroyImmediate(placeObject); }
        Directory.CreateDirectory("output"); File.WriteAllLines("output/field-effect-feedback.txt", results);
        Debug.Log("Field effect feedback: " + results.Count + " passed");
    }
}
