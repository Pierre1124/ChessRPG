using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class CardFXVolumeTwoSetup
{
    public const string Folder="Assets/Art/VFX/CardSpells02";
    public const string Preview="Assets/Scenes/Previews/CardSpells02Preview.unity";
    private static readonly string[] Ids={"E04","E02","E10","E05"};
    [Serializable] public class Recipe{public Effect[] effects;}
    [Serializable] public class Effect{public string name;public Color color;public Line[] lines;public bool loop;}
    [Serializable] public class Line{public string name;public Vector3[] points;public float width;public bool closed;public float spin;public float bob;}
    private static Recipe Read()=>JsonUtility.FromJson<Recipe>(File.ReadAllText(Folder+"/CardFXVolume02.json"));

    [MenuItem("Tools/Chess/Presentation/Spell Effects 02/Create and Assign")]
    public static void Create()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode");
        var recipe=Read();var mat=AssetDatabase.LoadAssetAtPath<Material>(CardIdleEffectsSetup.Folder+"/IdleLines.mat");
        if(mat==null)throw new Exception("Install Volume 01 material first");
        for(int i=0;i<4;i++)
        {
            var data=recipe.effects[i];string path=Folder+"/"+data.name+".prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)
            {
                var root=new GameObject(data.name);
                try
                {
                    var motion=root.AddComponent<CardIdleEffectMotion>();motion.loopSeconds=data.loop?4:1.5f;
                    var content=new GameObject("Editable outlines").transform;content.SetParent(root.transform,false);motion.content=content;
                    var entries=new List<CardIdleEffectMotion.Element>();
                    foreach(var line in data.lines)
                    {
                        var go=new GameObject(line.name);go.transform.SetParent(content,false);var lr=go.AddComponent<LineRenderer>();
                        var points=(Vector3[])line.points.Clone();for(int p=0;p<points.Length;p++)points[p].y/=1.6f;
                        lr.useWorldSpace=false;lr.positionCount=points.Length;lr.SetPositions(points);lr.loop=line.closed;
                        lr.widthMultiplier=line.width;lr.startColor=lr.endColor=data.color;lr.sharedMaterial=mat;
                        lr.numCapVertices=2;lr.numCornerVertices=2;lr.shadowCastingMode=ShadowCastingMode.Off;lr.receiveShadows=false;
                        entries.Add(new CardIdleEffectMotion.Element{target=go.transform,turnsPerLoop=line.spin,bob=line.bob/1.6f});
                    }
                    motion.elements=entries.ToArray();
                    if(!data.loop)root.AddComponent<CardSpellPulse>();
                    prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            var card=AssetDatabase.LoadAssetAtPath<CardAsset>("Assets/Resources/Cards/"+Ids[i]+".asset");
            Undo.RecordObject(card,"Assign spell VFX");
            if(data.loop)
            {
                var s=card.definition.statusesToApply[0];
                if(s.idleEffectPrefab==null)s.idleEffectPrefab=prefab;
                else if(s.idleEffectPrefab!=prefab)throw new Exception("Existing idle VFX retained: "+Ids[i]);
            }
            else if(!card.definition.animations.Exists(a=>a.effectPrefab==prefab))
                card.definition.animations.Add(new CardAnimationData{timing=i==0?CardAnimationTiming.OnDamageDealt:CardAnimationTiming.OnHealed,recipient=CardAnimationRecipient.Both,effectPrefab=prefab,effectScale=Vector3.one,attachEffectToReceiver=true,effectLifetime=1.6f});
            EditorUtility.SetDirty(card);
        }
        AssetDatabase.SaveAssets();CreatePreview();RegisterActive();Verify();
    }

    private static void RegisterTemplates(Scene scene)
    {
        SceneObjectTemplates library=null;
        foreach(var root in scene.GetRootGameObjects())
        {
            library=root.GetComponentInChildren<SceneObjectTemplates>(true);
            if(library!=null)break;
        }
        if(library==null)
        {
            var root=new GameObject("Spell Effect Templates");SceneManager.MoveGameObjectToScene(root,scene);
            root.SetActive(false);library=root.AddComponent<SceneObjectTemplates>();
            Undo.RegisterCreatedObjectUndo(root,"Add spell templates");
        }
        Undo.RecordObject(library,"Register spell templates");
        foreach(var name in new[]{"EmberBurst","BlessingStar"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+".prefab");
            if(library.templates.Exists(e=>e.sourcePrefab==prefab))continue;
            var holder=new GameObject(name+" template holder");holder.transform.SetParent(library.transform,false);holder.SetActive(false);
            Undo.RegisterCreatedObjectUndo(holder,"Add spell template");
            var template=(GameObject)PrefabUtility.InstantiatePrefab(prefab,holder.transform);
            library.templates.Add(new SceneObjectTemplates.Entry{key="Card effect - "+name,sourcePrefab=prefab,template=template});
        }
        EditorUtility.SetDirty(library);EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/Chess/Presentation/Spell Effects 02/Register Active Scene")]
    public static void RegisterActive()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode");
        RegisterTemplates(SceneManager.GetActiveScene());
    }

    private static void CreatePreview()
    {
        if(File.Exists(Preview))
        {
            var active=SceneManager.GetActiveScene();
            var existing=EditorSceneManager.OpenScene(Preview,OpenSceneMode.Additive);
            try{RegisterTemplates(existing);EditorSceneManager.SaveScene(existing);}
            finally{EditorSceneManager.CloseScene(existing,true);SceneManager.SetActiveScene(active);}
            return;
        }
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Previews/CardIdleFXPreview.unity",OpenSceneMode.Additive);
        try
        {
            var recipe=Read();
            foreach(var root in scene.GetRootGameObjects())
            {
                var old=root.GetComponent<CardIdleEffectPreview>();
                if(old!=null)
                {
                    var demo=root.AddComponent<CardSpellPreview>();demo.owners=old.owners;demo.cards=new CardAsset[4];
                    for(int i=0;i<4;i++){demo.cards[i]=AssetDatabase.LoadAssetAtPath<CardAsset>("Assets/Resources/Cards/"+Ids[i]+".asset");demo.owners[i].name=Ids[i]+" "+recipe.effects[i].name;}
                    UnityEngine.Object.DestroyImmediate(old);
                }
                foreach(var text in root.GetComponentsInChildren<TextMesh>())
                {
                    int k=Array.FindIndex(CardIdleEffectsSetup.Ids,id=>text.text.StartsWith(id));
                    if(k>=0){text.text=Ids[k]+" / "+recipe.effects[k].name;text.color=recipe.effects[k].color;text.characterSize=.025f;}
                    if(text.text.StartsWith("CHESS")){text.text="CHESS RPG / SPELLS 02";text.transform.position+=Vector3.forward*.8f;}
                }
            }
            RegisterTemplates(scene);EditorSceneManager.SaveScene(scene,Preview);
        }
        finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}
    }

    [MenuItem("Tools/Chess/Presentation/Spell Effects 02/Verify Assets")]
    public static void Verify()
    {
        var report=new List<string>();var recipe=Read();
        for(int i=0;i<4;i++)
        {
            var card=AssetDatabase.LoadAssetAtPath<CardAsset>("Assets/Resources/Cards/"+Ids[i]+".asset");
            var fx=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+recipe.effects[i].name+".prefab");
            if(fx==null||fx.GetComponentsInChildren<LineRenderer>().Length!=recipe.effects[i].lines.Length)throw new Exception("Geometry mismatch");
            if(i<2&&!card.definition.animations.Exists(a=>a.effectPrefab==fx&&a.recipient==CardAnimationRecipient.Both))throw new Exception("Animation not wired");
            if(i>=2&&card.definition.statusesToApply[0].idleEffectPrefab!=fx)throw new Exception("Status not wired");
            if((fx.GetComponent<CardSpellPulse>()!=null)!=(i<2))throw new Exception("Wrong lifetime");
            foreach(var r in fx.GetComponentsInChildren<Renderer>())if(r.sharedMaterial==null||ShaderUtil.ShaderHasError(r.sharedMaterial.shader))throw new Exception("Shader error");
            if(fx.GetComponentsInChildren<Collider>().Length>0)throw new Exception("Input obstruction");
            report.Add("PASS "+Ids[i]+": recipe geometry, references, lifetime, shader, no colliders");
        }
        Directory.CreateDirectory("output");File.WriteAllLines("output/card-spells02-assets.txt",report);
    }

    [MenuItem("Tools/Chess/Presentation/Spell Effects 02/Play Preview")]
    public static void Play()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode");
        var old=EditorSceneManager.playModeStartScene;
        SessionState.SetString("CardIdleOldStartScene",old==null?"":AssetDatabase.GetAssetPath(old));
        SessionState.SetBool("CardIdlePreviewPending",true);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Preview);EditorApplication.isPlaying=true;
    }

    [MenuItem("Tools/Chess/Presentation/Spell Effects 02/Verify Live")]
    public static void Live()
    {
        var demo=UnityEngine.Object.FindFirstObjectByType<CardSpellPreview>();
        if(!EditorApplication.isPlaying||demo==null)throw new Exception("Play preview first");
        demo.repeatBursts=false;
        double deadline=EditorApplication.timeSinceStartup+2;
        void AfterClear()
        {
            if(EditorApplication.timeSinceStartup<deadline)return;
            EditorApplication.update-=AfterClear;
            try
            {
                if(demo==null)throw new Exception("Preview interrupted");
                if(UnityEngine.Object.FindObjectsByType<CardSpellPulse>(FindObjectsSortMode.None).Length!=0)throw new Exception("One-shot effects failed to expire");
                demo.Fire();
                if(UnityEngine.Object.FindObjectsByType<CardSpellPulse>(FindObjectsSortMode.None).Length!=2)throw new Exception("Card animation receiver failed");
                for(int i=2;i<4;i++)
                {
                    var owner=demo.owners[i];var fx=owner.Statuses[0].idleEffectInstance;
                    if(fx==null)throw new Exception("Idle effect absent");
                    var pos=fx.transform.position;owner.transform.position+=Vector3.right;
                    if(Vector3.Distance(fx.transform.position,pos+Vector3.right)>.001f)throw new Exception("Not following owner");
                    owner.transform.position-=Vector3.right;owner.RemoveStatusesByCardId(Ids[i]);
                    if(fx.activeSelf)throw new Exception("Status removal failed");
                    owner.ApplyStatus(demo.cards[i].CreateDefinition().statusesToApply[0],owner,false);
                }
                demo.repeatBursts=true;
                File.WriteAllText("output/card-spells02-live.txt","PASS: two event receiver animations; timed destruction; two status idle effects; follow and removal.\n");
            }
            catch(Exception e){Debug.LogException(e);}
        }
        EditorApplication.update+=AfterClear;
    }
}
