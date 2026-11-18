using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>獨立的進階場地外觀樣本；不改動正式場景或卡片規則。</summary>
public static class AdvancedFieldRoomVariants
{
    const string Output = "Assets/Room/AdvancedFieldVariants";
    static readonly string[] Ids = { "F02", "F04", "F06", "F08", "F10", "F11", "F12" };
    static readonly string[] Names = { "LavaPlateau", "EternalMarsh", "LightEmpire", "DarkEmpire", "GildedSin", "InfernalMarsh", "FatesEnd" };
    static readonly Color[] Tints = {
        new Color(.36f,.19f,.15f), new Color(.24f,.37f,.27f), new Color(.95f,.86f,.65f),
        new Color(.3f,.22f,.43f), new Color(.65f,.44f,.2f), new Color(.38f,.3f,.19f), new Color(.42f,.47f,.62f)
    };
    static readonly Color[] Accents = {
        new Color(1f,.19f,.025f), new Color(.28f,1f,.53f), new Color(1f,.78f,.3f),
        new Color(.64f,.19f,1f), new Color(1f,.55f,.08f), new Color(1f,.35f,.08f), new Color(.3f,.75f,1f)
    };

    [MenuItem("Tools/Chess/Presentation/Build Advanced Field Room Variants")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var prior = SceneManager.GetActiveScene();
        var sourceScene = SceneManager.GetSceneByPath("Assets/Scenes/ChessScene.unity");
        bool openedSource = !sourceScene.isLoaded;
        if (openedSource) sourceScene = EditorSceneManager.OpenScene("Assets/Scenes/ChessScene.unity", OpenSceneMode.Additive);
        var source = sourceScene.GetRootGameObjects().Single(g => g.name == "SceneRoot");
        var sourceCamera = sourceScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory("output/advanced-field-rooms");
        var report = new List<string> { "Source: ChessScene/SceneRoot (current loaded hierarchy). Original scene/materials are not saved or modified." };
        foreach (var m in source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct())
            report.Add("SOURCE MATERIAL: " + AssetDatabase.GetAssetPath(m) + " | " + m.shader.name);
        try
        {
            for (int i = 0; i < Ids.Length; i++)
            {
                string folder = Output + "/" + Ids[i] + "_" + Names[i];
                // Do not overwrite artist edits when the generator is run again.
                if (Directory.Exists(folder)) { report.Add("SKIPPED existing " + folder); continue; }
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                try
                {
                    var room = Object.Instantiate(source);
                    room.name = "SceneRoot_" + Ids[i] + "_" + Names[i];
                    var materials = new Dictionary<Material, Material>();
                    foreach (var renderer in room.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.lightmapIndex = -1;
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                        {
                            if (!original) return original;
                            if (materials.TryGetValue(original, out var known)) return known;
                            var copy = new Material(original) { name = original.name + "_" + Ids[i] };
                            // Keep the source UVs, maps, normal detail and tiling intact.
                            string colorProperty = copy.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                            if (copy.HasProperty(colorProperty)) copy.SetColor(colorProperty, Color.Lerp(copy.GetColor(colorProperty), Tints[i], .65f));
                            if (copy.HasProperty("_EmissionColor") && copy.IsKeywordEnabled("_EMISSION")) copy.SetColor("_EmissionColor", Accents[i] * 1.8f);
                            AssetDatabase.CreateAsset(copy, folder + "/Material_" + materials.Count.ToString("00") + ".mat");
                            materials.Add(original, copy);
                            return copy;
                        }).ToArray();
                    }
                    foreach (var light in room.GetComponentsInChildren<Light>(true))
                    {
                        light.color = Color.Lerp(Color.white, Accents[i], .55f);
                        light.lightmapBakeType = LightmapBakeType.Realtime;
                    }
                    AddInlays(room, i, folder);
                    string prefabPath = folder + "/" + room.name + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(room, prefabPath);
                    var cam = new GameObject("Preview Camera").AddComponent<Camera>();
                    cam.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
                    cam.orthographic = sourceCamera.orthographic;
                    cam.orthographicSize = sourceCamera.orthographicSize * 1.35f;
                    cam.fieldOfView = Mathf.Min(85, sourceCamera.fieldOfView * 1.25f);
                    cam.nearClipPlane = .1f; cam.farClipPlane = 200;
                    cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Tints[i] * .12f;
                    cam.tag = "MainCamera";
                    var key = new GameObject("Preview Fill Light").AddComponent<Light>();
                    key.type = LightType.Directional; key.intensity = 1.1f;
                    key.color = Color.Lerp(Color.white, Accents[i], .15f);
                    key.transform.rotation = Quaternion.Euler(55, -25, 0);
                    EditorSceneManager.SaveScene(scene, folder + "/" + Ids[i] + "_RoomPreview.unity");
                    // Render far from the user's open gameplay scene without changing it.
                    foreach (var root in scene.GetRootGameObjects()) root.transform.position += Vector3.right * 1000;
                    Capture(cam, "output/advanced-field-rooms/" + Ids[i] + ".png");
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    int missing = prefab.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    if (missing != 0) throw new InvalidOperationException(prefabPath + " has missing scripts");
                    if (prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Any(m => !m || m.shader.name == "Hidden/InternalErrorShader"))
                        throw new InvalidOperationException(prefabPath + " has invalid materials");
                    report.Add("PASS " + Ids[i] + ": " + materials.Count + " independent materials; " + prefab.GetComponentsInChildren<Renderer>(true).Length + " renderers; no missing scripts/materials. " + prefabPath);
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllLines("output/advanced-field-rooms/verification.txt", report);
            Debug.Log("Advanced field room variants complete: " + Output);
        }
        finally
        {
            if (openedSource) EditorSceneManager.CloseScene(sourceScene, true);
            SceneManager.SetActiveScene(prior);
        }
    }

    static void AddInlays(GameObject room, int theme, string folder)
    {
        var floor = room.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "ground");
        if (!floor) return;
        Bounds b = floor.bounds;
        var group = new GameObject("Field Accent Inlays");
        group.transform.SetParent(room.transform, true);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = Ids[theme] + "_Accent";
        mat.SetColor("_BaseColor", Accents[theme]);
        mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Accents[theme] * 2);
        AssetDatabase.CreateAsset(mat, folder + "/Accent.mat");
        for (int n = 0; n < 4; n++)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Perimeter Inlay " + (n + 1);
            Object.DestroyImmediate(strip.GetComponent<Collider>());
            strip.transform.position = new Vector3(b.center.x + (n < 2 ? (n == 0 ? -1 : 1) * b.extents.x * .82f : 0), b.max.y + .025f,
                b.center.z + (n >= 2 ? (n == 2 ? -1 : 1) * b.extents.z * .82f : 0));
            strip.transform.localScale = n < 2 ? new Vector3(.055f,.025f,b.size.z*.82f) : new Vector3(b.size.x*.82f,.025f,.055f);
            strip.transform.SetParent(group.transform, true);
            strip.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    static void Capture(Camera camera, string path)
    {
        var rt = new RenderTexture(960, 640, 24);
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            rt.Release(); Object.DestroyImmediate(rt);
            if (image) Object.DestroyImmediate(image);
        }
    }
}
