using UnityEngine;
/// <summary>第二組教學場景，示範真正的卡片動畫及狀態掛接。</summary>
public sealed class CardSpellPreview : MonoBehaviour
{
    public Piece[] owners;
    public CardAsset[] cards;
    public bool repeatBursts=true;
    private float elapsed;
    private void Start()
    {
        for(int i=2;i<4;i++)owners[i].ApplyStatus(cards[i].CreateDefinition().statusesToApply[0],owners[i],false);
        Fire();
    }
    private void Update(){elapsed+=Time.deltaTime;if(repeatBursts&&elapsed>=3){elapsed=0;Fire();}}
    public void Fire()
    {
        for(int i=0;i<2;i++)CardAnimationEvents.Play(owners[i],owners[i],cards[i].CreateDefinition(),i==0?CardAnimationTiming.OnDamageDealt:CardAnimationTiming.OnHealed);
    }
}
