using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class CardIdleEffectsPreviewSetup
{
    private const string ScenePath="Assets/Scenes/Previews/CardIdleFXPreview.unity";
    [MenuItem("Tools/Chess/Presentation/Idle Effects/Create Preview Scene")]
    public static void Create()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode first");
        if(File.Exists(ScenePath))return;
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var demo=new GameObject("Idle FX Preview").AddComponent<CardIdleEffectPreview>();
            demo.owners=new Piece[4];demo.cards=new CardAsset[4];
            var floorMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMat.color=new Color(.025f,.038f,.055f);
            AssetDatabase.CreateAsset(floorMat,CardIdleEffectsSetup.Folder+"/PreviewGround.mat");
            var tileMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));tileMat.color=new Color(.13f,.16f,.2f);
            AssetDatabase.CreateAsset(tileMat,CardIdleEffectsSetup.Folder+"/PreviewTile.mat");
            string[] pieces={"White Queen","Black Queen","White Rook","Black Pawn"};
            string[] labels={"J10  SACRED AEGIS","J09  BLOOD VOW","J04  ION CAGE","E09  VENOM BLOOM"};
            var cols=new[]{new Color(1,.7f,.2f),new Color(1,.2f,.3f),new Color(.7f,.4f,1),new Color(.45f,1,.25f)};
            for(int i=0;i<4;i++)
            {
                float x=(i%2==0?-1.25f:1.25f),z=(i<2?1.2f:-1.2f);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+pieces[i]+".prefab");
                var owner=UnityEngine.Object.Instantiate(prefab);owner.name=labels[i];owner.transform.position=new Vector3(x,.12f,z);
                foreach(var script in owner.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
                foreach(var animator in owner.GetComponentsInChildren<Animator>(true))animator.enabled=false;
                foreach(var canvas in owner.GetComponentsInChildren<Canvas>(true))UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                foreach(var collider in owner.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
                demo.owners[i]=owner.GetComponent<Piece>();demo.cards[i]=AssetDatabase.LoadAssetAtPath<CardAsset>("Assets/Resources/Cards/"+CardIdleEffectsSetup.Ids[i]+".asset");
                var tile=GameObject.CreatePrimitive(PrimitiveType.Cube);tile.name="Preview tile";tile.transform.position=new Vector3(x,0,z);tile.transform.localScale=new Vector3(1.9f,.12f,1.9f);tile.GetComponent<Renderer>().sharedMaterial=tileMat;
                Label(labels[i],new Vector3(x,.075f,z-.95f),.115f,cols[i]);
            }
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Preview ground";ground.transform.position=Vector3.down*.075f;ground.GetComponent<Renderer>().sharedMaterial=floorMat;
            Label("CHESS RPG / IDLE FX",new Vector3(0,0,2.65f),.22f,Color.white);
            Label("UNITY URP / LIVE STATUS PREFABS",new Vector3(0,0,-2.6f),.1f,Color.gray);
            var camera=new GameObject("Idle Preview Camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,7,-8);camera.transform.LookAt(new Vector3(0,.25f,0));camera.orthographic=true;camera.orthographicSize=3.6f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.025f,.04f);camera.tag="MainCamera";
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
            var light=new GameObject("Preview key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(50,-30,0);
            var volume=new GameObject("Preview bloom").AddComponent<Volume>();volume.isGlobal=true;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();var bloom=profile.Add<Bloom>();bloom.intensity.Override(.35f);bloom.threshold.Override(.8f);AssetDatabase.CreateAsset(profile,CardIdleEffectsSetup.Folder+"/PreviewBloom.asset");volume.sharedProfile=profile;
            Directory.CreateDirectory("Assets/Scenes/Previews");EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        }
        finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}
    }

    private static void Label(string text,Vector3 pos,float size,Color color)
    {
        var obj=new GameObject(text);obj.transform.position=pos;obj.transform.rotation=Quaternion.Euler(90,0,0);
        var tm=obj.AddComponent<TextMesh>();tm.text=text;tm.characterSize=size*.12f;tm.fontSize=64;tm.anchor=TextAnchor.MiddleCenter;tm.color=color;
    }

    [MenuItem("Tools/Chess/Presentation/Idle Effects/Update Preview Styling")]
    public static void UpdateStyling()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode first");
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            var mats=new Material[2];
            for(int i=0;i<2;i++)
            {
                string path=CardIdleEffectsSetup.Folder+(i==0?"/PreviewIvory.mat":"/PreviewObsidian.mat");
                mats[i]=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mats[i]==null){mats[i]=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mats[i],path);}
                mats[i].SetColor("_BaseColor",i==0?new Color(.72f,.79f,.85f):new Color(.11f,.14f,.19f));
                mats[i].SetFloat("_Smoothness",.65f);mats[i].SetFloat("_Metallic",.2f);EditorUtility.SetDirty(mats[i]);
            }
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var tm in root.GetComponentsInChildren<TextMesh>())
                    tm.characterSize=(tm.text.StartsWith("CHESS")?.22f:tm.text.StartsWith("UNITY")?.1f:.115f)*.12f;
                var demo=root.GetComponent<CardIdleEffectPreview>();
                if(demo==null)continue;
                for(int i=0;i<demo.owners.Length;i++)
                    foreach(var renderer in demo.owners[i].GetComponentsInChildren<MeshRenderer>())
                    {
                        var slots=renderer.sharedMaterials;
                        for(int j=0;j<slots.Length;j++)slots[j]=mats[i%2];
                        renderer.sharedMaterials=slots;
                    }
            }
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}
    }

    [MenuItem("Tools/Chess/Presentation/Idle Effects/Play Preview")]
    public static void Play()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Already playing");
        var old=EditorSceneManager.playModeStartScene;
        SessionState.SetString("CardIdleOldStartScene",old==null?"":AssetDatabase.GetAssetPath(old));
        SessionState.SetBool("CardIdlePreviewPending",true);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorApplication.isPlaying=true;
    }

    [InitializeOnLoadMethod]
    private static void RestoreHook()
    {
        EditorApplication.playModeStateChanged-=OnPlayState;
        EditorApplication.playModeStateChanged+=OnPlayState;
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CardIdlePreviewPending",false))return;
        SessionState.SetBool("CardIdlePreviewPending",false);
        string old=SessionState.GetString("CardIdleOldStartScene","");
        EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(old)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(old);
    }

    [MenuItem("Tools/Chess/Presentation/Idle Effects/Verify Live Preview")]
    public static void VerifyLive()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Run Play Preview first");
        var demo=UnityEngine.Object.FindFirstObjectByType<CardIdleEffectPreview>();
        if(demo==null)throw new Exception("Preview scene not active");
        var report=new List<string>();
        for(int i=0;i<4;i++)
        {
            var piece=demo.owners[i];var status=piece.Statuses[0];var fx=status.idleEffectInstance;
            if(fx==null || !fx.activeInHierarchy)throw new Exception("Status did not instantiate FX");
            BattleFeedback.StartStatusIdle(status);
            if(status.idleEffectInstance!=fx || piece.GetComponentsInChildren<CardIdleEffectMotion>().Length!=1)throw new Exception("Duplicate FX");
            var before=fx.transform.position;piece.transform.position+=Vector3.right;
            if(Vector3.Distance(fx.transform.position,before+Vector3.right)>.001f)throw new Exception("FX not following owner");
            piece.transform.position-=Vector3.right;
            var motion=fx.GetComponent<CardIdleEffectMotion>();
            if(Mathf.Abs(motion.content.lossyScale.x-motion.worldRadius)>.001f)throw new Exception("Parent scale not compensated");
            piece.RemoveStatusesByCardId(demo.cards[i].definition.id);
            if(fx.activeSelf || status.idleEffectInstance!=null || piece.Statuses.Count!=0)throw new Exception("Removal left active FX");
            piece.ApplyStatus(demo.cards[i].CreateDefinition().statusesToApply[0],piece,true);
            var classicFx=piece.Statuses[0].idleEffectInstance;piece.ClearRpgState();
            if(classicFx.activeSelf || piece.Statuses.Count!=0)throw new Exception("Classic mode cleanup failed");
            report.Add("PASS: "+CardIdleEffectsSetup.Ids[i]+" apply, no duplicates, follow, scale, removal, classic cleanup");
        }
        demo.Show();
        EditorApplication.delayCall+=()=>
        {
            try
            {
                if(demo==null || UnityEngine.Object.FindObjectsByType<CardIdleEffectMotion>(FindObjectsSortMode.None).Length!=4)throw new Exception("Deferred cleanup failed");
                report.Add("PASS: deferred Destroy leaves exactly four live effects");
                Directory.CreateDirectory("output");File.WriteAllLines("output/card-idle-live.txt",report);Debug.Log(string.Join("\n",report));
            }
            catch(Exception e){Debug.LogException(e);}
        };
    }
}
