using UnityEngine;

//==============================
// 棋盤管理類別 Board
// 功能：
// 1. 建立棋盤格子
// 2. 放置初始棋子
// 3. 與 LogicManager 互動
//==============================
public class Board : MonoBehaviour
{
    //==============================
    // 棋盤格子 Prefab
    //==============================
    public GameObject Square;

    //==============================
    // 棋子 Prefab 陣列
    // 索引對應：
    // 0~1 Pawn
    // 2~3 Rook
    // 4~5 Knight
    // 6~7 Bishop
    // 8~9 Queen
    // 10~11 King
    //==============================
    public GameObject[] PiecePrefabs;

    //==============================
    // 棋子材質 (白方 / 黑方)
    // 0 = 白方材質
    // 1 = 黑方材質
    //==============================
    public Material[] PieceMaterials;

    //==============================
    // 棋盤大小 (預設 8x8)
    //==============================
    public int Width = 8;
    public int Height = 8;

    //==============================
    // 遊戲邏輯管理器
    //==============================
    public LogicManager logicManager;

    //==============================
    // Unity 生命週期：遊戲開始
    //==============================
    /// <summary>
    /// 取得對局控制器、初始化對局，再依既有順序建立棋盤與棋子。
    /// </summary>
    void Start()
    {
        // 找到場景中的 LogicManager
        logicManager = Object.FindFirstObjectByType<LogicManager>();

        // 初始化遊戲邏輯
        logicManager.Initialize();

        // 生成棋盤格子
        GenerateBoard();

        // 放置初始棋子
        PlaceStartingPosition();
    }

    //==============================
    // 建立棋盤格子
    //==============================
    /// <summary>
    /// 建立棋盤格子，設定座標、材質與棋盤索引。
    /// </summary>
    public void GenerateBoard()
    {
        for (int i = 0; i < Width; i++)
        {
            for (int j = 0; j < Height; j++)
            {
                // 建立格子物件
                GameObject squareObject =
                    Instantiate(Square, new Vector3(i, 0, j), Quaternion.identity);

                // 設定父物件為 Board
                squareObject.transform.parent = this.transform;

                // 取得 Square 腳本並存入 LogicManager
                Square square = squareObject.GetComponent<Square>();
                logicManager.squares[i, j] = square;

                // 設定格子顏色 (黑白相間)
                Renderer renderer = square.GetComponent<Renderer>();
                if ((i + j) % 2 == 0)
                {
                    renderer.material.color = Color.black;
                }
                else
                {
                    renderer.material.color = Color.white;
                }
            }
        }
    }

    //==============================
    // 放置初始棋子
    //==============================
    /// <summary>
    /// 依標準開局配置放置雙方棋子。
    /// </summary>
    public void PlaceStartingPosition()
    {
        float pieceHeight = 0.12f;   // 棋子高度
        float pawnsHeight = 0.05f;   // 兵的高度

        // 放置 Pawn
        for (int i = 0; i < Width; i++)
        {
            // 白兵
            InstantiatePiece(PiecePrefabs[0], new Vector3(i, pawnsHeight, 1), PieceMaterials[0], "Pawn", true);

            // 黑兵
            InstantiatePiece(PiecePrefabs[1], new Vector3(i, pawnsHeight, 6), PieceMaterials[1], "Pawn", false);
        }

        // 白方 Rook
        InstantiatePiece(PiecePrefabs[2], new Vector3(0, pieceHeight, 0), PieceMaterials[0], "Rook", true);
        InstantiatePiece(PiecePrefabs[2], new Vector3(7, pieceHeight, 0), PieceMaterials[0], "Rook", true);

        // 黑方 Rook
        InstantiatePiece(PiecePrefabs[3], new Vector3(0, pieceHeight, 7), PieceMaterials[1], "Rook", false);
        InstantiatePiece(PiecePrefabs[3], new Vector3(7, pieceHeight, 7), PieceMaterials[1], "Rook", false);

        // 白方 Knight
        InstantiatePiece(PiecePrefabs[4], new Vector3(1, pieceHeight, 0), PieceMaterials[0], "Knight", true);
        InstantiatePiece(PiecePrefabs[4], new Vector3(6, pieceHeight, 0), PieceMaterials[0], "Knight", true);

        // 黑方 Knight
        InstantiatePiece(PiecePrefabs[5], new Vector3(1, pieceHeight, 7), PieceMaterials[1], "Knight", false);
        InstantiatePiece(PiecePrefabs[5], new Vector3(6, pieceHeight, 7), PieceMaterials[1], "Knight", false);

        // 白方 Bishop
        InstantiatePiece(PiecePrefabs[6], new Vector3(2, pieceHeight, 0), PieceMaterials[0], "Bishop", true);
        InstantiatePiece(PiecePrefabs[6], new Vector3(5, pieceHeight, 0), PieceMaterials[0], "Bishop", true);

        // 黑方 Bishop
        InstantiatePiece(PiecePrefabs[7], new Vector3(2, pieceHeight, 7), PieceMaterials[1], "Bishop", false);
        InstantiatePiece(PiecePrefabs[7], new Vector3(5, pieceHeight, 7), PieceMaterials[1], "Bishop", false);

        // 白方 Queen
        InstantiatePiece(PiecePrefabs[8], new Vector3(3, pieceHeight, 0), PieceMaterials[0], "Queen", true);

        // 黑方 Queen
        InstantiatePiece(PiecePrefabs[9], new Vector3(3, pieceHeight, 7), PieceMaterials[1], "Queen", false);

        // 白方 King
        InstantiatePiece(PiecePrefabs[10], new Vector3(4, pieceHeight, 0), PieceMaterials[0], "King", true);

        // 黑方 King
        InstantiatePiece(PiecePrefabs[11], new Vector3(4, pieceHeight, 7), PieceMaterials[1], "King", false);
    }

    //==============================
    // 建立棋子方法
    //==============================
    /// <summary>
    /// 建立指定種類與陣營的棋子，初始化座標並登錄到棋盤。
    /// </summary>
    public Piece InstantiatePiece(
        GameObject piecePrefab,
        Vector3 position,
        Material material,
        string pieceType,
        bool isWhite,
        CardDefinition cardDefinition = null
    )
    {
        // 建立棋子物件
        GameObject pieceObject = Instantiate(piecePrefab, position, Quaternion.identity);
        pieceObject.transform.parent = this.transform;

        // 設定棋子材質
        Renderer renderer = pieceObject.GetComponentInChildren<Renderer>();
        if (renderer != null) renderer.material = material;

        // 初始化棋子腳本
        Piece piece = pieceObject.GetComponent<Piece>();
        if (piece != null)
        {
            piece.Initialize(pieceType, isWhite);

            // 如果有卡牌效果，套用
            if (cardDefinition != null)
            {
                piece.ApplyCard(cardDefinition);
            }
        }

        return piece;
    }
}
