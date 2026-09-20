using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CardDebugSpawner : MonoBehaviour
{
    [SerializeField] private RectTransform spawnRoot;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private ChessCard cardLibrary;

    /// <summary>
    /// 偵測 L 快捷鍵並建立兩張既有測試卡牌。
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            SpawnLancerCard();
            SpawnShielderCard();
        }
    }

    /// <summary>
    /// 使用卡牌庫中的 J01 建立測試卡牌。
    /// </summary>
    public void SpawnLancerCard()
    {
        SpawnCard(cardLibrary != null ? cardLibrary.GetCard("J01") : null);
    }

    /// <summary>
    /// 使用卡牌庫中的 J02 建立測試卡牌。
    /// </summary>
    public void SpawnShielderCard()
    {
        SpawnCard(cardLibrary != null ? cardLibrary.GetCard("J02") : null);
    }

    /// <summary>
    /// 依卡牌定義建立卡片物件並套用顯示資料。
    /// </summary>
    public GameObject SpawnCard(CardDefinition card)
    {
        if (
            spawnRoot == null ||
            cardPrefab == null ||
            card == null
        )
        {
            Debug.LogWarning(
                "CardDebugSpawner is missing CardGameUI, CardImage prefab, or card data."
            );
            return null;
        }

        GameObject cardObject =
            Instantiate(cardPrefab, spawnRoot);

        cardObject.name = card.cardName;

        RectTransform rectTransform =
            cardObject.GetComponent<RectTransform>();

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        ApplyCardData(cardObject, card);

        return cardObject;
    }

    /// <summary>
    /// 將卡牌圖像、文字與相關資訊套用到卡片 UI。
    /// </summary>
    private void ApplyCardData(
        GameObject cardObject,
        CardDefinition card
    )
    {
        Image image =
            cardObject.GetComponent<Image>();

        if (image == null)
        {
            image =
                FindChildComponent<Image>(
                    cardObject.transform,
                    "CardImage"
                );
        }

        if (image != null && card.cardImage != null)
        {
            image.sprite = card.cardImage;
        }

        SetText(cardObject.transform, "CardNameText", card.cardName);
        ApplyTags(cardObject.transform, card.tags);
        SetNestedText(
            cardObject.transform,
            "CardLord",
            "CardLordText",
            card.description
        );
    }

    /// <summary>
    /// 將卡牌標籤套用到對應的 UI 文字。
    /// </summary>
    private void ApplyTags(
        Transform root,
        List<string> tags
    )
    {
        Transform tagGroup =
            FindChildRecursive(root, "CardTagGroup");

        Transform template =
            tagGroup != null
                ? FindChildRecursive(tagGroup, "CardTag")
                : FindChildRecursive(root, "CardTag");

        if (template == null)
        {
            return;
        }

        if (tags == null || tags.Count == 0)
        {
            template.gameObject.SetActive(false);
            return;
        }

        for (int i = 0; i < tags.Count; i++)
        {
            Transform tagTransform =
                i == 0
                    ? template
                    : Instantiate(template.gameObject, template.parent).transform;

            tagTransform.name = "CardTag";
            tagTransform.gameObject.SetActive(true);
            SetTextOnTransform(tagTransform, tags[i]);
        }
    }

    /// <summary>
    /// 尋找指定文字元件並更新其顯示內容。
    /// </summary>
    private void SetText(
        Transform root,
        string childName,
        string value
    )
    {
        TMP_Text text =
            FindChildComponent<TMP_Text>(root, childName);

        if (text != null)
        {
            text.text = value;
        }
    }

    /// <summary>
    /// 更新指定子階層中的文字元件。
    /// </summary>
    private void SetNestedText(
        Transform root,
        string parentName,
        string childName,
        string value
    )
    {
        Transform parent =
            FindChildRecursive(root, parentName);

        if (parent == null)
        {
            return;
        }

        TMP_Text text =
            FindChildComponent<TMP_Text>(parent, childName);

        if (text != null)
        {
            text.text = value;
        }
    }

    /// <summary>
    /// 更新指定 Transform 上的文字元件。
    /// </summary>
    private void SetTextOnTransform(
        Transform root,
        string value
    )
    {
        TMP_Text text =
            root.GetComponentInChildren<TMP_Text>(true);

        if (text != null)
        {
            text.text = value;
        }
    }

    /// <summary>
    /// 依名稱尋找子物件，再取得所需類型的元件。
    /// </summary>
    private T FindChildComponent<T>(
        Transform root,
        string childName
    ) where T : Component
    {
        Transform child =
            FindChildRecursive(root, childName);

        return child != null
            ? child.GetComponent<T>()
            : null;
    }

    /// <summary>
    /// 依階層順序遞迴尋找指定名稱的 Transform；回傳第一個符合的物件。
    /// </summary>
    private Transform FindChildRecursive(
        Transform root,
        string childName
    )
    {
        if (root.name == childName)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            Transform match =
                FindChildRecursive(child, childName);

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// 補齊元件所需的預設資料或場景引用。
    /// </summary>
    private void ResolveDefaults()
    {
        if (spawnRoot == null)
        {
            GameObject canvasObject =
                GameObject.Find("CardGameUI");

            if (canvasObject != null)
            {
                spawnRoot =
                    canvasObject.GetComponent<RectTransform>();
            }
        }

#if UNITY_EDITOR
        if (cardPrefab == null)
        {
            cardPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/CardPrefabs/CardImage.prefab"
                );
        }

#endif
    }
}
