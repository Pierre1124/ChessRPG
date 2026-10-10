using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>場景內可編輯的可變數量物件樣板；執行時只複製已設計的階層。</summary>
public sealed class SceneObjectTemplates : MonoBehaviour
{
    [Serializable] public class Entry { public string key; public GameObject template; public GameObject sourcePrefab; }
    public List<Entry> templates = new List<Entry>();

    /// <summary>從本場景樣板產生副本，缺少配置時明確報錯，不再臨時拼裝介面。</summary>
    public static GameObject Spawn(string key, Transform parent = null, bool active = true)
    {
        var library = FindFirstObjectByType<SceneObjectTemplates>(FindObjectsInactive.Include);
        Entry entry = library != null ? library.templates.Find(item => item.key == key) : null;
        if (entry == null || entry.template == null) throw new InvalidOperationException("場景缺少物件樣板：" + key);
        GameObject copy = Instantiate(entry.template, parent, false);
        copy.name = key; copy.SetActive(active); return copy;
    }

    /// <summary>以卡牌資產中的特效引用查找場景樣板，讓特效外觀也能在 Scene 修改。</summary>
    public static GameObject SpawnEffect(GameObject sourcePrefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        var library = FindFirstObjectByType<SceneObjectTemplates>(FindObjectsInactive.Include);
        Entry entry = library != null ? library.templates.Find(item => item.sourcePrefab == sourcePrefab) : null;
        if (entry == null) throw new InvalidOperationException("場景缺少卡牌特效樣板：" + sourcePrefab.name);
        var copy = Spawn(entry.key, parent);
        copy.transform.SetPositionAndRotation(position, rotation); return copy;
    }
}
