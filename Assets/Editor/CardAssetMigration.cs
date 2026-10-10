using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>將既有素材與 Excel 快照遷移成正式卡牌資產；重跑不覆寫已存在資產。</summary>
public static class CardAssetMigration
{
    [Serializable] private sealed class Sheet { public List<Entry> cards; }
    [Serializable] private sealed class Entry
    {
        public string id, name, condition, description, sheet;
        public string[] tags;
        public int row;
    }

    /// <summary>建立缺少的卡牌資產，保留舊卡號與所有素材引用。</summary>
    [MenuItem("Tools/Chess/Cards/Migrate Spreadsheet Cards")]
    public static void Migrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先離開 Play Mode。");
        ChessCard library = UnityEngine.Object.FindFirstObjectByType<ChessCard>(FindObjectsInactive.Include);
        if (library == null) throw new InvalidOperationException("請開啟含 ChessCard 的 ChessScene。");
        var rows = JsonUtility.FromJson<Sheet>(File.ReadAllText("Assets/Editor/CardSpreadsheetSnapshot.json"));
        var legacy = library.BuildLegacyCardsForMigration().ToDictionary(c => c.id);
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Cards")) AssetDatabase.CreateFolder("Assets/Resources", "Cards");
        foreach (Entry row in rows.cards)
        {
            string path = "Assets/Resources/Cards/" + row.id + ".asset";
            if (AssetDatabase.LoadAssetAtPath<CardAsset>(path) != null) continue;
            if (!legacy.TryGetValue(row.id, out CardDefinition card)) throw new InvalidOperationException("缺少舊資料：" + row.id);
            card.cardName = row.name; card.conditionCost = row.condition; card.description = row.description;
            card.tags = new List<string>(row.tags); card.specialRuleId = card.id;
            foreach (StatusDefinition status in card.statusesToApply) status.statusName = row.name;
            ConfigureActions(card);
            if (card.id == "J09") { card.friendlyDamageBonus = 5; card.blocksHealing = true; card.requiresNoBoardCaptures = true; }
            if (card.id == "J10") { card.friendlyDamageBonus = 1; card.damageBonusUsesCaptureStacks = true;
                card.lastWillDamage = 1; card.lastWillDamageTags = DamageTag.Fire | DamageTag.LastWill | DamageTag.Skill; }
            if (card.id == "F10") card.damageTags = DamageTag.Field;
            if (card.id == "J10") card.damageTags = DamageTag.Fire | DamageTag.LastWill | DamageTag.Skill;
            if (card.id == "J07") card.randomDamageTypes = new[] { DamageTag.Fire, DamageTag.Electric };
            if (card.id == "F11") card.randomDamageTypes = new[] { DamageTag.Fire, DamageTag.Poison };
            if (card.id == "E09" || card.id == "E05") card.ruleNotes = "層數與重施規則待確認；遷移先保留原本 1 層及刷新時間行為。";
            var asset = ScriptableObject.CreateInstance<CardAsset>();
            asset.definition = card;
            asset.sourceReference = "Chess!.xlsx / " + row.sheet + " / row " + row.row + " (2026-10-09)";
            AssetDatabase.CreateAsset(asset, path);
        }
        AssetDatabase.SaveAssets();
        library.InvalidateCardCache();
        Verify();
    }

    /// <summary>將通用事件轉成可編輯的效果序列，特殊奪取仍使用專用規則。</summary>
    private static void ConfigureActions(CardDefinition card)
    {
        if (card.id == "E01" || card.id == "E03")
        {
            if (card.id == "E03") card.healthCost = 5;
            return;
        }
        if (card.cardType != CardType.Event) return;
        card.useDataActions = true;
        switch (card.id)
        {
            case "E02": Add(card, CardPlayActionType.Heal, 5); break;
            case "E04": Add(card, CardPlayActionType.Damage, 5); break;
            case "E07": case "E08": Add(card, CardPlayActionType.ApplyStatusesToBoard); break;
            case "E10":
                card.minimumTargetHealth = 50;
                Add(card, CardPlayActionType.ApplyStatuses); Add(card, CardPlayActionType.Damage, 10); break;
            case "E11":
                card.maximumTargetHealth = 50;
                Add(card, CardPlayActionType.ApplyStatuses); Add(card, CardPlayActionType.Heal, 40); break;
            default: Add(card, CardPlayActionType.ApplyStatuses); break;
        }
    }

    /// <summary>加入一個可直接由 Inspector 修改的即時效果。</summary>
    private static void Add(CardDefinition card, CardPlayActionType type, int amount = 0)
    {
        card.playActions.Add(new CardPlayAction { type = type, amount = amount });
    }

    /// <summary>验证卡號、快照、素材引用及執行期副本隔離。</summary>
    [MenuItem("Tools/Chess/Cards/Verify Card Assets")]
    public static void Verify()
    {
        var lines = new List<string>();
        var assets = Resources.LoadAll<CardAsset>("Cards");
        var source = JsonUtility.FromJson<Sheet>(File.ReadAllText("Assets/Editor/CardSpreadsheetSnapshot.json"));
        if (assets.Length != 36 || assets.Select(a => a.definition.id).Distinct().Count() != 36)
            throw new InvalidOperationException("預期 36 張唯一卡牌。");
        foreach (Entry entry in source.cards)
        {
            CardAsset asset = assets.Single(a => a.definition.id == entry.id);
            if (asset.definition.cardName != entry.name || asset.definition.description != entry.description || asset.definition.conditionCost != entry.condition)
                throw new InvalidOperationException(entry.id + " 與 Excel 快照不符。");
            CardDefinition a = asset.CreateDefinition(), b = asset.CreateDefinition();
            if (ReferenceEquals(a, b) || ReferenceEquals(a.statusesToApply, b.statusesToApply) || a.cardImage != asset.definition.cardImage)
                throw new InvalidOperationException(entry.id + " 副本隔離或素材引用失敗。");
            if (a.statusesToApply.Count > 0)
            {
                a.statusesToApply[0].durationTurns = 999;
                if (b.statusesToApply[0].durationTurns == 999 || asset.definition.statusesToApply[0].durationTurns == 999)
                    throw new InvalidOperationException(entry.id + " 共用可變狀態。");
            }
            if (entry.id == "F10" && (a.damageTags & (DamageTag.Physical | DamageTag.Fire | DamageTag.Poison | DamageTag.Curse | DamageTag.Electric | DamageTag.True)) != 0)
                throw new InvalidOperationException("罪金之域必須為無屬性傷害。");
            if (entry.id == "J07" || entry.id == "F11")
            {
                var randomState = UnityEngine.Random.state;
                try
                {
                    var seen = new HashSet<DamageTag>();
                    UnityEngine.Random.InitState(20261009);
                    for (int i = 0; i < 64; i++)
                    {
                        DamageTag tags = a.ResolveDamageTags(DamageTag.Skill);
                        DamageTag element = tags & ~DamageTag.Skill;
                        if (!a.randomDamageTypes.Contains(element))
                            throw new InvalidOperationException(entry.id + " 必須隨機擇一屬性，且保留來源標記。");
                        seen.Add(element);
                    }
                    if (seen.Count != 2) throw new InvalidOperationException(entry.id + " 隨機選擇未涵蓋兩種屬性。");
                }
                finally { UnityEngine.Random.state = randomState; }
            }
            lines.Add("PASS " + entry.id + " " + entry.name + " metadata, references, isolated copy");
        }
        Directory.CreateDirectory("output");
        File.WriteAllLines("output/card-assets-regression.txt", lines);
        Debug.Log("Card assets verified: " + assets.Length);
    }
}
