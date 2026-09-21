using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>集中處理卡牌圖片、文字與標籤，與手牌資料及操作流程分離。</summary>
public static class CardPresentation
{
    /// <summary>將卡牌定義套用到既有卡片 UI 階層。</summary>
    public static void ApplyCardData(GameObject cardObject, CardDefinition card)
    {
        Image image = cardObject.GetComponent<Image>();
        if (image == null)
        {
            image = FindChildComponent<Image>(
                cardObject.transform,
                "CardImage"
            );
        }

        if (image != null && card.cardImage != null)
        {
            image.sprite = card.cardImage;
        }

        SetText(cardObject.transform, "CardNameText", card.cardName);
        SetNestedText(
            cardObject.transform,
            "CardLord",
            "CardLordText",
            card.description
        );
        ApplyTags(cardObject.transform, card.tags);
    }

    /// <summary>
    /// 將卡牌標籤套用到對應的 UI 文字。
    /// </summary>
    private static void ApplyTags(Transform root, List<string> tags)
    {
        Transform tagGroup = FindChildRecursive(root, "CardTagGroup");
        Transform template =
            tagGroup != null
                ? FindChildRecursive(tagGroup, "CardTag")
                : FindChildRecursive(root, "CardTag");

        if (template == null || template.parent == null)
        {
            return;
        }

        for (int i = template.parent.childCount - 1; i >= 0; i--)
        {
            Transform child = template.parent.GetChild(i);
            if (child != template && child.name == "CardTag")
            {
                Object.Destroy(child.gameObject);
            }
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
                    : Object.Instantiate(template.gameObject, template.parent)
                        .transform;

            tagTransform.name = "CardTag";
            tagTransform.gameObject.SetActive(true);

            TMP_Text text =
                tagTransform.GetComponentInChildren<TMP_Text>(true);

            if (text != null)
            {
                text.text = tags[i];
            }
        }
    }

    /// <summary>更新指定子物件的文字。</summary>
    private static void SetText(
        Transform root,
        string childName,
        string value
    )
    {
        TMP_Text text = FindChildComponent<TMP_Text>(root, childName);
        if (text != null)
        {
            text.text = value;
        }
    }

    /// <summary>
    /// 更新指定子階層中的文字元件。
    /// </summary>
    private static void SetNestedText(
        Transform root,
        string parentName,
        string childName,
        string value
    )
    {
        Transform parent = FindChildRecursive(root, parentName);
        if (parent != null)
        {
            SetText(parent, childName, value);
        }
    }

    /// <summary>
    /// 依名稱尋找子物件，再取得所需類型的元件。
    /// </summary>
    private static T FindChildComponent<T>(
        Transform root,
        string childName
    ) where T : Component
    {
        Transform child = FindChildRecursive(root, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    /// <summary>
    /// 依階層順序遞迴尋找指定名稱的 Transform；回傳第一個符合的物件。
    /// </summary>
    private static Transform FindChildRecursive(
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

}
