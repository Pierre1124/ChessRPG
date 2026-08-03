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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            SpawnLancerCard();
            SpawnShielderCard();
        }
    }

    public void SpawnLancerCard()
    {
        SpawnCard(cardLibrary != null ? cardLibrary.GetCard("J01") : null);
    }

    public void SpawnShielderCard()
    {
        SpawnCard(cardLibrary != null ? cardLibrary.GetCard("J02") : null);
    }

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
