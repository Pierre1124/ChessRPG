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

    public BoardCoordinate(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public bool IsValid
    {
        get { return x >= 0 && x < 8 && y >= 0 && y < 8; }
    }

    public Vector2 ToVector2()
    {
        return new Vector2(x, y);
    }

    public static BoardCoordinate FromVector2(Vector2 coordinates)
    {
        return new BoardCoordinate(
            Mathf.RoundToInt(coordinates.x),
            Mathf.RoundToInt(coordinates.y)
        );
    }

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
