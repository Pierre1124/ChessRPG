using System;
using UnityEngine;

/// <summary>以明確資產引用讓進階場地房間隨遊戲一起打包。</summary>
public sealed class FieldRoomCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string fieldId;
        public GameObject roomPrefab;
    }

    public Entry[] rooms = Array.Empty<Entry>();

    public GameObject Find(string fieldId)
    {
        foreach (var entry in rooms)
            if (entry.fieldId == fieldId) return entry.roomPrefab;
        return null;
    }
}
