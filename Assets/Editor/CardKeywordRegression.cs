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

public static class CardKeywordRegression
{
    public static void RunManagedChecks(Action<bool, string> check)
    {
        var formatter = new CardKeywordFormatter(new[] {
            new CardKeywordEntry { id = "token", term = "指示物", aliases = new[] { "Token" }, description = "特殊棋子" },
            new CardKeywordEntry { id = "net", term = "電網Token", description = "電網詳細規則" },
            new CardKeywordEntry { id = "last", term = "遺言", description = "被吃子時發動" }
        });
        string marked = formatter.Format("電網Token；遺言：生成Token");
        check(marked == "<link=\"kw:net\"><b><u>電網Token</u></b></link>；<link=\"kw:last\"><b><u>遺言</u></b></link>：生成<link=\"kw:token\"><b><u>Token</u></b></link>", "Keywords prefer complete terms and support aliases");
        check(formatter.Format(marked) == marked, "Keyword formatting is idempotent");
        check(formatter.Format("<color=red>遺言</color>") == "<color=red><link=\"kw:last\"><b><u>遺言</u></b></link></color>", "Keywords preserve rich text styling");
        check(formatter.Format("<link=\"other\">遺言</link>") == "<link=\"other\">遺言</link>", "Keywords do not nest existing links");
        check(formatter.Format("<noparse>遺言</noparse>") == "<noparse>遺言</noparse>", "Keywords respect noparse text");
        check(formatter.Format("Tokenizer MyToken Token2") == "Tokenizer MyToken Token2", "English aliases do not match inside identifiers");
        check(formatter.Format(null) == "" && formatter.Format("普通文字") == "普通文字", "Empty and non-keyword descriptions remain intact");
        check(formatter.TryGet("kw:net", out var entry) && entry.description == "電網詳細規則", "Link resolves its detailed definition");
        check(!formatter.TryGet("other:net", out _) && !formatter.TryGet("kw:missing", out _), "Unknown links cannot open glossary entries");
        var card = new CardDefinition { cardType = CardType.JobChange, description = "完整規則", shortDescription = "簡述" };
        check(card.DisplayDescription == "簡述" && card.description == "完整規則", "Job card uses summary without replacing full rules");
        card.shortDescription = " ";
        check(card.DisplayDescription == "完整規則", "Empty summary falls back to full description");
        card.cardType = CardType.Event; card.shortDescription = "簡述";
        check(card.DisplayDescription == "完整規則", "Other card types retain their original descriptions");
    }

