using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FieldCardPlace : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private StatusInfoTooltip statusTooltip;
    [SerializeField] private Color emptyColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    private CardDefinition activeCard;
    private Material runtimeMaterial;
    private Vector3 authoredLocalPosition;
    private float mobileFusionProgress = -1;

    private void OnEnable() => LogicManager.InvalidateCardTargets();
    private void OnDisable() => LogicManager.InvalidateCardTargets();

    public CardDefinition ActiveCard
    {
        get { return activeCard; }
    }

    /// <summary>
    /// 取得場地引用、建立執行期材質並刷新初始顯示。
    /// </summary>
    private void Awake()
    {
        authoredLocalPosition = transform.localPosition;
        ResolveReferences();
        EnsureRuntimeMaterial();
        RefreshVisual();
    }

    public void SetMobileFusionProgress(float progress) => mobileFusionProgress = progress;

    private void LateUpdate()
    {
        if (!MobilePrototype.Enabled) return;
        bool portrait = MobilePortraitLayout.IsPortrait;
        Vector3 start = portrait
            ? new Vector3(name.EndsWith("2") ? 5.6f : 1.4f, authoredLocalPosition.y, -2.1f)
            : authoredLocalPosition;
        Vector3 center = portrait ? new Vector3(3.5f, start.y, -2.1f) : new Vector3(9, start.y, 3.5f);
        bool fused = activeCard != null && FieldCardRules.IsAdvancedField(activeCard.id);
        Vector3 position = fused ? center : start;
        if (mobileFusionProgress >= 0)
        {
            float t = Mathf.Clamp01(mobileFusionProgress);
            position = Vector3.Lerp(start, center, Mathf.Clamp01(t * 4));
            position.y += Mathf.Sin(Mathf.Clamp01((t - .25f) / .75f) * Mathf.PI) * 2;
        }
        transform.localPosition = position;
    }

    /// <summary>
    /// 編輯器重設元件時補齊場地引用。
    /// </summary>
    private void Reset()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 將場地卡指定到此欄位並更新顯示。
    /// </summary>
    public void SetCard(CardDefinition card)
    {
        if (activeCard != card) LogicManager.InvalidateCardTargets();
        activeCard = card;
        EnsureRuntimeMaterial();
        RefreshVisual();
    }

    /// <summary>
    /// 清除此場地欄位的卡牌與顯示內容。
    /// </summary>
    public void Clear()
    {
        SetCard(null);
    }

    /// <summary>
    /// 顯示此場地卡的資訊提示。
    /// </summary>
    public void ShowInfo(Vector2 screenPosition)
    {
        if (statusTooltip == null || activeCard == null)
        {
            return;
        }

        statusTooltip.Show(BuildTooltipContent(activeCard), screenPosition);
    }

    /// <summary>
    /// 隱藏此場地卡的資訊提示。
    /// </summary>
    public void HideInfo()
    {
        if (statusTooltip != null)
        {
            statusTooltip.Hide();
        }
    }

    /// <summary>
    /// 回應場地欄位點擊並顯示資訊。
    /// </summary>
    private void Update()
    {
        if (!GameplayPointer.Released || GameplayPointer.Canceled || CardDragHandler.BlocksBoardInput ||
            ControlBindings.SuppressGameplay || CardKeywordPopup.IsOpen || InputManager.IsPointerOverUiStatic())
        {
            return;
        }

        if (Camera.main != null && Physics.Raycast(Camera.main.ScreenPointToRay(GameplayPointer.Position), out RaycastHit hit) &&
            hit.transform.GetComponentInParent<FieldCardPlace>() == this) ShowInfo(GameplayPointer.Position);
    }

    /// <summary>
    /// 補齊此元件所需的場景與 UI 引用。
    /// </summary>
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

    /// <summary>
    /// 確保場地顯示使用可獨立修改的執行期材質。
    /// </summary>
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

    /// <summary>
    /// 依目前場地卡更新圖像與材質顯示。
    /// </summary>
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

    /// <summary>
    /// 將場地卡貼圖套用到材質支援的貼圖屬性。
    /// </summary>
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

    /// <summary>
    /// 將顏色套用到材質支援的色彩屬性。
    /// </summary>
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

    /// <summary>
    /// 組合場地卡的名稱與效果說明。
    /// </summary>
    private string BuildTooltipContent(CardDefinition card)
    {
        return $"{card.id} {card.cardName}\n" +
            "類型：場地卡\n" +
            $"條件/代價：{GetTextOrNone(card.conditionCost)}\n" +
            $"效果：{GetTextOrNone(card.description)}\n" +
            "來源：場地卡區";
    }

    /// <summary>
    /// 回傳可用文字，沒有內容時使用既有的空值提示。
    /// </summary>
    private string GetTextOrNone(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "無" : value;
    }
}
