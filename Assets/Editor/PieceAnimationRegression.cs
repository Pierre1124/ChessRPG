using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PieceAnimationRegression
{
    [MenuItem("Tools/Chess/Presentation/Verify Piece Movement Layers")]
    public static void Run()
    {
        var report = new List<string>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            foreach (string side in new[] { "White", "Black" })
            foreach (string piece in new[] { "Pawn", "Knight", "Bishop", "Rook", "Queen", "King" })
            foreach (int fps in new[] { 30, 60, 120 })
            foreach (int captureFrame in new[] { -2, -1, 1, 8 })
            {
                var scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{side} {piece}.prefab"), scene);
                    var player = obj.GetComponent<PieceAnimationPlayer>();
                    var animator = obj.GetComponent<Animator>();
                    var visual = obj.transform.Find("VisualRoot");
                    var late = typeof(PieceAnimationPlayer).GetMethod("LateUpdate", flags);
                    typeof(PieceAnimationPlayer).GetMethod("Awake", flags).Invoke(player, null);
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Rebind(); animator.Update(0); animator.Update(.1f);
                    float peak = 1f;
                    for (int move = 0; move < 2; move++)
                    {
                        var from = obj.transform.position;
                        obj.transform.position = from + Vector3.forward * 4;
                        if (captureFrame == -1) player.PlayCapture();
                        player.PlayMove(from, obj.transform.position);
                        float previous = from.z;
                        for (int frame = 0; frame < fps; frame++)
                        {
                            if (frame == captureFrame) player.PlayCapture();
                            animator.Update(1f / fps);
                            late.Invoke(player, null);
                            float current = visual.position.z;
                            if (current < previous - .0001f || current - previous > 1f)
                                throw new Exception($"Jump: {side} {piece}, fps={fps}, captureFrame={captureFrame}, frame={frame}, {previous}->{current}");
                            previous = current;
                            peak = Mathf.Max(peak, visual.localScale.x);
                        }
                        if (Mathf.Abs(visual.position.z - obj.transform.position.z) > .001f) throw new Exception("Movement failed to finish");
                        if ((visual.localScale - Vector3.one).sqrMagnitude > .0001f) throw new Exception("Action scale failed to reset");
                    }
                    if (captureFrame != -2 && peak < 1.1f) throw new Exception("Capture action did not play");
                    report.Add($"PASS {side} {piece}, fps={fps}, captureFrame={captureFrame}: two monotonic moves; action peak={peak:F3}; scale restored");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            report.Add("PASS ALL 144 scenarios / 288 moves");
        }
        catch (Exception e) { report.Add("FAIL " + e); Debug.LogException(e); }
        finally { Directory.CreateDirectory("output"); File.WriteAllLines("output/piece-animation-regression.txt", report); }
    }
}
