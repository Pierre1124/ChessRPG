using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public static class ArrowFxSetup
{
    const string Folder = "Assets/Art/VFX/CardSpells02";
    [MenuItem("Tools/Arrow FX/Build and Verify")]
    public static void Build()
    {
        var report = new List<string>();
        foreach (var kind in new[] {"Rise", "Fall"})
        {
            string path = Folder + "/Arrow_" + kind + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false;
            importer.clipAnimations = Array.Empty<ModelImporterClipAnimation>();
            importer.SaveAndReimport();
            var definitions = importer.defaultClipAnimations;
            if (definitions.Length != 1) throw new Exception(kind + " expected 1 take, got " + definitions.Length);
            definitions[0].name = "Arrow_" + kind;
            definitions[0].loopTime = false;
            importer.clipAnimations = definitions;
            importer.SaveAndReimport();
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            if (clips.Length != 1 || Mathf.Abs(clips[0].length - 1f) > .001f) throw new Exception(kind + " wrong clip count/duration");
            var clip = clips[0];
            var bindings = AnimationUtility.GetCurveBindings(clip);
            int animatedArrows = bindings.Where(b => b.path.Contains("_Arrow_")).Select(b => b.path).Distinct().Count();
            if (animatedArrows != 24) throw new Exception(kind + " expected 24 animated arrows, got " + animatedArrows);
            string controllerPath = Folder + "/Arrow_" + kind + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm = controller.layers[0].stateMachine;
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Play");
            if (!state) state = sm.AddState("Play");
            state.motion = clip; state.speed = 1; sm.defaultState = state;
            EditorUtility.SetDirty(controller);
            string matPath = Folder + "/Arrow_" + kind + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (!shader) throw new Exception("No supported unlit shader");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, matPath); }
            material.shader = shader;
            Color color = kind == "Rise" ? new Color(1,.035f,.055f,1) : new Color(.025f,.3f,1,1);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor",color);
            if (material.HasProperty("_Color")) material.SetColor("_Color",color);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull",0);
            EditorUtility.SetDirty(material);
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), preview);
                instance.name = "Arrow_" + kind + "_FX";
                instance.transform.position = Vector3.zero;
                var animator = instance.GetComponent<Animator>();
                if (!animator) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterial = material;
                var arrows = instance.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("_Arrow_")).ToArray();
                if (arrows.Length != 24) throw new Exception("Wrong arrow count");
                clip.SampleAnimation(instance,.25f);
                var first = arrows.Select(t => t.position.y).ToArray();
                clip.SampleAnimation(instance,.70f);
                int correct = arrows.Where((t,i) => kind == "Rise" ? t.position.y > first[i]+.1f : t.position.y < first[i]-.1f).Count();
                if (correct != 24) throw new Exception(kind + " movement check failed: " + correct);
                clip.SampleAnimation(instance,1f);
                if (arrows.Any(t => t.localScale.sqrMagnitude > .00001f)) throw new Exception("Arrows not hidden at end");
                clip.SampleAnimation(instance,0f);
                PrefabUtility.SaveAsPrefabAsset(instance,Folder + "/Arrow_" + kind + "_FX.prefab");
                report.Add(kind + ": 1 clip, " + clip.length + " sec, 24 animated arrows, all 24 move " + (kind == "Rise" ? "up" : "down") + ", hidden at end, controller + material + prefab saved.");
            }
            finally
            {
                if (instance) UnityEngine.Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
        AssetDatabase.SaveAssets();
        File.WriteAllLines("D:/BlenderPJ/Test/CardIdleFX/ArrowFX/unity-verification.txt", report);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Arrow_Rise_FX.prefab");
        EditorGUIUtility.PingObject(Selection.activeObject);
        Debug.Log(string.Join("\n",report));
    }
}
