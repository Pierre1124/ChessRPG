using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>在獨立 Play Mode 驗證集中傷害面板、結算時機及中斷清理。</summary>
[InitializeOnLoad]
public static class DamagePanelRegression
{
    private const string Key = "ChessRPG.DamagePanelRegression";
    private const string Output = "output/damage-panel-regression.txt";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static int stage, applied, captured, blackBefore, expectedDamage;
    private static bool sawPending, sawParallel, sawIncrease, sawDecrease;
    private static double deadline, cancelDeadline;
    private static LogicManager logic;
    private static DamageCalculationVisualizer visualizer;
    private static CardHandManager hand;
    private static Transform health;
    private static Vector3 healthOrigin;

    /// <summary>重載後接續測試並在結束時還原播放場景。</summary>
    static DamagePanelRegression()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Restore;
    }
    /// <summary>啟動離線測試，不修改儲存的場景或玩家偏好。</summary>
    [MenuItem("Tools/Chess/Run Damage Panel Regression")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Photon.Pun.PhotonNetwork.IsConnected) return;
        SessionState.SetString(Key + "Scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key, true); stage = 0;
        Directory.CreateDirectory("output"); File.WriteAllText(Output, "Unified damage panel regression\n");
        LogicManager.SetNextGameMode(false);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/ChessScene.unity");
        EditorApplication.isPlaying = true;
    }
    /// <summary>逐幀驗證演出途中不扣血，以及完成、批次與取消只執行應有回呼。</summary>
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
        try
        {
            if (stage == 0)
            {
                logic = Object.FindFirstObjectByType<LogicManager>();
                visualizer = Object.FindFirstObjectByType<DamageCalculationVisualizer>();
                if (logic == null || visualizer == null || logic.boardMap[0, 1] == null || logic.IsOperationLocked) return;
                deadline = EditorApplication.timeSinceStartup + 30;
                foreach (string name in new[] { "stepDuration", "captureDelay", "flyDuration" })
                    typeof(DamageCalculationVisualizer).GetField(name, Private).SetValue(visualizer, 0.2f);
                var battle = new CardBattleSystem(logic);
                var context = new DamageContext { source = logic.boardMap[0, 1], target = logic.boardMap[0, 6] };
                var actual = (DamageCalculationSequence)typeof(CardBattleSystem).GetMethod("BuildCaptureDamageSequence", Private).Invoke(battle, new object[] { context });
                Check(actual.steps.All(s => s.hasContribution) && actual.steps.Sum(s => s.contribution) == actual.finalDamage,
                    "Real capture builder provides signed contributions matching final damage");
                var wire = new NetworkDamageCalculationStep { title = "測試來源", detail = "8 → 5", hasContribution = true, contribution = -3,
                    sourceCardId = "J06", modifierPhase = (int)DamageModifierPhase.Decrease };
                var restored = JsonUtility.FromJson<NetworkDamageCalculationStep>(JsonUtility.ToJson(wire));
                Check(restored.title == wire.title && restored.hasContribution && restored.contribution == -3 && restored.sourceCardId == "J06" && restored.modifierPhase == (int)DamageModifierPhase.Decrease, "Network payload preserves source and signed change");
                applied = captured = 0; sawPending = false; blackBefore = logic.blackHealth;
                var fields = (System.Collections.Generic.List<CardDefinition>)typeof(LogicManager).GetField("activeFieldCards", Private).GetValue(logic);
                var database = visualizer.imageDatabase;
                VerifyPaladinAndWeakness(battle, database, fields);
                fields.Add(database.GetCard("F01")); fields.Add(database.GetCard("F05"));
                logic.boardMap[1, 7].ApplyCard(database.GetCard("J06"));
                var fire = new DamageContext { source = logic.boardMap[0, 1], target = logic.boardMap[0, 6],
                    sourceCard = database.GetCard("E04"), baseDamage = 3, tags = DamageTag.Fire };
                var sequence = (DamageCalculationSequence)typeof(CardBattleSystem).GetMethod("BuildEffectDamageSequence", Private).Invoke(battle, new object[] { fire });
                Check(sequence.finalDamage == 2 && sequence.steps.Count(s => s.modifierPhase == DamageModifierPhase.Increase) == 1 &&
                    sequence.steps.Count(s => s.modifierPhase == DamageModifierPhase.Decrease) == 2,
                    "Fireball with volcano, kingdom and adjacent paladin retains final damage 2 and groups 1 increase / 2 reductions");
                Check(sequence.steps.Single(s => s.sourceCardId == "J06").worldPosition == logic.boardMap[1, 7].transform.position,
                    "Paladin cue anchors to supporting knight rather than damaged pawn");
                expectedDamage = sequence.finalDamage; sawIncrease = sawDecrease = false;
                logic.PlayDamageCalculation(sequence, () => captured++, () => { applied++; battle.DamagePlayer(false, sequence.finalDamage, false); });
                Check(captured == 1 && applied == 0, "Attack visual starts before sources without applying HP");
                stage = 1;
            }
            else if (stage == 1)
            {
                DamageResolutionPanel panel = Object.FindFirstObjectByType<DamageResolutionPanel>();
                if (applied == 0)
                {
                    CheckSilent(logic.blackHealth == blackBefore, "Health changed before sources completed");
                    if (visualizer.CurrentModifierPhase == DamageModifierPhase.Increase)
                    {
                        CheckSilent(!sawDecrease && visualizer.VisibleModifierCount == 1, "Increase must precede reductions"); sawIncrease = true;
                    }
                    if (visualizer.CurrentModifierPhase == DamageModifierPhase.Decrease)
                    {
                        CheckSilent(sawIncrease && visualizer.VisibleModifierCount == 2, "Both reductions must appear together"); sawDecrease = true;
                        var panels = Object.FindObjectsByType<DamageResolutionPanel>(FindObjectsSortMode.None);
                        CheckSilent(panels.All(p => p.DisplayedTotal == ""), "Sources must not show accumulated damage");
                    }
                    sawPending |= panel != null;
                }
                else
                {
                    Check(applied == 1 && captured == 1 && sawPending && sawIncrease && sawDecrease, "Single sequence applies once after increase then simultaneous reductions");
                    Check(logic.blackHealth == blackBefore - expectedDamage && !logic.IsDamageCalculationBusy, "Damage applies final amount and releases operation lock");
                    Check(panel == null, "Completed panel is hidden before next calculation");
                    stage = 2;
                }
            }
            else if (stage == 2)
            {
                applied = captured = 0; blackBefore = logic.blackHealth; logic.whiteHealth = 80;
                typeof(LogicManager).GetMethod("BeginDamageCalculationBatch", Private).Invoke(logic, null);
                var battle = new CardBattleSystem(logic);
                var damage = Sequence(false, new[] { 3, -1 });
                var healing = Sequence(true, new[] { 3, 2, -1 });
                var zero = Sequence(false, new[] { 2, -2 });
                logic.PlayDamageCalculation(damage, () => captured++, () => { applied++; battle.DamagePlayer(false, 2, false); });
                logic.PlayDamageCalculation(healing, () => captured++, () => { applied++; battle.HealPlayer(true, 4, false); });
                logic.PlayDamageCalculation(zero, () => captured++, () => applied++);
                typeof(LogicManager).GetMethod("EndDamageCalculationBatch", Private).Invoke(logic, null);
                sawParallel = false;
                stage = 3;
            }
            else if (stage == 3)
            {
                if (applied == 0)
                {
                    CheckSilent(logic.blackHealth == blackBefore && logic.whiteHealth == 80, "Batch applied HP before all sources completed");
                    var panels = Object.FindObjectsByType<DamageResolutionPanel>(FindObjectsSortMode.None);
                    if (visualizer.CurrentModifierPhase == DamageModifierPhase.Decrease && panels.Length == 3) sawParallel = true;
                }
                else
                {
                    Check(sawParallel, "All three target UIs advance together before any HP settlement");
                    Check(applied == 3 && captured == 3, "Mixed batch including zero result invokes every callback exactly once");
                    Check(logic.blackHealth == blackBefore - 2 && logic.whiteHealth == 84 && !logic.IsDamageCalculationBusy,
                        "Damage and healing retain separate results and release queue lock");
                    applied = captured = 0;
                    hand = Object.FindFirstObjectByType<CardHandManager>();
                    typeof(CardHandManager).GetField("isUsingCardAnimationPlaying", Private).SetValue(hand, true);
                    visualizer.Play(Sequence(false, new[] { 5, 2 }), () => captured++, () => applied++);
                    cancelDeadline = EditorApplication.timeSinceStartup + 0.4;
                    stage = 4;
                }
            }
            else if (stage == 4 && EditorApplication.timeSinceStartup > cancelDeadline)
            {
                Check(captured == 0 && applied == 0 && visualizer.VisibleModifierCount == 0,
                    "Damage animation waits until using-card animation finishes");
                typeof(CardHandManager).GetField("isUsingCardAnimationPlaying", Private).SetValue(hand, false);
                cancelDeadline = EditorApplication.timeSinceStartup + 0.08;
                stage = 6;
            }
            else if (stage == 6 && EditorApplication.timeSinceStartup > cancelDeadline)
            {
                Check(captured == 1 && applied == 0, "Initial effect starts only after card animation releases");
                health = (Transform)typeof(DamageCalculationVisualizer).GetField("blackHealthTarget", Private).GetValue(visualizer);
                healthOrigin = health.localPosition;
                typeof(DamageCalculationVisualizer).GetMethod("StartHealthShake", Private).Invoke(visualizer, new object[] { health });
                visualizer.DisableForClassicChess();
                Check(health.localPosition == healthOrigin, "Cancellation restores health bar position without drift");
                cancelDeadline = EditorApplication.timeSinceStartup + 1.5; stage = 7;
            }
            else if (stage == 7 && EditorApplication.timeSinceStartup > cancelDeadline)
            {
                Check(applied == 0 && Object.FindFirstObjectByType<DamageResolutionPanel>() == null, "Classic-mode cancellation hides panel and prevents delayed settlement");
                File.AppendAllText(Output, "PASS: all damage panel checks completed\n"); EditorApplication.isPlaying = false; stage = 5;
            }
            if (stage != 5 && deadline > 0 && EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Damage UI did not finish");
        }
        catch (Exception ex) { File.AppendAllText(Output, "FAIL: " + ex + "\n"); EditorApplication.isPlaying = false; }
    }
    /// <summary>使用正式資產重現聖騎士走法及虛弱火球，區分易傷與場地減傷。</summary>
    private static void VerifyPaladinAndWeakness(CardBattleSystem battle, ChessCard database,
        System.Collections.Generic.List<CardDefinition> fields)
    {
        Piece knight = logic.boardMap[1, 0];
        var moves = knight.GetLegalMoves(); var attacks = knight.GetAttackedFields();
        knight.ApplyCard(database.GetCard("J06"));
        Check(moves.Count == 2 && moves.All(knight.GetLegalMoves().Contains) && knight.GetLegalMoves().Count == moves.Count,
            "Equipped paladin retains both initial knight moves");
        Check(attacks.Count == knight.GetAttackedFields().Count && attacks.All(knight.GetAttackedFields().Contains),
            "Equipped paladin retains knight attack map for check detection");
        knight.ApplyCard(null);
        foreach (CardDefinition card in CardAssetLibrary.LoadDefinitions().Where(c => c.cardType == CardType.JobChange &&
            (c.jobChangeDefinition == null || !c.jobChangeDefinition.HasMoveRules)))
        {
            // 此處只檢查走法選擇，避免其他轉職卡的裝備效果改動測試棋盤。
            knight.cardDefinition = card;
            Check(!(bool)typeof(Piece).GetProperty("UsesDefinitionRules", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(knight),
                card.id + " empty serialized definition does not suppress standard movement");
        }
        knight.cardDefinition = null;
        Piece pawn = logic.boardMap[0, 6];
        pawn.ApplyCard(database.GetCard("J01"));
        Check((bool)typeof(Piece).GetProperty("UsesDefinitionRules", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pawn),
            "Long spearman retains its explicit custom movement");
        pawn.ApplyCard(null);
        Piece paladin = logic.boardMap[1, 7]; paladin.ApplyCard(database.GetCard("J06"));
        foreach (StatusDefinition status in database.GetCard("E06").statusesToApply) pawn.ApplyStatus(status, null, false);
        CardDefinition fireball = database.GetCard("E04");
        var context = new DamageContext { target = pawn, sourceCard = fireball,
            baseDamage = fireball.playActions.First(a => a.type == CardPlayActionType.Damage).amount,
            tags = fireball.damageTags };
        Func<DamageCalculationSequence> build = () => (DamageCalculationSequence)typeof(CardBattleSystem)
            .GetMethod("BuildEffectDamageSequence", Private).Invoke(battle, new object[] { context });
        fields.Clear();
        Check(build().finalDamage == 5, "Fireball 5 + weakness vulnerability 1 - paladin 1 = 5 without field reduction");
        fields.Add(database.GetCard("F06"));
        Check(build().finalDamage == 4, "Empire adds reduction 1: weakness fireball against protected pawn = 4");
        fields.Clear(); fields.Add(database.GetCard("F01")); fields.Add(database.GetCard("F05"));
        Check(build().finalDamage == 5, "Volcano + kingdom + paladin + weakness fireball = 5");
        fields.Clear(); pawn.ClearRpgState(); paladin.ApplyCard(null);
    }

    /// <summary>建立含加成與減免的展示序列，數值總和與最終結果一致。</summary>
    private static DamageCalculationSequence Sequence(bool healing, int[] changes)
    {
        var sequence = new DamageCalculationSequence { target = logic.boardMap[0, 6], attacker = logic.boardMap[0, 1],
            resultStartWorldPosition = logic.boardMap[0, 6].transform.position,
            isHealing = healing, damagedWhitePlayer = healing, finalDamage = Mathf.Max(0, changes.Sum()) };
        for (int i = 0; i < changes.Length; i++) sequence.steps.Add(new DamageCalculationStep {
            modifierPhase = i == 0 ? DamageModifierPhase.None : changes[i] > 0 ? DamageModifierPhase.Increase : DamageModifierPhase.Decrease,
            title = (healing ? "治療" : string.Join(",", changes)) + "來源 " + (i + 1), hasContribution = true, contribution = changes[i], displayText = Mathf.Abs(changes[i]).ToString(),
            worldPosition = logic.boardMap[0, 1].transform.position, countingIcon = healing ? DamageCountingIcon.Heal : DamageCountingIcon.Attack });
        return sequence;
    }
    /// <summary>記錄通過的檢查，失敗時中止測試。</summary>
    private static void Check(bool condition, string message) { CheckSilent(condition, message); File.AppendAllText(Output, "PASS: " + message + "\n"); }
    /// <summary>檢查逐幀不變條件，避免每幀輸出重複訊息。</summary>
    private static void CheckSilent(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    /// <summary>退出 Play Mode 後還原原本場景設定。</summary>
    private static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
        string scene = SessionState.GetString(Key + "Scene", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(scene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
        SessionState.SetBool(Key, false); stage = 0; deadline = 0;
    }
}
