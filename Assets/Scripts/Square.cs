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
    /// <summary>
    /// 取得棋盤格 Renderer 並記錄原始顏色。
    /// </summary>
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
    /// <summary>
    /// 將棋盤格的材質顏色設為指定高亮色。
    /// </summary>
    public void Highlight(Color highlightColor)
    {
        // 修改格子顏色
        squareRenderer.material.color = highlightColor;
    }

    //==============================
    // 取消高亮
    //==============================
    /// <summary>
    /// 將棋盤格的材質顏色還原為啟動時記錄的顏色。
    /// </summary>
    public void Unhighlight()
    {
        // 恢復原本顏色
        squareRenderer.material.color = originalColor;
    }
}
