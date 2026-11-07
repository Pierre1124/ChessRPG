using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class JobIdlePreviewSetup
{
    const string ScenePath="Assets/Scenes/Previews/JobIdleFXPreview.unity";
    static readonly string[] Labels={"SPEAR WATCH","SHIELD GUARD","MOAT RIPPLE","ION CAGE","VANGUARD","PALADIN LIGHT","ARCANE ORBIT","CLERIC PRAYER","BLOOD VOW","SACRED AEGIS","SOVEREIGN","TYRANT"};
    [MenuItem("Tools/Chess/Presentation/Job Idle/Create Preview")]
    public static void Create()
    {
        var prior=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var tileMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/CardIdle/PreviewTile.mat");
            var ivory=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/CardIdle/PreviewIvory.mat");
            for(int i=0;i<12;i++)
            {
                string id="J"+(i+1).ToString("00");var card=AssetDatabase.LoadAssetAtPath<CardAsset>("Assets/Resources/Cards/"+id+".asset");
                Vector3 pos=new Vector3((i%4-1.5f)*1.7f,0,(1-i/4)*2f);
                var owner=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/White Pawn.prefab"));
                owner.name=id+" "+card.definition.cardName;owner.transform.position=pos;
                foreach(var script in owner.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
                foreach(var c in owner.GetComponentsInChildren<Canvas>(true))UnityEngine.Object.DestroyImmediate(c.gameObject);
                foreach(var c in owner.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
                foreach(var a in owner.GetComponentsInChildren<Animator>(true))a.enabled=false;
                foreach(var r in owner.GetComponentsInChildren<MeshRenderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>ivory).ToArray();
                var effect=UnityEngine.Object.Instantiate(card.definition.statusesToApply[0].idleEffectPrefab,owner.transform,false);
                var motion=effect.GetComponent<CardIdleEffectMotion>();motion.FitToPiece();motion.Evaluate(.7f);
                var tile=GameObject.CreatePrimitive(PrimitiveType.Cylinder);tile.name=id+" plinth";tile.transform.position=pos+Vector3.down*.08f;tile.transform.localScale=new Vector3(1.3f,.07f,1.3f);tile.GetComponent<Renderer>().sharedMaterial=tileMat;UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>());
                Label(id+" / "+Labels[i],pos+new Vector3(0,-.01f,-.86f),.15f);
            }
            Label("JOB CLASS / IDLE AURAS",new Vector3(0,0,4.1f),.28f);
            var camera=new GameObject("Job Idle Preview Camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(0,8,-10);camera.transform.LookAt(new Vector3(0,.15f,0));camera.orthographic=true;camera.orthographicSize=4;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.012f,.019f,.035f);
            var light=new GameObject("Preview key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.7f;light.transform.rotation=Quaternion.Euler(50,-30,0);
            // Keep the additive showcase outside the gameplay scene's render volume.
            foreach(var root in scene.GetRootGameObjects())root.transform.position+=Vector3.right*1000;
            camera.farClipPlane=30;
            EditorSceneManager.SaveScene(scene,ScenePath);
            var rt=new RenderTexture(1400,1100,24);var before=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                File.WriteAllBytes("D:/BlenderPJ/Test/CardIdleFX/job-idle-preview.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{camera.targetTexture=null;RenderTexture.active=before;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(prior);}
    }
    static void Label(string text,Vector3 position,float size)
    {
        var go=new GameObject(text);go.transform.position=position;go.transform.rotation=Quaternion.Euler(90,0,0);
        var t=go.AddComponent<TextMesh>();t.text=text;t.fontSize=64;t.characterSize=size*.12f;t.anchor=TextAnchor.MiddleCenter;t.color=new Color(.7f,.8f,.9f);
    }
    static GameObject[] owners;
    static StatusRuntime[] statuses;
    static double started;
    static int phase;
    [MenuItem("Tools/Chess/Presentation/Job Idle/Test Runtime Lifecycle")]
    public static void TestLive()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first");
        owners=new GameObject[12];statuses=new StatusRuntime[12];
        for(int i=0;i<12;i++)
        {
            owners[i]=new GameObject("JobIdleLifecycleTest_"+i);owners[i].transform.position=new Vector3(100+i*2,0,100);
            var piece=owners[i].AddComponent<Pawn>();piece.enabled=false;
            var card=AssetDatabase.LoadAssetAtPath<CardAsset>("Assets/Resources/Cards/J"+(i+1).ToString("00")+".asset");
            var def=card.CreateDefinition().statusesToApply[0];statuses[i]=new StatusRuntime(def,piece,piece,true);
            BattleFeedback.StartStatusIdle(statuses[i]);
            if(!statuses[i].idleEffectInstance)throw new Exception("Status idle did not spawn for "+card.definition.id);
        }
        started=EditorApplication.timeSinceStartup;phase=0;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        try
        {
            if(phase==0 && EditorApplication.timeSinceStartup-started>4.3)
            {
                foreach(var status in statuses)
                {
                    if(!status.idleEffectInstance || !status.idleEffectInstance.activeInHierarchy)throw new Exception("Effect stopped before full loop");
                    BattleFeedback.StopStatusIdle(status);
                    if(status.idleEffectInstance!=null)throw new Exception("Effect reference not cleared");
                }
                phase=1;
            }
            else if(phase==1)
            {
                if(owners.Any(o=>o.GetComponentInChildren<CardIdleEffectMotion>(true)))throw new Exception("Effect not destroyed");
                File.AppendAllText("D:/BlenderPJ/Test/CardIdleFX/job-idle-verification.txt","\nLIVE: 12/12 status effects spawned, persisted beyond 4 seconds, and removed via StopStatusIdle. PASS\n");
                Cleanup();
            }
        }
        catch(Exception ex){Debug.LogException(ex);Cleanup();}
    }
    static void Cleanup(){EditorApplication.update-=Tick;foreach(var o in owners)if(o)UnityEngine.Object.Destroy(o);}
}
