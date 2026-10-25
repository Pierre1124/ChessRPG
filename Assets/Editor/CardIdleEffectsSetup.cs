using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>建立可編輯原生 Unity 特效，僅補空的卡牌待機欄位。</summary>
public static class CardIdleEffectsSetup
{
    public const string Folder = "Assets/Art/VFX/CardIdle";
    public static readonly string[] Names = { "SacredAegis", "BloodVow", "IonCage", "VenomBloom" };
    public static readonly string[] Ids = { "J10", "J09", "J04", "E09" };
    private static readonly Color[] Colors = { new Color(1,.64f,.12f,.8f), new Color(1,.06f,.18f,.7f), new Color(.58f,.18f,1,.8f), new Color(.3f,1,.12f,.65f) };

    [MenuItem("Tools/Chess/Presentation/Idle Effects/Create and Assign")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before creating assets.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var shader = Shader.Find("ChessRPG/VFX/Idle Additive");
        if (shader == null) throw new InvalidOperationException("Idle shader has not imported.");
        var line = MaterialAsset("IdleLines", shader, null);
        var dots = MaterialAsset("IdleMotes", shader, SoftDot());
        for (int k=0;k<4;k++)
        {
            string path = Folder + "/" + Names[k] + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var root = new GameObject(Names[k]);
                try
                {
                    var motion = root.AddComponent<CardIdleEffectMotion>();
                    var content = new GameObject("Aura geometry").transform;
                    content.SetParent(root.transform,false); motion.content=content;
                    var elements = new List<CardIdleEffectMotion.Element>();
                    Build(k,content,line,dots,elements);
                    motion.elements=elements.ToArray();
                    prefab = PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            var card=AssetDatabase.LoadAssetAtPath<CardAsset>("Assets/Resources/Cards/"+Ids[k]+".asset");
            if (card == null || card.definition.statusesToApply.Count == 0) throw new InvalidOperationException("Missing status: "+Ids[k]);
            var status=card.definition.statusesToApply[0];
            if (status.idleEffectPrefab == null)
            {
                Undo.RecordObject(card,"Assign card idle effect");
                status.idleEffectPrefab=prefab; EditorUtility.SetDirty(card);
            }
        }
        AssetDatabase.SaveAssets();
        Verify();
    }

    private static Material MaterialAsset(string name, Shader shader, Texture texture)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m!=null)return m;
        m=new Material(shader);m.name=name;m.SetColor("_BaseColor",Color.white);
        if(texture!=null)m.SetTexture("_BaseMap",texture);
        AssetDatabase.CreateAsset(m,path);return m;
    }

    private static Texture2D SoftDot()
    {
        string path=Folder+"/SoftMote.png";
        var old=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(old!=null)return old;
        var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32))/32;
            float a=Mathf.Pow(Mathf.Clamp01(1-d),2);
            tex.SetPixel(x,y,new Color(1,1,1,a));
        }
        tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Transform Line(string name,Transform parent,List<Vector3> points,float width,Color color,Material material,bool loop=false)
    {
        var o=new GameObject(name);o.transform.SetParent(parent,false);
        var l=o.AddComponent<LineRenderer>();l.useWorldSpace=false;l.loop=loop;l.positionCount=points.Count;l.SetPositions(points.ToArray());
        l.widthMultiplier=width;l.startColor=l.endColor=color;l.sharedMaterial=material;l.numCornerVertices=2;l.numCapVertices=2;
        l.shadowCastingMode=ShadowCastingMode.Off;l.receiveShadows=false;
        return o.transform;
    }

    private static Transform Ring(string name,Transform parent,float radius,float y,Color col,Material mat)
    {
        var pts=new List<Vector3>();for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;pts.Add(new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius));}
        return Line(name,parent,pts,.018f,col,mat,true);
    }

    private static void Animate(List<CardIdleEffectMotion.Element> es,Transform t,float turns,float bob=0,float phase=0,float breathe=0)
    { es.Add(new CardIdleEffectMotion.Element{target=t,position=t.localPosition,turnsPerLoop=turns,bob=bob,phase=phase,breathe=breathe}); }

    private static void Build(int k,Transform p,Material line,Material dots,List<CardIdleEffectMotion.Element> es)
    {
        var c=Colors[k];
        Ring("Ground seal",p,.92f,.035f,c,line);
        if(k==0)
        {
            Ring("Inner seal",p,.72f,.045f,c,line);
            var runes=new GameObject("Rotating runes").transform;runes.SetParent(p,false);
            for(int j=0;j<12;j++)
            {
                float a=j*Mathf.PI/6;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Line("Rune",runes,new List<Vector3>{dir*.81f+Vector3.up*.055f,dir*.97f+Vector3.up*.055f},.018f,c,line);
            }
            Animate(es,runes,1);
            Animate(es,Ring("Crown halo",p,.45f,1.13f,c,line),0,.04f);
        }
        if(k==1 || k==3)
        {
            for(int j=0;j<3;j++)
            {
                var pts=new List<Vector3>();
                for(int i=0;i<48;i++)
                {
                    float t=i/47f,a=j*Mathf.PI*2/3+t*Mathf.PI*1.65f,r=.7f+.1f*Mathf.Sin(t*Mathf.PI);
                    pts.Add(new Vector3(r*Mathf.Cos(a),(k==1?.1f+t*.9f:.09f+t*.2f),r*Mathf.Sin(a)));
                }
                var arc=Line(k==1?"Blood ribbon":"Venom wisp",p,pts,k==1?.035f:.025f,c,line);
                var lr=arc.GetComponent<LineRenderer>();lr.widthCurve=new AnimationCurve(new Keyframe(0,.05f),new Keyframe(.5f,1),new Keyframe(1,.05f));
                Animate(es,arc,k==1?-1:1,.025f,j);
            }
        }
        if(k==2)
        {
            for(int j=0;j<4;j++)
            {
                var pts=new List<Vector3>();
                for(int i=0;i<13;i++)
                {
                    float a=j*Mathf.PI*.5f+.19f*Mathf.Sin(i*2.7f),r=.72f+.1f*Mathf.Sin(i*1.8f);
                    pts.Add(new Vector3(Mathf.Cos(a)*r,.08f+i/12f,Mathf.Sin(a)*r));
                }
                Animate(es,Line("Electric arc",p,pts,.022f,c,line),j%2==0?1:-1,0,j,.12f);
            }
            Ring("Upper charge",p,.48f,1.06f,c,line);
        }
        var go=new GameObject("Looping motes");go.transform.SetParent(p,false);
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.duration=4;main.loop=true;main.prewarm=true;main.startLifetime=4;main.startSpeed=0;
        main.startSize=new ParticleSystem.MinMaxCurve(k==3?.07f:.035f,k==3?.14f:.075f);
        main.startColor=c;main.maxParticles=32;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
        var em=ps.emission;em.rateOverTime=7;
        var sh=ps.shape;sh.shapeType=ParticleSystemShapeType.Circle;sh.radius=.72f;sh.radiusThickness=.35f;sh.rotation=new Vector3(90,0,0);
        var vel=ps.velocityOverLifetime;vel.enabled=true;vel.space=ParticleSystemSimulationSpace.Local;vel.y=.24f;
        var col=ps.colorOverLifetime;col.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.9f,.2f),new GradientAlphaKey(.6f,.7f),new GradientAlphaKey(0,1)});col.color=gradient;
        var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.2f),new Keyframe(.45f,1),new Keyframe(1,.1f)));
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=dots;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(40+k);
        ps.Play();
    }

    [MenuItem("Tools/Chess/Presentation/Idle Effects/Verify Assets")]
    public static void Verify()
    {
        var result=new List<string>();
        for(int k=0;k<4;k++)
        {
            var card=AssetDatabase.LoadAssetAtPath<CardAsset>("Assets/Resources/Cards/"+Ids[k]+".asset");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+Names[k]+".prefab");
            if(prefab==null || card.definition.statusesToApply[0].idleEffectPrefab!=prefab)throw new Exception("Card wiring failed: "+Ids[k]);
            if(prefab.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("VFX must not intercept input");
            foreach(var r in prefab.GetComponentsInChildren<Renderer>(true))
                if(r.sharedMaterial==null || !r.sharedMaterial.shader.isSupported || ShaderUtil.ShaderHasError(r.sharedMaterial.shader))throw new Exception("Invalid material: "+r.name);
            foreach(var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                if(!ps.main.loop || ps.main.simulationSpace!=ParticleSystemSimulationSpace.Local || ps.main.maxParticles>32)throw new Exception("Invalid particle settings");
            var copy=UnityEngine.Object.Instantiate(prefab);
            try
            {
                var motion=copy.GetComponent<CardIdleEffectMotion>();motion.Evaluate(0);
                var starts=new List<Matrix4x4>();foreach(var e in motion.elements)starts.Add(e.target.localToWorldMatrix);
                motion.Evaluate(4);
                for(int i=0;i<starts.Count;i++)for(int j=0;j<16;j++)
                    if(Mathf.Abs(starts[i][j]-motion.elements[i].target.localToWorldMatrix[j])>.0001f)throw new Exception("Loop seam");
            }
            finally{UnityEngine.Object.DestroyImmediate(copy);}
            result.Add("PASS: "+Ids[k]+" -> "+Names[k]+"; material, loop seam, local particles, no colliders");
        }
        Directory.CreateDirectory("output");File.WriteAllLines("output/card-idle-assets.txt",result);Debug.Log(string.Join("\n",result));
    }
}
