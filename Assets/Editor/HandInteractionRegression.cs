using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class HandInteractionRegression
{
    [MenuItem("Tools/Chess/Presentation/Verify Hand Interaction")]
    public static void Run()
    {
        var results = new List<string>();
        void Check(bool condition, string text) { if (!condition) throw new Exception(text); results.Add("PASS: " + text); }
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/ChessScene.unity");
        GameObject source = null, cameraObject = null;
        HandCardPreview preview = null;
        RenderTexture render = null;
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        try
        {
            var hand = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<CardHandManager>(true)).First();
            var row = (RectTransform)typeof(CardHandManager).GetField("handRoot", flags).GetValue(hand);
            var toggle = row.GetComponentInParent<HandCardToggle>(true);
            var button = toggle.GetComponentInChildren<Button>(true);
            var tab = (RectTransform)button.transform;
            Check(row.GetComponent<HorizontalLayoutGroup>() != null && tab.parent == row.parent && row.gameObject.activeSelf,
                "Collapsed hand starts with its card row active and the tab outside the layout");
            if (!(bool)typeof(HandCardToggle).GetField("isInitialized", flags).GetValue(toggle))
                typeof(HandCardToggle).GetMethod("Awake", flags).Invoke(toggle, null);
            var panel = (RectTransform)toggle.transform;
            float TabBase() => tab.localPosition.y + tab.rect.yMin;
            float closedTop = TabBase();
            Check(tab.anchorMax == Vector2.one && Mathf.Abs(closedTop - panel.rect.yMax) < .1f,
                "Visible tab is anchored to the collapsed panel's top right");
            button.onClick.Invoke();
            Check(toggle.IsExpanded && row.gameObject.activeSelf, "Pressing the tab expands the panel with cards active");
            typeof(HandCardToggle).GetField("normalizedTime", flags).SetValue(toggle, 1f);
            typeof(HandCardToggle).GetMethod("EvaluateAnimation", flags).Invoke(toggle, null);
            Check(TabBase() > closedTop && Mathf.Abs(TabBase() - panel.rect.yMax) < .1f,
                "Tab moves with the top edge of the expanded panel");
            button.onClick.Invoke();
            Check(!toggle.IsExpanded && row.gameObject.activeSelf && button.gameObject.activeSelf,
                "Collapsing keeps both the card row and the tab active");
            var prefab = (GameObject)typeof(CardHandManager).GetField("cardPrefab", flags).GetValue(hand);
            source = UnityEngine.Object.Instantiate(prefab);
            source.SetActive(true);
            var card = Resources.Load<CardAsset>("Cards/J04").CreateDefinition();
            CardPresentation.ApplyCardData(source, card);
            var handler = source.GetComponent<CardDragHandler>();
            HandCardPreview.Show(handler, false);
            preview = UnityEngine.Object.FindFirstObjectByType<HandCardPreview>();
            Check(preview != null, "Saved hand prefab opens large reading preview");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(preview.gameObject, scene);
            Check(preview.GetComponentsInChildren<CardDragHandler>().All(c => !c.enabled), "Preview cannot drag or play a copied card");
            Check(preview.GetComponentsInChildren<CardUnavailableHint>().All(c => !c.enabled), "Preview does not duplicate hover hints");
            Check(preview.GetComponentsInChildren<CardKeywordText>().All(c => c.enabled), "Preview keeps keyword interactions enabled");
            preview.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            Check((bool)typeof(HandCardPreview).GetField("pinned", flags).GetValue(preview), "Clicking reading preview pins it");
            cameraObject = new GameObject("Hand preview verification camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .12f, .16f);
            camera.cullingMask = 1 << 31;
            render = new RenderTexture(1280, 720, 24); camera.targetTexture = render;
            var canvas = preview.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            foreach (var child in preview.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            Canvas.ForceUpdateCanvases();
            typeof(HandCardPreview).GetMethod("Fit", flags).Invoke(preview, null);
            camera.Render();
            var keyword = preview.GetComponentsInChildren<CardKeywordText>().First().GetComponent<TMP_Text>();
            keyword.ForceMeshUpdate();
            Check(keyword.textInfo.linkCount > 0, "Enlarged card retains formatted glossary links");
            var rectangle = (RectTransform)typeof(HandCardPreview).GetField("cardRect", flags).GetValue(preview);
            var corners = new Vector3[4]; rectangle.GetWorldCorners(corners);
            Check(corners.Select(camera.WorldToViewportPoint).All(p => p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1), "Large card fits within 1280x720 viewport");
            var old = RenderTexture.active; RenderTexture.active = render;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply(); RenderTexture.active = old;
            Directory.CreateDirectory("output"); File.WriteAllBytes("output/hand-reading-preview.png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            var selected = typeof(CardDragHandler).GetField("selected", BindingFlags.Static | BindingFlags.NonPublic);
            selected.SetValue(null, handler);
            Check(CardDragHandler.BlocksBoardInput, "Choosing card target blocks piece movement");
            typeof(CardDragHandler).GetMethod("CancelSelection", flags).Invoke(handler, null);
            Check(!handler.IsSelected && CardDragHandler.BlocksBoardInput, "Cancel clears selection and consumes its input frame");
        }
        catch (Exception error) { results.Add("FAIL: " + error); throw; }
        finally
        {
            if (source != null) { HandCardPreview.Hide(source.GetComponent<CardDragHandler>()); UnityEngine.Object.DestroyImmediate(source); }
            if (preview != null) UnityEngine.Object.DestroyImmediate(preview.gameObject);
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (render != null) { render.Release(); UnityEngine.Object.DestroyImmediate(render); }
            EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory("output"); File.WriteAllLines("output/hand-interaction-regression.txt", results);
        }
        Debug.Log("Hand interaction verification passed: " + results.Count);
    }
}
