using UnityEngine;

/// <summary>獨立預覽場景透過正式狀態入口展示特效，不執行出牌或勝負規則。</summary>
public sealed class CardIdleEffectPreview : MonoBehaviour
{
    public Piece[] owners;
    public CardAsset[] cards;
    private void Start() { Show(); }

    public void Show()
    {
        for(int i=0;i<owners.Length;i++)
        {
            owners[i].RemoveStatusesByCardId(cards[i].definition.id);
            owners[i].ApplyStatus(cards[i].CreateDefinition().statusesToApply[0],owners[i],true);
        }
    }
}
