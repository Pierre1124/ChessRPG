using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CheckAlarmRegression
{
    [MenuItem("Tools/Chess/Presentation/Verify Check And Alarms")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/ChessScene.unity");
        var results = new List<string>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); results.Add("PASS: " + message); }
        try
        {
            var flow = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<GameFlowUI>(true)).First();
            var show = typeof(GameFlowUI).GetMethod("SpawnAlarm", flags);
            var list = (List<RectTransform>)typeof(GameFlowUI).GetField("activeOneShotAlarms", flags).GetValue(flow);
            show.Invoke(flow, new object[] { "最早的警示" });
            var oldest = list[0];
            show.Invoke(flow, new object[] { "第二則警示" });
            show.Invoke(flow, new object[] { "多行警示\n第二行\n第三行" });
            show.Invoke(flow, new object[] { "最新警示" });
            foreach (var row in list)
                if (row.TryGetComponent<AlarmPresentation>(out var motion))
                    typeof(AlarmPresentation).GetMethod("Advance", flags).Invoke(motion, new object[] { 1f });
            Check(list.Count == 3 && (oldest == null || !oldest.gameObject.activeSelf), "Fourth alarm removes oldest immediately; only three remain");
            Check(list[2].GetComponentInChildren<TMP_Text>().text == "最新警示", "Newest message is at bottom of stack");
            var template = ((GameObject)typeof(GameFlowUI).GetField("alarmTemplate", flags).GetValue(flow)).GetComponent<RectTransform>();
            Check(list[2].anchoredPosition == template.anchoredPosition, "Newest alarm preserves template position");
            Check(list.All(r => r.sizeDelta == template.sizeDelta && r.pivot == template.pivot &&
                r.anchorMin == template.anchorMin && r.anchorMax == template.anchorMax),
                "Alarms preserve template dimensions, pivot and anchors");
            for (int i = 0; i < list.Count - 1; i++)
                Check(Mathf.Approximately(list[i].anchoredPosition.y - list[i + 1].anchoredPosition.y, 50f),
                    "Older alarm moves upward by exactly 50 " + i);
            Check(list.All(r => !r.GetComponentInChildren<TMP_Text>().raycastTarget), "Alarms do not block board clicks");
            var timeout = (IEnumerator)typeof(GameFlowUI).GetMethod("FadeAndRemoveAlarm", flags)
                .Invoke(flow, new object[] { list[0].gameObject, list[0] });
            while (timeout.MoveNext())
                if (list[0].TryGetComponent<AlarmPresentation>(out var fading))
                    typeof(AlarmPresentation).GetMethod("Advance", flags).Invoke(fading, new object[] { 1f });
            Check(list.Count == 2, "Expired alarm is removed and remaining rows reflow");
            var testRow = list[1];
            var animation = testRow.GetComponent<AlarmPresentation>() ?? testRow.gameObject.AddComponent<AlarmPresentation>();
            animation.Initialize(.2f, .2f);
            var group = testRow.GetComponent<CanvasGroup>();
            var advance = typeof(AlarmPresentation).GetMethod("Advance", flags);
            Vector2 start = testRow.anchoredPosition;
            animation.MoveTo(start + Vector2.up * 50f);
            Check(group.alpha == 0f, "New alarm starts transparent");
            advance.Invoke(animation, new object[] { .1f });
            Check(group.alpha > 0 && group.alpha < 1 && testRow.anchoredPosition.y > start.y && testRow.anchoredPosition.y < start.y + 50f,
                "Fade-in and upward motion interpolate smoothly");
            advance.Invoke(animation, new object[] { .1f });
            Check(group.alpha == 1f && testRow.anchoredPosition == start + Vector2.up * 50f,
                "Animation ends at exact Y plus 50");
            animation.FadeOut();
            advance.Invoke(animation, new object[] { .1f });
            Check(group.alpha > 0 && group.alpha < 1, "Fade-out interpolates transparency");
            advance.Invoke(animation, new object[] { .1f });
            Check(animation.IsHidden && testRow.sizeDelta == template.sizeDelta && testRow.pivot == template.pivot,
                "Fade-out completes without changing template format");
            var kingObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/White King.prefab"));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(kingObject, scene);
            var king = kingObject.GetComponent<King>();
            KingCheckIndicator.SetVisible(king, true);
            var marker = king.GetComponentInChildren<KingCheckIndicator>(true);
            var line = marker.GetComponent<LineRenderer>();
            Check(line.enabled && marker.gameObject.activeSelf && line.sharedMaterial.GetColor("_BaseColor").r == 1f,
                "Checked king has red base marker");
            king.transform.position += new Vector3(2f, 0, 1f);
            typeof(KingCheckIndicator).GetMethod("LateUpdate", flags).Invoke(marker, null);
            Check(Mathf.Abs(line.GetPosition(0).x - king.transform.position.x - .42f) < .001f,
                "Marker follows king instead of staying on old square");
            KingCheckIndicator.SetVisible(king, false);
            Check(!marker.gameObject.activeSelf, "Resolving check hides marker");
            Check(!File.ReadAllText("Assets/Scripts/LogicManager.cs").Contains("GameFlowUI.SetPersistent"),
                "Check status no longer creates operation alarm");
        }
        catch (Exception error) { results.Add("FAIL: " + error); throw; }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory("output"); File.WriteAllLines("output/check-alarm-regression.txt", results);
        }
        Debug.Log("Check/alarm verification passed: " + results.Count);
    }
}
