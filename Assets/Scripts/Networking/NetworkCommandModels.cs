using System;
using System.Collections.Generic;
using UnityEngine;

public enum MultiplayerMode
{
    Local,
    Host,
    Client
}

public enum PlayerSide
{
    None,
    White,
    Black
}

public enum NetworkGameCommandKind
{
    MovePiece,
    DrawCard,
    PlayCardOnPiece,
    PlayFieldCard,
    RecycleCard,
    EndTurn
}

[Serializable]
public struct BoardCoordinate
{
    public int x;
    public int y;

    /// <summary>
    /// 建立整數棋盤座標。
    /// </summary>
    public BoardCoordinate(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public bool IsValid
    {
        get { return x >= 0 && x < 8 && y >= 0 && y < 8; }
    }

    /// <summary>
    /// 將網路棋盤座標轉為 Unity 二維座標。
    /// </summary>
    public Vector2 ToVector2()
    {
        return new Vector2(x, y);
    }

    /// <summary>
    /// 將 Unity 二維座標四捨五入為網路棋盤格座標。
    /// </summary>
    public static BoardCoordinate FromVector2(Vector2 coordinates)
    {
        return new BoardCoordinate(
            Mathf.RoundToInt(coordinates.x),
            Mathf.RoundToInt(coordinates.y)
        );
    }

    /// <summary>
    /// 回傳棋盤座標的可讀文字表示。
    /// </summary>
    public override string ToString()
    {
        return $"({x}, {y})";
    }
}

[Serializable]
public struct NetworkGameCommand
{
    public NetworkGameCommandKind kind;
    public int sequence;
    public bool isWhitePlayer;
    public BoardCoordinate from;
    public BoardCoordinate to;
    public string cardId;
    public string targetObjectName;
    public string fieldPlaceName;

    /// <summary>
    /// 建立包含序號、玩家陣營及起訖座標的移動命令資料。
    /// </summary>
    public static NetworkGameCommand Move(
        int sequence,
        bool isWhitePlayer,
        BoardCoordinate from,
        BoardCoordinate to
    )
    {
        return new NetworkGameCommand
        {
            kind = NetworkGameCommandKind.MovePiece,
            sequence = sequence,
            isWhitePlayer = isWhitePlayer,
            from = from,
            to = to
        };
    }

    /// <summary>
    /// 建立對指定棋子座標出牌的命令資料。
    /// </summary>
    public static NetworkGameCommand CardOnPiece(
        int sequence,
        bool isWhitePlayer,
        string cardId,
        BoardCoordinate target,
        string targetObjectName
    )
    {
        return new NetworkGameCommand
        {
            kind = NetworkGameCommandKind.PlayCardOnPiece,
            sequence = sequence,
            isWhitePlayer = isWhitePlayer,
            cardId = cardId,
            to = target,
            targetObjectName = targetObjectName
        };
    }

    /// <summary>
    /// 建立對指定場地欄位出牌的命令資料。
    /// </summary>
    public static NetworkGameCommand FieldCard(
        int sequence,
        bool isWhitePlayer,
        string cardId,
        string fieldPlaceName
    )
    {
        return new NetworkGameCommand
        {
            kind = NetworkGameCommandKind.PlayFieldCard,
            sequence = sequence,
            isWhitePlayer = isWhitePlayer,
            cardId = cardId,
            fieldPlaceName = fieldPlaceName
        };
    }

    /// <summary>
    /// 建立抽牌或回收等不需要棋盤起訖座標的命令。
    /// </summary>
    public static NetworkGameCommand Simple(
        NetworkGameCommandKind kind,
        int sequence,
        bool isWhitePlayer,
        string cardId = ""
    )
    {
        return new NetworkGameCommand
        {
            kind = kind,
            sequence = sequence,
            isWhitePlayer = isWhitePlayer,
            cardId = cardId
        };
    }
}

[Serializable]
public struct NetworkGameSnapshot
{
    public bool isWhiteTurn;
    public int whiteHealth;
    public int blackHealth;
    public string activeFieldCardIds;
    public string whiteHandCardIds;
    public string blackHandCardIds;
}

[Serializable]
public class NetworkDamageCalculationBatch
{
    public List<NetworkDamageCalculationSequence> sequences =
        new List<NetworkDamageCalculationSequence>();
}

[Serializable]
public class NetworkDamageCalculationSequence
{
    public bool damagedWhitePlayer;
    public bool isHealing;
    public bool isCapture;
    public int finalDamage;
    public Vector3 resultStartWorldPosition;
    public BoardCoordinate attacker = new BoardCoordinate(-1, -1);
    public BoardCoordinate target = new BoardCoordinate(-1, -1);
    public List<NetworkDamageCalculationStep> steps =
        new List<NetworkDamageCalculationStep>();
}

[Serializable]
public class NetworkDamageCalculationStep
{
    public string displayText;
    public Vector3 worldPosition;
    public Color color = Color.white;
    public bool usePieceIcon;
    public bool useTargetPieceIcon;
    public BoardCoordinate iconPiece = new BoardCoordinate(-1, -1);
    public int countingIcon;
    public int side;
}
