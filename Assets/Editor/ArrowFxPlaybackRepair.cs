using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ArrowFxPlaybackRepair
{
    const string Folder="Assets/Art/VFX/CardSpells02/";
    const string Report="D:/BlenderPJ/Test/CardIdleFX/ArrowFX/playback-verification.txt";
    static double started;
    static GameObject[] live;
    static float[][] startY;
    static int phase;
    [MenuItem("Tools/Arrow FX/Test Live Battle Playback")]
    public static void TestLive()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play Mode required");
        var piece=UnityEngine.Object.FindObjectsByType<Piece>(FindObjectsSortMode.None).FirstOrDefault();
        var origin=piece?piece.transform.position:Vector3.zero;
        BattleFeedback.PlayType(BattlePresentationType.ModifierUp,origin,false);
        BattleFeedback.PlayType(BattlePresentationType.ModifierDown,origin+Vector3.right*3,false);
        live=new[]{"Rise","Fall"}.Select(k=>UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Select(a=>a.gameObject).Last(g=>g.name=="Arrow_"+k+"_FX(Clone)")).ToArray();
        started=EditorApplication.timeSinceStartup;phase=0;startY=new float[2][];
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    static Transform[] Arrows(GameObject go)=>go.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("_Arrow_")).OrderBy(t=>t.name).ToArray();
    static void Tick()
    {
        try
        {
            double elapsed=EditorApplication.timeSinceStartup-started;
            if(phase==0 && elapsed>.2)
            {
                for(int i=0;i<2;i++)
                {
                    var a=Arrows(live[i]);startY[i]=a.Select(t=>t.position.y).ToArray();
                    if(a.Count(t=>t.localScale.sqrMagnitude>.001f)!=24)throw new Exception("Live arrows not visible");
                }
                phase=1;
            }
            if(phase==1 && elapsed>.6)
            {
                for(int i=0;i<2;i++)
                {
                    int moved=Arrows(live[i]).Where((t,j)=>i==0?t.position.y>startY[i][j]+.1f:t.position.y<startY[i][j]-.1f).Count();
                    if(moved!=24)throw new Exception("Live motion failed: "+moved);
                }
                phase=2;
            }
            if(phase==2 && elapsed>1.25)
            {
                if(live.Any(g=>g))throw new Exception("Live effect did not clean up");
                File.AppendAllText(Report,"\nLIVE BattleFeedback.PlayType: both prefabs spawned, 24/24 arrows visible and moving correctly, destroyed after 1 second. PASS\n");
                EditorApplication.update-=Tick;
            }
        }
        catch(Exception ex){File.AppendAllText(Report,"\nLIVE FAIL: "+ex+"\n");EditorApplication.update-=Tick;Debug.LogException(ex);}
    }
    [MenuItem("Tools/Arrow FX/Verify Animator Playback")]
    public static void Verify()
    {
        var report=new List<string>();
        foreach(var kind in new[]{"Rise","Fall"})
        {
            var scene=EditorSceneManager.NewPreviewScene();
            GameObject obj=null;
            try
            {
                obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Arrow_"+kind+"_FX.prefab"),scene);
                var animator=obj.GetComponent<Animator>();
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();animator.Play("Play",0,0);animator.Update(0);
                var arrows=obj.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("_Arrow_")).ToArray();
                animator.Update(.25f);
                var y=arrows.Select(t=>t.position.y).ToArray();
                int visible=arrows.Count(t=>t.localScale.sqrMagnitude>.01f);
                animator.Update(.45f);
                int moved=arrows.Where((t,i)=>kind=="Rise"?t.position.y>y[i]+.1f:t.position.y<y[i]-.1f).Count();
                report.Add(kind+": Animator visible="+visible+", moving="+moved+"/24, normalized="+animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
                if(moved!=24 || visible!=24) throw new Exception(string.Join("\n",report));
            }
            finally {if(obj) UnityEngine.Object.DestroyImmediate(obj);EditorSceneManager.ClosePreviewScene(scene);File.WriteAllLines(Report,report);}
        }
    }
    [MenuItem("Tools/Arrow FX/Wire Battle Scene")]
    public static void Wire()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before saving scene wiring");
        foreach(var kind in new[]{"Rise","Fall"})
        {
            string path=Folder+"Arrow_"+kind+"_FX.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animator=root.GetComponent<Animator>();
                animator.updateMode=AnimatorUpdateMode.UnscaledTime;
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var feedbacks=UnityEngine.Object.FindObjectsByType<BattleFeedback>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        if(feedbacks.Length!=1)throw new Exception("Expected one BattleFeedback in active scene");
        var feedback=feedbacks[0];Undo.RecordObject(feedback,"Assign arrow effects");
        foreach(var style in feedback.presentationStyles)
        {
            string kind=style.type==BattlePresentationType.ModifierUp?"Rise":style.type==BattlePresentationType.ModifierDown?"Fall":null;
            if(kind==null)continue;
            style.effectPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Arrow_"+kind+"_FX.prefab");
            style.effectOffset=Vector3.zero;style.effectLifetime=1f;
        }
        EditorUtility.SetDirty(feedback);EditorSceneManager.MarkSceneDirty(feedback.gameObject.scene);
        EditorSceneManager.SaveScene(feedback.gameObject.scene);
        Selection.activeGameObject=feedback.gameObject;
        Debug.Log("Arrow effects assigned and saved on "+feedback.gameObject.scene.path);
    }
}
