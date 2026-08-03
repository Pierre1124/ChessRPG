using UnityEngine;

public class Square : MonoBehaviour
{
    //==============================
    // 格子的 Renderer
    // 用來改變格子顏色
    //==============================
    private Renderer squareRenderer;

    //==============================
    // 原本顏色
    // 用於取消高亮時恢復
    //==============================
    private Color originalColor;

    //==============================
    // 初始化
    //==============================
    void Start()
    {
        // 取得 Renderer 元件
        squareRenderer = GetComponent<Renderer>();

        // 記錄初始顏色
        originalColor = squareRenderer.material.color;
    }

    //==============================
    // 高亮格子
    //==============================
    public void Highlight(Color highlightColor)
    {
        // 修改格子顏色
        squareRenderer.material.color = highlightColor;
    }

    //==============================
    // 取消高亮
    //==============================
    public void Unhighlight()
    {
        // 恢復原本顏色
        squareRenderer.material.color = originalColor;
    }
}