    [MenuItem("Tools/Chess/Keywords/Run Regression")]
    public static void Run()
    {
        var results = new List<string>();
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            results.Add("PASS: " + label);
        }
        var root = new GameObject("Keyword test", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        try
        {
            RunManagedChecks(Check);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root.transform, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset; text.fontSize = 28;
            text.rectTransform.sizeDelta = new Vector2(700, 200);
            CardKeywordText.Bind(text, "遺言：普通文字。電網Token，攻擊力+2。");
            Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate();
            Check(text.textInfo.linkCount == 3, "Real TMP creates three glossary links");
            var first = text.textInfo.linkInfo[0];
            Check(first.GetLinkText() == "遺言", "TMP link covers exactly the term");
            var character = text.textInfo.characterInfo[first.linkTextfirstCharacterIndex];
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, text.transform.TransformPoint((character.bottomLeft + character.topRight) / 2));
            var handler = text.GetComponent<CardKeywordText>();
            Check(handler.IsRaycastLocationValid(screen, null), "Keyword glyph accepts clicks");
            var ordinary = text.textInfo.characterInfo[3];
            Vector2 outside = RectTransformUtility.WorldToScreenPoint(null, text.transform.TransformPoint((ordinary.bottomLeft + ordinary.topRight) / 2));
            Check(!handler.IsRaycastLocationValid(outside, null), "Ordinary text leaves parent card interactions available");
            Check(ExecuteEvents.GetEventHandler<IDragHandler>(textObject) == textObject &&
                ExecuteEvents.GetEventHandler<IPointerDownHandler>(textObject) == textObject, "Keyword owns click and drag gestures instead of parent card");
            handler.OnPointerClick(new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left });
            var opened = UnityEngine.Object.FindFirstObjectByType<CardKeywordPopup>();
            try
            {
                Check(CardKeywordPopup.IsOpen && opened != null && opened.titleText.text == "遺言" && opened.detailText.text.Contains("被吃子"),
                    "Clicking actual TMP link opens the correct definition");
                opened.closeButton.onClick.Invoke();
                Check(!CardKeywordPopup.IsOpen, "Close button dismisses glossary and releases modal state");
            }
            finally { if (opened != null) UnityEngine.Object.DestroyImmediate(opened.gameObject); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardKeywordSetup.PrefabPath);
            Check(prefab != null, "Editable popup prefab is included in Resources");
            var popupObject = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var popup = popupObject.GetComponent<CardKeywordPopup>();
                Check(popup.titleText != null && popup.detailText != null && popup.closeButton != null && popup.backdropButton != null, "Popup references are complete");
                popup.titleText.text = "電網指示物";
                popup.detailText.text = "由電網召喚的指示物。\n\n無法被操作或作為目標。棋子通過時，對其造成 1 點電屬性傷害。\n\n當來源卡片被破壞或關閉電網時消失。";
                Capture(popup);
                popup.detailText.text = string.Join("\n", new string[50]).Replace("\n", "長規則說明\n");
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(popup.scroll.content);
                Check(popup.scroll.content.rect.height > popup.scroll.viewport.rect.height && popup.scroll.vertical && !popup.scroll.horizontal,
                    "Long definitions expand inside a vertical scroll viewport");
            }
            finally { UnityEngine.Object.DestroyImmediate(popupObject); }
            textObject.SetActive(false);
            VerifyActualCards(root, Check);
        }
        catch (Exception error) { results.Add("FAIL: " + error); throw; }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            Directory.CreateDirectory("output"); File.WriteAllLines("output/card-keyword-regression.txt", results);
        }
        Debug.Log("Card keywords: " + results.Count + " passed");
    }

    private static void VerifyActualCards(GameObject root, Action<bool, string> check)
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/ChessScene.unity");
        var cameraObject = new GameObject("Card raycast test camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .15f, .18f);
        camera.cullingMask = 1 << 31;
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
        root.GetComponent<Canvas>().worldCamera = camera;
        root.GetComponent<Canvas>().planeDistance = 1;
        try
        {
            var objects = scene.GetRootGameObjects();
            var palette = objects.SelectMany(o => o.GetComponentsInChildren<BattleFeedback>(true)).First().presentationStyles;
            foreach (var pair in new[] { ("fire", BattlePresentationType.Fire), ("electric", BattlePresentationType.Electric),
                ("poison", BattlePresentationType.Poison), ("curse", BattlePresentationType.Curse),
                ("normal", BattlePresentationType.Neutral), ("true", BattlePresentationType.TrueDamage), ("cost", BattlePresentationType.Cost) })
            {
                CardKeywordGlossary.Formatter.TryGet("kw:" + pair.Item1, out var entry);
                string color = ColorUtility.ToHtmlStringRGB(palette.First(style => style.type == pair.Item2).numberColor);
                string formatted = CardKeywordGlossary.Formatter.Format(entry.term);
                check(formatted.Contains("<b><color=#" + color + "><u>"), entry.term + " is bold and matches saved damage color");
            }
            var hand = objects.SelectMany(o => o.GetComponentsInChildren<CardHandManager>(true)).First();
            var info = objects.SelectMany(o => o.GetComponentsInChildren<CardInfoUI>(true)).First();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var sources = new[] {
                (GameObject)typeof(CardHandManager).GetField("cardPrefab", flags).GetValue(hand),
                (GameObject)typeof(CardInfoUI).GetField("cardPreviewPrefab", flags).GetValue(info)
            };
            for (int i = 0; i < sources.Length; i++)
            {
                var clone = UnityEngine.Object.Instantiate(sources[i], root.transform);
                try
                {
                    clone.SetActive(true);
                    foreach (var child in clone.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    var rect = clone.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one;
                    var card = Resources.Load<CardAsset>("Cards/J04").CreateDefinition();
                    CardPresentation.ApplyCardData(clone, card);
                    if (i == 1) typeof(CardInfoUI).GetMethod("DisablePreviewInteraction", flags).Invoke(info, new object[] { clone });
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    var label = clone.GetComponentsInChildren<CardKeywordText>(true).Single().GetComponent<TMP_Text>();
                    label.ForceMeshUpdate();
                    check(label.textInfo.linkCount > 0 && label.text.Contains("開啟時無法移動"), "Saved card " + i + " displays summary links");
                    var link = label.textInfo.linkInfo[0];
                    var ch = label.textInfo.characterInfo[link.linkTextfirstCharacterIndex];
                    var screen = RectTransformUtility.WorldToScreenPoint(camera, label.transform.TransformPoint((ch.bottomLeft + ch.topRight) / 2));
                    var pointer = new PointerEventData(EventSystem.current) { position = screen };
                    List<RaycastResult> Hits()
                    {
                        var hits = new List<RaycastResult>();
                        foreach (var raycaster in clone.GetComponentsInChildren<GraphicRaycaster>()) raycaster.Raycast(pointer, hits);
                        return hits.OrderByDescending(hit => hit.depth).ToList();
                    }
                    var results = Hits();
                    check(results.Count > 0 && results[0].gameObject == label.gameObject, "Saved card " + i + " routes actual UI raycast to underlined text; hits=" + string.Join(",", results.Select(hit => hit.gameObject.name + ":" + hit.depth)) +
                        " screen=" + screen + " depth=" + label.depth + " rect=" + label.rectTransform.rect + " canvas=" + label.canvas.renderMode + " camera=" + label.canvas.GetComponent<GraphicRaycaster>().eventCamera);
                    pointer.pointerPressRaycast = results[0];
                    ExecuteEvents.ExecuteHierarchy(results[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    var opened = UnityEngine.Object.FindFirstObjectByType<CardKeywordPopup>();
                    try
                    {
                        check(CardKeywordPopup.IsOpen && opened != null && opened.titleText.text == "電網Token", "Saved card " + i + " raycast click opens token definition");
                        opened.closeButton.onClick.Invoke();
                    }
                    finally { if (opened != null) UnityEngine.Object.DestroyImmediate(opened.gameObject); }
                    var nested = label.GetComponentInParent<Canvas>().GetComponent<GraphicRaycaster>();
                    UnityEngine.Object.DestroyImmediate(nested);
                    check(!Hits().Any(hit => hit.gameObject == label.gameObject), "Saved card " + i + " reproduces missing nested raycaster failure");
                    CardKeywordText.Bind(label, card.DisplayDescription);
                    check(Hits().Any(hit => hit.gameObject == label.gameObject), "Saved card " + i + " binding repairs nested raycaster");
                    check(label.fontSharedMaterial.GetColor("_FaceColor") == Color.white,
                        "Saved card " + i + " material preserves keyword colors instead of multiplying them by black");
                    foreach (string id in new[] { "E05", "E09" })
                    {
                        var statusCard = Resources.Load<CardAsset>("Cards/" + id).CreateDefinition();
                        CardKeywordText.Bind(label, statusCard.DisplayDescription);
                        label.ForceMeshUpdate();
                        string keywordId = id == "E05" ? "curse-status" : "poison-status";
                        CardKeywordGlossary.Formatter.TryGet("kw:" + keywordId, out var definition);
                        var statusLink = label.textInfo.linkInfo.Take(label.textInfo.linkCount).Single(linkInfo => linkInfo.GetLinkID() == "kw:" + keywordId);
                        var glyph = label.textInfo.characterInfo[statusLink.linkTextfirstCharacterIndex];
                        ColorUtility.TryParseHtmlString("#" + definition.fallbackColor, out Color expected);
                        var vertices = label.textInfo.meshInfo[glyph.materialReferenceIndex].colors32;
                        check(vertices[glyph.vertexIndex].Equals((Color32)expected), id + " actual glyph mesh has its status damage color");
                        check(definition.description.Contains(id == "E05" ? "轉移" : "移動後"), id + " status link contains derived mechanics");
                        if (i == 0)
                        {
                            camera.Render();
                            var previous = RenderTexture.active;
                            var screenshot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                            try
                            {
                                RenderTexture.active = target;
                                screenshot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                                screenshot.Apply();
                                File.WriteAllBytes("output/card-keyword-" + id + ".png", screenshot.EncodeToPNG());
                            }
                            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(screenshot); }
                        }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
            }
            var tokenObject = new GameObject("Token info test", typeof(BoardFieldTokenInfo));
            try
            {
                var token = tokenObject.GetComponent<BoardFieldTokenInfo>();
                token.type = BoardFieldEffectType.Moat;
                check(token.Entry.description.Contains("阻攔"), "Moat token carries its derived rules");
                token.type = BoardFieldEffectType.ElectricNet;
                check(token.Entry.description.Contains("1 點電屬性傷害"), "Electric token carries its derived rules");
                check(token.GetComponent<Piece>() == null && token.GetComponent<Collider>() == null, "Inspecting token does not turn it into a gameplay target");
            }
            finally { UnityEngine.Object.DestroyImmediate(tokenObject); }
        }
        finally
        {
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void Capture(CardKeywordPopup popup)
    {
        var cameraObject = new GameObject("Keyword preview camera", typeof(Camera));
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            var camera = cameraObject.GetComponent<Camera>();
            camera.targetTexture = target; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.15f, .19f, .23f); camera.cullingMask = 1 << 31;
            foreach (var transform in popup.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
            var canvas = popup.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            Directory.CreateDirectory("output"); File.WriteAllBytes("output/card-keyword-popup.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            popup.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
