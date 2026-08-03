using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FieldCardPlace : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private StatusInfoTooltip statusTooltip;
    [SerializeField] private Color emptyColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    private CardDefinition activeCard;
    private Material runtimeMaterial;

    public CardDefinition ActiveCard
    {
        get { return activeCard; }
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureRuntimeMaterial();
        RefreshVisual();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    public void SetCard(CardDefinition card)
    {
        activeCard = card;
        EnsureRuntimeMaterial();
        RefreshVisual();
    }

    public void Clear()
    {
        SetCard(null);
    }

    public void ShowInfo(Vector2 screenPosition)
    {
        if (statusTooltip == null || activeCard == null)
        {
            return;
        }

        statusTooltip.Show(BuildTooltipContent(activeCard), screenPosition);
    }

    public void HideInfo()
    {
        if (statusTooltip != null)
        {
            statusTooltip.Hide();
        }
    }

    private void OnMouseDown()
    {
        if (InputManager.IsPointerOverUiStatic())
        {
            return;
        }

        ShowInfo(Input.mousePosition);
    }

    private void ResolveReferences()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (statusTooltip == null)
        {
            statusTooltip = Object.FindFirstObjectByType<StatusInfoTooltip>(
                FindObjectsInactive.Include
            );
        }
    }

    private void EnsureRuntimeMaterial()
    {
        if (targetRenderer == null || runtimeMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        runtimeMaterial = shader != null
            ? new Material(shader)
            : new Material(targetRenderer.material);
        runtimeMaterial.name = $"{name}_FieldCardRuntime";
        targetRenderer.material = runtimeMaterial;
    }

    private void RefreshVisual()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        Sprite sprite = activeCard != null
            ? activeCard.cardImage != null
                ? activeCard.cardImage
                : activeCard.skillImage
            : null;

        Texture texture = sprite != null ? sprite.texture : null;
        SetMaterialTexture(texture);
        SetMaterialColor(texture != null ? Color.white : emptyColor);

        Debug.Log(
            $"[CardDebug][FieldPlaceVisual] Place={name} | " +
            $"Card={(activeCard != null ? activeCard.id : "None")} | " +
            $"Texture={(texture != null ? texture.name : "None")} | " +
            $"Shader={runtimeMaterial.shader.name}"
        );
    }

    private void SetMaterialTexture(Texture texture)
    {
        if (runtimeMaterial.HasProperty("_BaseMap"))
        {
            runtimeMaterial.SetTexture("_BaseMap", texture);
        }

        if (runtimeMaterial.HasProperty("_MainTex"))
        {
            runtimeMaterial.SetTexture("_MainTex", texture);
        }

        runtimeMaterial.mainTexture = texture;
    }

    private void SetMaterialColor(Color color)
    {
        if (runtimeMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor("_BaseColor", color);
        }

        if (runtimeMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.SetColor("_Color", color);
        }

        runtimeMaterial.color = color;
    }

    private string BuildTooltipContent(CardDefinition card)
    {
        return $"{card.id} {card.cardName}\n" +
            "類型：場地卡\n" +
            $"條件/代價：{GetTextOrNone(card.conditionCost)}\n" +
            $"效果：{GetTextOrNone(card.description)}\n" +
            "來源：場地卡區";
    }

    private string GetTextOrNone(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "無" : value;
    }
}
