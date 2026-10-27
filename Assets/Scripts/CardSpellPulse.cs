using UnityEngine;

/// <summary>一次性特效的淡入淡出；沿用共用材質，不建立材質副本。</summary>
public sealed class CardSpellPulse : MonoBehaviour
{
    public float duration = 1.5f;
    private LineRenderer[] lines;
    private Color[] colors;
    private float elapsed;
    private void Awake()
    {
        lines=GetComponentsInChildren<LineRenderer>();colors=new Color[lines.Length];
        for(int i=0;i<lines.Length;i++)colors[i]=lines[i].startColor;
    }
    private void OnEnable(){elapsed=0;Evaluate(0);}
    private void Update(){elapsed+=Time.deltaTime;Evaluate(elapsed);if(elapsed>=duration)Destroy(gameObject);}
    public void Evaluate(float seconds)
    {
        if(lines==null)return;
        float t=Mathf.Clamp01(seconds/Mathf.Max(.05f,duration));
        float alpha=Mathf.Min(t*8,1)*Mathf.Pow(1-t,1.3f);
        for(int i=0;i<lines.Length;i++){Color c=colors[i];c.a*=alpha;lines[i].startColor=lines[i].endColor=c;}
    }
}
