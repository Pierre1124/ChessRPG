using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>共用可編輯的 Resources 預製件，長規則可捲動閱讀。</summary>
public sealed class CardKeywordPopup : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text detailText;
    public Button closeButton;
    public Button backdropButton;
    public ScrollRect scroll;
    private static CardKeywordPopup current;
    private LogicManager logic;
    public static bool IsOpen => current != null;

    public static void Show(CardKeywordEntry entry)
    {
        if (current != null) current.Close();
        var prefab = Resources.Load<CardKeywordPopup>("UI/CardKeywordPopup");
        if (prefab == null) { Debug.LogError("缺少 Resources/UI/CardKeywordPopup 預製件"); return; }
        current = Instantiate(prefab);
        current.titleText.text = entry.term;
        current.detailText.text = entry.description;
        current.detailText.richText = false;
        current.closeButton.onClick.AddListener(current.Close);
        current.backdropButton.onClick.AddListener(current.Close);
        current.logic = FindFirstObjectByType<LogicManager>();
        current.logic?.PushOperationLock("CardKeywordPopup");
        Canvas.ForceUpdateCanvases();
        current.scroll.verticalNormalizedPosition = 1;
    }

    public void Close()
    {
        ReleaseLock();
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
    private void ReleaseLock()
    {
        if (logic != null) logic.PopOperationLock("CardKeywordPopup");
        logic = null;
        if (current == this) current = null;
    }
    private void OnDestroy() => ReleaseLock();
}
