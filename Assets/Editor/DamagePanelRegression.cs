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
    private static bool sawActivation;
    private static Piece eventTarget;

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
                VerifyPieceAnchoring();
                VerifyFeedbackInterfaces();
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
                VerifyCombinedModifiers(battle, database);
                fields.Add(database.GetCard("F01")); fields.Add(database.GetCard("F05"));
                var fire = new DamageContext { source = logic.boardMap[0, 1], target = logic.boardMap[0, 6],
                    sourceCard = database.GetCard("E04"), baseDamage = 5, tags = DamageTag.Fire };
                var sequence = (DamageCalculationSequence)typeof(CardBattleSystem).GetMethod("BuildEffectDamageSequence", Private).Invoke(battle, new object[] { fire });
                Check(sequence.finalDamage == 5 && sequence.steps.Count(s => s.modifierPhase == DamageModifierPhase.Increase) == 1 &&
                    sequence.steps.Count(s => s.modifierPhase == DamageModifierPhase.Decrease) == 1,
                    "Fireball with volcano and kingdom retains final damage 5 and two modifier phases");
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
                        CheckSilent(!sawDecrease && visualizer.VisibleModifierCount == 1 && visualizer.DisplayedFinalDamage == "最終傷害:5", "Increase starts with base damage 5"); sawIncrease = true;
                    }
                    if (visualizer.CurrentModifierPhase == DamageModifierPhase.Decrease)
                    {
                        CheckSilent(sawIncrease && visualizer.VisibleModifierCount == 1 && visualizer.DisplayedFinalDamage == "最終傷害:6", "Volcano updates fixed damage to 6 before kingdom reduction"); sawDecrease = true;
                        var panels = Object.FindObjectsByType<DamageResolutionPanel>(FindObjectsSortMode.None);
                        CheckSilent(panels.All(p => p.DisplayedTotal == "" && p.GetComponentsInChildren<TMPro.TMP_Text>().Length == 0), "Source popups must show icons only");
                    }
                    if (panel != null) CheckSilent(panel.AttachedPiece != null && panel.transform.parent == panel.AttachedPiece, "Damage icons must be children of their piece during playback");
                    sawPending |= panel != null;
                }
                else
                {
                    Check(applied == 1 && captured == 1 && sawPending && sawIncrease && sawDecrease, "Single sequence applies once after increase then simultaneous reductions");
                    Check(logic.blackHealth == blackBefore - expectedDamage && !logic.IsDamageCalculationBusy, "Damage applies final amount and releases operation lock");
                    Check(panel == null && visualizer.DisplayedFinalDamage == "最終傷害:5", "Kingdom completes fixed damage progression 5 -> 6 -> 5");
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
                visualizer.gameObject.SetActive(true);
                Piece cleric = logic.boardMap[2, 0]; cleric.ApplyCard(visualizer.imageDatabase.GetCard("J08"));
                var battle = new CardBattleSystem(logic);
                var heal = (DamageCalculationSequence)typeof(CardBattleSystem).GetMethod("BuildHealSequence", Private)
                    .Invoke(battle, new object[] { cleric, true, 1, 1, cleric.cardDefinition.skillImage });
                Check(heal.steps[0].modifierPhase == DamageModifierPhase.Activation && heal.steps[0].sourceCardId == "J08",
                    "Cleric healing retains its activation icon without requiring a damage modifier");
                var electric = new DamageContext { source = logic.boardMap[0, 0], target = logic.boardMap[0, 6],
                    sourceCard = visualizer.imageDatabase.GetCard("J04"), baseDamage = 5, tags = DamageTag.Electric };
                var grid = (DamageCalculationSequence)typeof(CardBattleSystem).GetMethod("BuildFixedDamageSequence", Private).Invoke(battle, new object[] { electric });
                Check(grid.steps[0].modifierPhase == DamageModifierPhase.Activation && grid.steps[0].sourceCardId == "J04",
                    "Electric grid damage retains source card for local and remote icon lookup");
                applied = 0; sawActivation = false;
                visualizer.Play(heal, null, () => applied++); stage = 8;
            }
            else if (stage == 8)
            {
                if (visualizer.CurrentModifierPhase == DamageModifierPhase.Activation)
                    sawActivation |= visualizer.VisibleModifierCount == 1 && Object.FindFirstObjectByType<DamageResolutionPanel>() != null;
                if (applied == 0) return;
                Check(sawActivation && applied == 1, "Cleric activation icon is visible before heal settlement");
                hand = Object.FindFirstObjectByType<CardHandManager>(); eventTarget = logic.boardMap[0, 1];
                Check(hand.PlayCardOnPieceAsAuthority(true, "E06", new BoardCoordinate(0, 1), false), "Weakness event accepted for presentation");
                Check(!eventTarget.Statuses.Any(s => s.definition.sourceCardId == "E06"), "Accepted event does not apply status before animation");
                hand.PlayUsingCardAnimationOnPiece("E06", new BoardCoordinate(0, 1));
                stage = 9;
            }
            else if (stage == 9)
            {
                if (hand.IsUsingCardAnimationPlaying)
                {
                    CheckSilent(!eventTarget.Statuses.Any(s => s.definition.sourceCardId == "E06"), "Status applied before shrink completed");
                    return;
                }
                if (logic.IsOperationLocked) return;
                Check(eventTarget.Statuses.Count(s => s.definition.sourceCardId == "E06") == 1,
                    "Event applies exactly once after shrink, including duplicate presentation request");
                PrepareElectricTraversal();
                stage = 10; deadline = EditorApplication.timeSinceStartup + 25;
            }
            else if (stage == 10 && !logic.IsDamageCalculationBusy)
            {
                Check(logic.whiteHealth == 99 && logic.blackHealth == 100, "Friendly bishop crosses electric token and takes exactly 1 HP damage");
                logic.isWhiteTurn = false;
                Piece bishop = logic.boardMap[5, 7];
                Check(bishop.GetLegalMoves().Contains(new Vector2(0, 2)), "Enemy bishop can move past electric token");
                bishop.Move(new Vector2(0, 2)); stage = 11;
            }
            else if (stage == 11 && !logic.IsDamageCalculationBusy)
            {
                Check(logic.whiteHealth == 99 && logic.blackHealth == 99, "Enemy bishop crossing the same grid also takes exactly 1 HP damage");
                logic.boardMap[0, 3].CardRuntime.activeSkillDisabled = true;
                logic.boardMap[7, 3].CardRuntime.activeSkillDisabled = true;
                Check(logic.CollectElectricNetCrossings(logic.boardMap[0, 2], new Vector2(0, 2), new Vector2(2, 4)).Count == 0,
                    "Closed electric grid does not create traversal damage");
                File.AppendAllText(Output, "PASS: all damage panel checks completed\n"); EditorApplication.isPlaying = false; stage = 5;
            }
            if (stage != 5 && deadline > 0 && EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Damage UI did not finish");
        }
        catch (Exception ex) { File.AppendAllText(Output, "FAIL: " + ex + "\n"); EditorApplication.isPlaying = false; }
    }
    /// <summary>驗證空素材安全、狀態素材複製、待機單一實例、觸發及移除清理。</summary>
    private static void VerifyFeedbackInterfaces()
    {
        Check(Object.FindFirstObjectByType<BattleFeedback>() != null, "Scene has shared feedback audio interface");
        var template = new GameObject("Feedback test prefab"); template.SetActive(false);
        var asset = ScriptableObject.CreateInstance<CardAsset>();
        asset.definition.id = "FeedbackTest";
        asset.definition.statusesToApply.Add(new StatusDefinition { statusName = "FeedbackTest", idleEffectPrefab = template, triggerEffectPrefab = template });
        var definition = asset.CreateDefinition().statusesToApply[0];
        Check(definition.idleEffectPrefab == template && definition.triggerEffectPrefab == template, "Card clone preserves status effect references");
        Piece owner = logic.boardMap[0, 1]; owner.ApplyStatus(definition, owner, false);
        var status = owner.Statuses.Last(); var idle = status.idleEffectInstance;
        BattleFeedback.StartStatusIdle(status);
        Check(idle != null && idle.transform.parent == owner.transform && status.idleEffectInstance == idle, "Status idle effect attaches once to its piece");
        int before = owner.transform.childCount;
        BattleFeedback.PlayStatusTrigger(status);
        Check(owner.transform.childCount == before + 1, "Status trigger instantiates configured effect with empty sound safely");
        owner.RemoveStatusesByCardId("FeedbackTest");
        Check(status.idleEffectInstance == null && !idle.activeSelf, "Removing status immediately disables idle effect");
        owner.ApplyStatus(definition, owner, false); status = owner.Statuses.Last();
        owner.ClearRpgState();
        Check(status.idleEffectInstance == null, "Classic cleanup removes idle effects");
        Object.DestroyImmediate(asset); Object.DestroyImmediate(template);
    }

    /// <summary>驗證棋子子階層、移動跟隨、沒有棋子的座標備援與回收位置。</summary>
    private static void VerifyPieceAnchoring()
    {
        var panel = DamageResolutionPanel.Create(null);
        Transform pool = panel.transform.parent;
        Piece piece = logic.boardMap[0, 1];
        Vector3 original = piece.transform.position;
        panel.ShowSource(new DamageCalculationStep { iconPiece = piece, worldPosition = original }, null, 0);
        Check(panel.transform.parent == piece.transform && panel.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace,
            "Damage icon uses world canvas under source piece");
        Vector3 initial = panel.transform.position;
        piece.transform.position += Vector3.right;
        typeof(DamageResolutionPanel).GetMethod("LateUpdate", Private).Invoke(panel, null);
        Check(Mathf.Abs(panel.transform.position.x - initial.x - 1f) < 0.001f, "Damage icon follows piece movement");
        piece.transform.position = original;
        panel.Hide();
        Check(panel.transform.parent == pool && !panel.gameObject.activeSelf, "Hidden damage icon returns to its pool");
        panel.ShowSource(new DamageCalculationStep { worldPosition = new Vector3(3, 0, 4) }, null, 0);
        Check(panel.transform.parent == null && Mathf.Abs(panel.transform.position.x - 3f) < 0.001f && Mathf.Abs(panel.transform.position.z - 4f) < 0.001f,
            "Removed piece falls back to saved board coordinates");
        panel.Hide(); Object.DestroyImmediate(panel.gameObject);
    }

    /// <summary>建立橫向電網，以實際棋子 Move 驗證敵我均可穿越且觸發傷害。</summary>
    private static void PrepareElectricTraversal()
    {
        ((System.Collections.Generic.List<CardDefinition>)typeof(LogicManager).GetField("activeFieldCards", Private).GetValue(logic)).Clear();
        foreach (Piece piece in logic.boardMap) if (piece != null) piece.ClearRpgState();
        foreach (Vector2Int cell in new[] { new Vector2Int(3, 1), new Vector2Int(4, 6) })
        {
            Object.Destroy(logic.boardMap[cell.x, cell.y].gameObject); logic.boardMap[cell.x, cell.y] = null;
        }
        foreach (int x in new[] { 0, 7 })
        {
            Piece rook = logic.boardMap[x, 0]; logic.boardMap[x, 0] = null;
            rook.transform.position = new Vector3(x, rook.transform.position.y, 3); logic.boardMap[x, 3] = rook;
        }
        logic.boardMap[0, 3].ApplyCard(visualizer.imageDatabase.GetCard("J04"));
        Check(logic.GetActiveBoardFieldZones().Any(z => z.type == BoardFieldEffectType.ElectricNet), "Aligned rooks create electric token fixture");
        logic.whiteHealth = logic.blackHealth = 100; logic.isWhiteTurn = true;
        Piece mover = logic.boardMap[2, 0];
        Check(mover.GetLegalMoves().Contains(new Vector2(6, 4)), "Friendly bishop can move past electric token");
        mover.Move(new Vector2(6, 4));
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

    /// <summary>驗證同方魔法師、移動中的血腥瑪莉、聖騎士本體及同源多效果。</summary>
    private static void VerifyCombinedModifiers(CardBattleSystem battle, ChessCard database)
    {
        Piece queen = logic.boardMap[3, 0], mage = logic.boardMap[2, 0], target = logic.boardMap[1, 7];
        var runtime = typeof(Piece).GetField("cardRuntime", Private);
        // 僅裝入光環資料，避免裝備血腥瑪莉的移除國王副作用污染後續測試。
        runtime.SetValue(queen, new CardRuntimeState(database.GetCard("J09")));
        Func<DamageContext, DamageCalculationSequence> effect = context => (DamageCalculationSequence)typeof(CardBattleSystem)
            .GetMethod("BuildEffectDamageSequence", Private).Invoke(battle, new object[] { context });
        var hit = new DamageContext { source = mage, target = target, sourceCard = database.GetCard("J07"), baseDamage = 1, tags = DamageTag.Fire | DamageTag.Skill };
        Check(effect(hit).finalDamage == 6, "Same-side Bloody Mary increases mage fire 1 to 6");
        hit.tags = DamageTag.Electric | DamageTag.Skill;
        Check(effect(hit).finalDamage == 1, "Mage electric remains fixed 1 despite Bloody Mary");
        target.ApplyCard(database.GetCard("J06"));
        hit.tags = DamageTag.Fire | DamageTag.Skill;
        Check(effect(hit).finalDamage == 5, "Paladin protects itself: mage fire 1 + Mary 5 - reduction 1");
        foreach (var status in database.GetCard("E07").statusesToApply) queen.ApplyStatus(status, queen, false);
        logic.boardMap[3, 0] = null;
        try
        {
            var capture = (DamageCalculationSequence)typeof(CardBattleSystem).GetMethod("BuildCaptureDamageSequence", Private)
                .Invoke(battle, new object[] { new DamageContext { source = queen, target = target, tags = DamageTag.Capture | DamageTag.Physical } });
            Check(capture.finalDamage == 5 + battle.GetEffectiveValue(target), "Moving Mary retains own 5 + berserker 1 - paladin 1; value remains unreduced");
            Check(capture.steps.Any(s => s.sourceCardId == "E07") && capture.steps.Any(s => s.sourceCardId == "J09"), "Berserker and Mary preserve distinct source identities");
            var routine = (System.Collections.IEnumerator)typeof(DamageCalculationVisualizer).GetMethod("PlayModifierPhase", Private)
                .Invoke(visualizer, new object[] { new[] { capture }, DamageModifierPhase.Increase });
            Check(routine.MoveNext() && visualizer.VisibleModifierCount == 1, "First modifier on same piece shown alone");
            Check(routine.MoveNext() && visualizer.VisibleModifierCount == 1, "Second modifier on same piece shown in next wave");
            Check(!routine.MoveNext() && visualizer.VisibleModifierCount == 0, "Both modifier waves complete and clean up");
        }
        finally { logic.boardMap[3, 0] = queen; queen.ClearRpgState(); target.ClearRpgState(); }
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
