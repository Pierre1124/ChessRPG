using System.Collections.Generic;
using UnityEngine;

public enum BoardFieldEffectType
{
    None,
    Moat,
    ElectricNet
}

public class BoardFieldEffectZone
{
    public BoardFieldEffectType type;
    public Piece firstSource;
    public Piece secondSource;
    public List<Vector2Int> cells = new List<Vector2Int>();

    public bool Contains(Vector2Int cell)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] == cell)
            {
                return true;
            }
        }

        return false;
    }
}
