using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>在卡牌製作時列出定義及素材問題，不自動改寫卡牌內容。</summary>
[CustomEditor(typeof(ChessCard))]
public sealed class CardLibraryValidation : Editor
{
    private readonly List<string> findings = new List<string>();

    /// <summary>保留原本欄位，加入檢查按鈕及可直接閱讀的問題清單。</summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!EditorApplication.isPlaying && GUILayout.Button("檢查卡牌定義與素材"))
        {
            findings.Clear();
            // 重新建立清單，讓剛修改的 Inspector 素材立即納入檢查。
            ((ChessCard)target).InvalidateCardCache();
            Validate(((ChessCard)target).Cards, findings);
            if (findings.Count == 0) findings.Add("檢查完成，未發現問題。");
        }
        foreach (string finding in findings) EditorGUILayout.HelpBox(finding, MessageType.Info);
    }

    /// <summary>驗證唯一卡號、名稱、說明、職業定義及必要圖片，輸出卡號方便定位。</summary>
    public static void Validate(IReadOnlyList<CardDefinition> cards, List<string> results)
    {
        var ids = new HashSet<string>();
        foreach (CardDefinition card in cards)
        {
            if (card == null) { results.Add("卡牌清單包含空項目。"); continue; }
            string label = string.IsNullOrWhiteSpace(card.id) ? "未命名卡牌" : card.id;
            if (string.IsNullOrWhiteSpace(card.id) || !ids.Add(card.id)) results.Add(label + "：卡號空白或重複。");
            if (string.IsNullOrWhiteSpace(card.cardName)) results.Add(label + "：缺少名稱。");
            if (string.IsNullOrWhiteSpace(card.description)) results.Add(label + "：缺少效果說明。");
            if (card.cardImage == null) results.Add(label + "：缺少卡面圖片。");
            if (card.skillImage == null) results.Add(label + "：未指定技能圖示，遊戲將使用替代圖片。");
            if (card.cardType == CardType.JobChange && card.jobChangeDefinition == null)
                results.Add(label + "：職業卡缺少棋子定義。");
            if (card.cardType != CardType.Field && card.targetTypes == CardTargetType.None)
                results.Add(label + "：未設定可使用的棋子種類。");
        }
        for (int a = 1; a <= 12; a++)
            for (int b = a; b <= 12; b++)
            {
                string first = "F" + a.ToString("00"), second = "F" + b.ToString("00");
                string result = FieldCardRules.GetFusionId(first, second);
                if (result != null && (!ids.Contains(first) || !ids.Contains(second) || !ids.Contains(result)))
                    results.Add(first + " + " + second + "：合成配方引用不存在的卡牌。");
            }
    }
}
