using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class JobIdleEffectsSetup
{
    const string Folder="Assets/Art/VFX/JobIdle";
    const string Output="D:/BlenderPJ/Test/CardIdleFX";
    static readonly int[] Jobs={1,2,3,5,6,7,8,11,12};
    static readonly string[] Names={"SpearWatch","ShieldGuard","MoatRipple","Vanguard","PaladinLight","ArcaneOrbit","ClericPrayer","SovereignCrown","TyrantCrown"};
    static readonly Color[] Colors={new Color(.2f,.85f,1,.75f),new Color(.3f,.6f,1,.75f),new Color(.05f,.85f,.8f,.65f),new Color(1,.4f,.08f,.7f),new Color(1,.78f,.3f,.7f),new Color(.6f,.25f,1,.75f),new Color(.55f,1,.8f,.75f),new Color(1,.7f,.12f,.75f),new Color(.9f,.1f,.45f,.7f)};
    static Material material;
    static List<CardIdleEffectMotion.Element> elements;
    static Color color;
    static Transform Line(Transform parent,string name,Vector3[] points,bool loop=false,float width=.023f)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=loop;
        line.positionCount=points.Length;line.SetPositions(points);line.widthMultiplier=width;
        line.startColor=line.endColor=color;line.sharedMaterial=material;
        line.numCapVertices=2;line.numCornerVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
        return go.transform;
    }
    static Transform Group(Transform parent,string name)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    static Vector3 Polar(float a,float r,float y)=>new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
    static Transform Ring(Transform p,float radius,float y,string name)
    {return Line(p,name,Enumerable.Range(0,64).Select(i=>Polar(i*Mathf.PI*2/64,radius,y)).ToArray(),true,.014f);}
    static void Animate(Transform t,float turns=0,float bob=0,float phase=0,float breathe=0)
    {elements.Add(new CardIdleEffectMotion.Element{target=t,position=t.localPosition,turnsPerLoop=turns,bob=bob,phase=phase,breathe=breathe});}
    static void Glyph(Transform parent,float angle,float radius,float y,Vector3[] shape,string name)
    {var t=Group(parent,name);t.localPosition=Polar(angle,radius,y);t.localRotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg+90,0);Line(t,name+" outline",shape);}
    static void Build(int job,Transform p)
    {
        Ring(p,.9f,.035f,"Ground boundary");
        var orbit=Group(p,"Orbiting symbols");
        Animate(orbit,job==12?-1:1);
        if(job==1 || job==5)
        {
            for(int j=0;j<6;j++)
            {
                float a=j*Mathf.PI/3;
                var shape=job==1?new[]{new Vector3(0,-.22f,0),new Vector3(0,.2f,0),new Vector3(-.09f,.06f,0),new Vector3(0,.2f,0),new Vector3(.09f,.06f,0)}:new[]{new Vector3(-.13f,-.08f,0),new Vector3(0,.1f,0),new Vector3(.13f,-.08f,0)};
                Glyph(orbit,a,.78f,.45f,shape,job==1?"Spear":"Vanguard chevron");
                if(job==5)Glyph(orbit,a,.78f,.68f,shape,"Second chevron");
            }
        }
        else if(job==2)
        {
            for(int j=0;j<4;j++)Glyph(orbit,j*Mathf.PI/2,.78f,.5f,new[]{new Vector3(-.18f,.16f,0),new Vector3(.18f,.16f,0),new Vector3(.15f,-.09f,0),new Vector3(0,-.23f,0),new Vector3(-.15f,-.09f,0),new Vector3(-.18f,.16f,0)},"Shield");
            Ring(p,.7f,.1f,"Inner ward");
        }
        else if(job==3)
        {
            for(int j=0;j<3;j++)
            {
                var wave=Line(p,"Water ripple",Enumerable.Range(0,80).Select(i=>{float a=i*Mathf.PI/40;return Polar(a,.55f+j*.16f,.10f+j*.07f+.035f*Mathf.Sin(a*6));}).ToArray(),true,.023f);
                Animate(wave,j%2==0?1:-1,.015f,j,.04f);
            }
        }
        else if(job==6 || job==8)
        {
            Animate(Ring(p,.42f,1.08f,"Halo"),0,.035f);
            for(int j=0;j<4;j++)
            {
                float a=j*Mathf.PI/2;
                Glyph(orbit,a,.73f,.55f,new[]{new Vector3(0,-.17f,0),new Vector3(0,.22f,0)},"Holy cross stem");
                Glyph(orbit,a,.73f,.62f,new[]{new Vector3(-.12f,0,0),new Vector3(.12f,0,0)},"Holy cross arms");
            }
            if(job==8)for(int s=-1;s<=1;s+=2)
                Line(p,"Prayer wing",new[]{new Vector3(s*.25f,.8f,0),new Vector3(s*.85f,1.03f,0),new Vector3(s*.65f,.72f,0),new Vector3(s*.4f,.6f,0)},false,.025f);
            else Ring(p,.7f,.12f,"Paladin ward");
        }
        else if(job==7)
        {
            Line(orbit,"Arcane star",new[]{0,2,4,1,3}.Select(i=>Polar(i*Mathf.PI*2/5,.72f,.08f)).ToArray(),true);
            for(int j=0;j<3;j++)
            {
                var arc=Line(p,"Arcane helix",Enumerable.Range(0,50).Select(i=>{float t=i/49f;return Polar(j*Mathf.PI*2/3+t*Mathf.PI*1.3f,.7f,.2f+t*.7f);}).ToArray());
                Animate(arc,-1,.02f,j);
            }
        }
        else
        {
            float y=1.03f;
            for(int j=0;j<6;j++)
            {
                float a=j*Mathf.PI/3;
                var pts=new[]{Polar(a,.44f,y),Polar(a+.20f,.52f,y+(job==12 && j%2==0?.07f:.23f)),Polar(a+.42f,.44f,y),Polar(a+.74f,.44f,y)};
                Line(orbit,"Crown point",pts,false,.026f);
            }
            Ring(orbit,.44f,y-.035f,"Crown band");
            if(job==11)for(int j=0;j<8;j++){float a=j*Mathf.PI/4;Line(orbit,"Royal ray",new[]{Polar(a,.65f,.07f),Polar(a,.83f,.07f)});}
            else for(int j=0;j<3;j++)Glyph(orbit,j*Mathf.PI*2/3,.75f,.45f,new[]{new Vector3(0,.18f,0),new Vector3(.07f,0,0),new Vector3(0,-.18f,0),new Vector3(-.07f,0,0),new Vector3(0,.18f,0)},"Dark seal");
        }
        var motes=Group(p,"Orbiting sparks");Animate(motes,-1,.035f,0,.12f);
        for(int j=0;j<8;j++)
        {
            var q=Polar(j*2.399f,.62f+(j%3)*.09f,.16f+j*.105f);
            Line(motes,"Spark",new[]{q-Vector3.up*.025f,q+Vector3.up*.025f},false,.025f);
        }
    }
    [MenuItem("Tools/Chess/Presentation/Job Idle/Create Missing Effects")]
    public static void Create()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit play mode first");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/CardIdle/IdleLines.mat");
        if(!material)throw new Exception("Missing existing idle material");
        for(int i=0;i<Jobs.Length;i++)
        {
            string id="J"+Jobs[i].ToString("00"),path=Folder+"/"+Names[i]+".prefab";
            var card=AssetDatabase.LoadAssetAtPath<CardAsset>("Assets/Resources/Cards/"+id+".asset");
            if(card.definition.statusesToApply.Count==0)throw new Exception("Missing job status "+id);
            if(card.definition.statusesToApply[0].idleEffectPrefab)continue;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!prefab)
            {
                var root=new GameObject(Names[i]);
                try
                {
                    var motion=root.AddComponent<CardIdleEffectMotion>();motion.loopSeconds=4;motion.worldRadius=.42f;
                    motion.content=Group(root.transform,"Aura geometry");elements=new List<CardIdleEffectMotion.Element>();color=Colors[i];
                    Build(Jobs[i],motion.content);motion.elements=elements.ToArray();
                    prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            Undo.RecordObject(card,"Assign job idle effect");card.definition.statusesToApply[0].idleEffectPrefab=prefab;EditorUtility.SetDirty(card);
        }
        AssetDatabase.SaveAssets();Verify();
    }
    [MenuItem("Tools/Chess/Presentation/Job Idle/Verify All Jobs")]
    public static void Verify()
    {
        var lines=new List<string>();
        for(int i=1;i<=12;i++)
        {
            string id="J"+i.ToString("00");var card=AssetDatabase.LoadAssetAtPath<CardAsset>("Assets/Resources/Cards/"+id+".asset");
            var def=card.CreateDefinition();var prefab=def.statusesToApply[0].idleEffectPrefab;
            if(!prefab)throw new Exception("Unassigned "+id);
            var go=UnityEngine.Object.Instantiate(prefab);
            try
            {
                if(go.GetComponentsInChildren<Collider>(true).Length>0)throw new Exception("Collider on "+id);
                foreach(var r in go.GetComponentsInChildren<Renderer>(true))if(!r.sharedMaterial || !r.sharedMaterial.shader.isSupported)throw new Exception("Invalid material "+id);
                var motion=go.GetComponent<CardIdleEffectMotion>();motion.Evaluate(0);
                var starts=motion.elements.Select(e=>e.target.localToWorldMatrix).ToArray();
                motion.Evaluate(motion.loopSeconds);
                for(int n=0;n<starts.Length;n++)for(int k=0;k<16;k++)if(Mathf.Abs(starts[n][k]-motion.elements[n].target.localToWorldMatrix[k])>.001f)throw new Exception("Loop seam "+id);
                motion.Evaluate(.73f);
                if(!motion.elements.Where((e,n)=>e.target.localToWorldMatrix!=starts[n]).Any())throw new Exception("No movement "+id);
                lines.Add(id+" "+card.definition.cardName+" -> "+prefab.name+": clone reference, supported materials, animation, seamless loop, no colliders PASS");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        File.WriteAllLines(Output+"/job-idle-verification.txt",lines);Debug.Log(string.Join("\n",lines));
    }
}
