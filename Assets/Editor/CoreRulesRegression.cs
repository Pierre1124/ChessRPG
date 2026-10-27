using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

/// <summary>不修改場景的連線邊界、牌組與合成規則回歸測試。</summary>
public static class CoreRulesRegression
{
    /// <summary>從編輯器執行所有資料測試並保存可追蹤的結果。</summary>
    [MenuItem("Tools/Chess/Run Core Rules Regression")]
    public static void Run()
    {
        var results = new List<string>();
        try
        {
            RunManagedChecks((condition, name) => Check(condition, name, results));
            var findings = new List<string>();
            CardLibraryValidation.Validate(new[] { new CardDefinition { id = "J01" }, new CardDefinition { id = "J01" } }, findings);
            Check(findings.Exists(value => value.Contains("重複")), "Authoring detects duplicate IDs", results);
            Check(findings.Exists(value => value.Contains("缺少名稱")), "Authoring detects missing names", results);
            Check(findings.Exists(value => value.Contains("缺少卡面")), "Authoring detects missing art", results);
            foreach (bool white in new[] { true, false })
            {
                string json = PrivateCardState.Encode(white, "J01,E02", 4);
                Check(PrivateCardState.TryDecode(json, out PrivateCardState state) &&
                    state.recipientWhite == white && state.handIds == "J01,E02" && state.opponentHandCount == 4,
                    "Private hand round trip for " + white, results);
                Check(!json.Contains("deck") && !json.Contains("opponentHandIds"), "No deck order or opponent IDs", results);
            }
            Check(!PrivateCardState.TryDecode("J01;E01;F01;J02", out _), "Reject legacy leaked state", results);
            Check(!PrivateCardState.TryDecode("{broken", out _), "Reject malformed state", results);
            Check(!PrivateCardState.TryDecode("{\"version\":2,\"handIds\":\"\"}", out _), "Reject incompatible version", results);
        }
        catch (Exception error) { results.Add("FAIL: " + error); throw; }
        finally
        {
            Directory.CreateDirectory("output");
            File.WriteAllLines("output/core-rules-regression.txt", results);
        }
    }

    /// <summary>執行無 Unity 執行階段依賴的測試，亦可由命令列驗證程式呼叫。</summary>
    public static void RunManagedChecks(Action<bool, string> check)
    {
        CardKeywordRegression.RunManagedChecks(check);
        DamageModifierFeedbackSetup.ManagedChecks(check);
        check(!new PieceDefinition().HasMoveRules, "Empty serialized move definition preserves standard movement");
        check(!new PieceDefinition { moveRules = null }.HasMoveRules, "Null move list preserves standard movement");
        check(!new PieceDefinition { moveRules = new List<PieceMoveRule> { null } }.HasMoveRules, "Null-only move list preserves standard movement");
        check(new PieceDefinition { moveRules = new List<PieceMoveRule> { new PieceMoveRule() } }.HasMoveRules,
            "Explicit move rules still override standard movement");
        var fieldRooks = new HashSet<UnityEngine.Vector2Int>
        {
            new UnityEngine.Vector2Int(0, 0), new UnityEngine.Vector2Int(3, 0), new UnityEngine.Vector2Int(6, 0),
            new UnityEngine.Vector2Int(0, 3), new UnityEngine.Vector2Int(3, 3), new UnityEngine.Vector2Int(6, 3)
        };
        var fieldOccupied = new HashSet<UnityEngine.Vector2Int>(fieldRooks);
        var connected = FieldRookConnections.Collect(new UnityEngine.Vector2Int(0, 0), fieldOccupied.Contains, fieldRooks.Contains);
        check(connected.Count == 5 && new HashSet<UnityEngine.Vector2Int>(connected).Count == 5,
            "Field card connects every rook across parallel rows without duplicates");
        foreach (var origin in fieldRooks)
            check(FieldRookConnections.Collect(origin, fieldOccupied.Contains, fieldRooks.Contains).Count == 5,
                "Field network is independent of selected endpoint " + origin);
        fieldRooks.Clear(); fieldOccupied.Clear();
        fieldRooks.UnionWith(new[] { new UnityEngine.Vector2Int(0, 0), new UnityEngine.Vector2Int(3, 0), new UnityEngine.Vector2Int(6, 0) });
        fieldOccupied.UnionWith(fieldRooks);
        check(FieldRookConnections.Collect(new UnityEngine.Vector2Int(0, 0), fieldOccupied.Contains, fieldRooks.Contains).Count == 2,
            "Collinear rooks connect through intermediate friendly endpoint");
        fieldOccupied.Add(new UnityEngine.Vector2Int(1, 0));
        check(FieldRookConnections.Collect(new UnityEngine.Vector2Int(0, 0), fieldOccupied.Contains, fieldRooks.Contains).Count == 0,
            "Intervening piece blocks the field network");
        fieldOccupied.Remove(new UnityEngine.Vector2Int(1, 0));
        fieldRooks.Remove(new UnityEngine.Vector2Int(3, 0));
        check(FieldRookConnections.Collect(new UnityEngine.Vector2Int(0, 0), fieldOccupied.Contains, fieldRooks.Contains).Count == 0,
            "Enemy rook blocks connection to friendly rook beyond");
        fieldRooks.Clear(); fieldOccupied.Clear();
        fieldRooks.UnionWith(new[] { new UnityEngine.Vector2Int(0, 0), new UnityEngine.Vector2Int(1, 0), new UnityEngine.Vector2Int(3, 3) });
        fieldOccupied.UnionWith(fieldRooks);
        check(FieldRookConnections.Collect(new UnityEngine.Vector2Int(0, 0), fieldOccupied.Contains, fieldRooks.Contains).Count == 0,
            "Adjacent and diagonal rooks do not create a field");
        foreach (int requester in new[] { 1, 2 })
        {
            int opponent = requester == 1 ? 2 : 1;
            var agreement = new RestartAgreement();
            agreement.Begin(requester, opponent);
            string id = agreement.Id;
            check(agreement.IsPending && !agreement.IsApproved, "One vote never restarts: " + requester);
            check(!agreement.Reply(id, requester, true), "Requester cannot supply second vote: " + requester);
            check(!agreement.Reply(id, 99, true), "Nonparticipant cannot approve: " + requester);
            check(!agreement.Reply("expired", opponent, true), "Stale invitation rejected: " + requester);
            agreement.Begin(opponent, requester);
            check(agreement.Id == id && !agreement.IsApproved, "Simultaneous request does not silently consent: " + requester);
            check(agreement.Reply(id, opponent, true) && agreement.IsApproved, "Both participants approve: " + requester);
            check(!agreement.Reply(id, opponent, true), "Duplicate acceptance ignored: " + requester);
            agreement.Clear();
            check(!agreement.IsPending && !agreement.IsApproved && !agreement.Reply(id, opponent, true),
                "Disconnect or timeout clears old votes: " + requester);
            agreement.Begin(requester, opponent);
            check(agreement.Id != id && !agreement.Reply(id, opponent, true), "New invitation cannot reuse previous approval: " + requester);
            check(agreement.Reply(agreement.Id, opponent, false) && !agreement.IsApproved, "Opponent can decline: " + requester);
            agreement.Clear();
            agreement.Begin(requester, opponent);
            check(agreement.Reply(agreement.Id, requester, false) && !agreement.IsApproved, "Requester can cancel: " + requester);
        }
        foreach (byte request in new byte[] { 14, 18 })
        {
            check(NetworkInputPolicy.IsTrustedSender(request, 2, 1, true, true), "Host accepts match request " + request);
            check(!NetworkInputPolicy.IsTrustedSender(request, 2, 1, false, true), "Client rejects match request " + request);
            check(!NetworkInputPolicy.IsTrustedSender(request, 99, 1, true, false), "Reject outsider match request " + request);
        }
        foreach (byte result in new byte[] { 17, 19 })
        {
            check(NetworkInputPolicy.IsTrustedSender(result, 1, 1, false, true), "Accept master match result " + result);
            check(!NetworkInputPolicy.IsTrustedSender(result, 2, 1, true, true), "Reject forged match result " + result);
        }
        check(!NetworkInputPolicy.IsValidPayload(14, ""), "Reject legacy single-click restart");
        check(NetworkInputPolicy.IsValidPayload(14, new object[] { "invitation", true }), "Accept typed restart response");
        check(!NetworkInputPolicy.IsValidPayload(14, new object[] { "invitation", "yes" }), "Reject malformed restart vote");
        check(NetworkInputPolicy.IsValidPayload(18, ""), "Accept surrender without caller-selected side");
        check(!NetworkInputPolicy.IsValidPayload(18, "White"), "Reject caller-selected surrender side");
        check(NetworkInputPolicy.IsValidPayload(19, new object[] { true }), "Accept classic-mode reload");
        check(!NetworkInputPolicy.IsValidPayload(19, true), "Reject malformed reload");
        var net = new BoardFieldEffectZone { type = BoardFieldEffectType.ElectricNet,
            cells = new List<UnityEngine.Vector2Int> { new UnityEngine.Vector2Int(2, 2), new UnityEngine.Vector2Int(2, 3) } };
        var nets = new List<BoardFieldEffectZone> { net, net };
        check(ElectricNetCrossing.Collect(new UnityEngine.Vector2Int(2, 1), new UnityEngine.Vector2Int(2, 4), false, nets).Count == 2,
            "Electric traversal hits each token once even with overlapping zones");
        check(ElectricNetCrossing.Collect(new UnityEngine.Vector2Int(2, 2), new UnityEngine.Vector2Int(2, 3), false, nets).Count == 1,
            "Electric traversal excludes origin and includes landing cell");
        check(ElectricNetCrossing.Collect(new UnityEngine.Vector2Int(1, 0), new UnityEngine.Vector2Int(2, 2), true, nets).Count == 1,
            "Knight landing on token triggers electric damage");
        check(ElectricNetCrossing.Collect(new UnityEngine.Vector2Int(1, 1), new UnityEngine.Vector2Int(3, 2), true, nets).Count == 0,
            "Knight does not cross intermediate token cells");
        check(ElectricNetCrossing.Collect(new UnityEngine.Vector2Int(2, 2), new UnityEngine.Vector2Int(2, 2), false, nets).Count == 0,
            "Stationary piece does not retrigger token");
        foreach (DamageTag tag in new[] { DamageTag.None, DamageTag.Fire, DamageTag.Electric, DamageTag.Poison, DamageTag.Curse, DamageTag.True, DamageTag.Cost })
        {
            bool fixedDamage = tag == DamageTag.True || tag == DamageTag.Cost;
            bool outgoing = tag == DamageTag.None || tag == DamageTag.Fire;
            check(CardDamageRules.AllowsOutgoing(tag) == outgoing, tag + " outgoing bonus/weakening policy");
            check(CardDamageRules.AllowsEffect(tag, new CardEffectData { effectType = CardEffectType.ModifyDamageTaken }) == !fixedDamage,
                tag + " vulnerability/reduction policy");
            check(CardDamageRules.AllowsEffect(tag, new CardEffectData { effectType = CardEffectType.ModifyDamageDealt, operation = CardValueOperation.Set }),
                tag + " final truce override");
        }
        check(CardNumericRules.Apply(3.5f, CardValueOperation.Add, -2) == 1.5f, "Add preserves fractional value");
        check(CardNumericRules.Apply(9f, CardValueOperation.Set, 2) == 2f, "Set replaces prior modifiers");
        check(CardNumericRules.Apply(3.5f, CardValueOperation.Multiply, 2) == 7f, "Multiply preserves calculation precision");
        check(CardNumericRules.Apply(CardNumericRules.Apply(3f, CardValueOperation.Add, 2),
            CardValueOperation.Multiply, 3) == 15f, "Ordered add then multiply");
        var status = new StatusRuntime(new StatusDefinition { maxCharges = 2, initialCharges = 5,
            consumeChargeOnTrigger = true, rechargeTrigger = CardEffectTrigger.TurnStarted, rechargeAmount = 1 }, null, null, false);
        check(status.charges == 2, "Status initial charges clamp to maximum");
        check(status.ConsumeIfNeeded() && status.ConsumeIfNeeded() && !status.ConsumeIfNeeded(), "Status cannot trigger after charges exhausted");
        status.Recharge(CardEffectTrigger.TurnEnded);
        check(status.charges == 0, "Status ignores unrelated recharge trigger");
        status.Recharge(CardEffectTrigger.TurnStarted);
        check(status.charges == 1, "Status recharge restores one charge");
        status.Recharge(CardEffectTrigger.TurnStarted);
        status.Recharge(CardEffectTrigger.TurnStarted);
        check(status.charges == 2, "Status recharge cannot exceed maximum");
        check(NetworkInputPolicy.IsTrustedSender(1, 2, 1, true, true), "Host accepts member command");
        check(!NetworkInputPolicy.IsTrustedSender(1, 2, 1, false, true), "Client rejects command requests");
        check(!NetworkInputPolicy.IsTrustedSender(5, 2, 1, true, true), "Reject forged state from opponent");
        check(NetworkInputPolicy.IsTrustedSender(5, 1, 1, false, true), "Accept authoritative state");
        check(!NetworkInputPolicy.IsTrustedSender(12, 1, 1, false, false), "Reject nonmember game-over event");
        check(!NetworkInputPolicy.IsValidPayload(9, new object[] { 1, true, 8, 7, "Queen" }), "Reject out-of-board promotion");
        var policy = new NetworkInputPolicy();
        check(!policy.TryConsume(1, 0), "Reject zero sequence");
        check(policy.TryConsume(1, 3), "Accept first sequence");
        check(!policy.TryConsume(1, 3) && !policy.TryConsume(1, 2), "Reject duplicate and stale sequence");
        check(policy.TryConsume(2, 1) && policy.TryConsume(1, 4), "Independent player sequence streams");
        check(NetworkInputPolicy.IsValidPayload(5, new object[] { true, 100, 100, "{}", true }), "Accept typed state");
        check(!NetworkInputPolicy.IsValidPayload(5, new object[] { "true", 100, 100, "{}", true }), "Reject coerced boolean");
        check(!NetworkInputPolicy.IsValidPayload(5, new object[] { true }), "Reject truncated state");
        check(!NetworkInputPolicy.IsValidPayload(1, new string('x', 8193)), "Reject oversized command");
        check(!NetworkInputPolicy.IsValidPayload(99, "ignored"), "Reject unknown event");
        var card = new CardDefinition { id = "J01" };
        Func<string, CardDefinition> lookup = id => id == "J01" ? card : null;
        check(NetworkInputPolicy.TryParseDeck("J01,J01", lookup, 1, out var deck) && deck.Count == 2, "Accept maximum allowed copies");
        check(!NetworkInputPolicy.TryParseDeck("J01,J01,J01", lookup, 1, out _), "Reject excess copies");
        check(!NetworkInputPolicy.TryParseDeck("J01,INVALID", lookup, 2, out _), "Reject whole deck containing unknown ID");
        check(!NetworkInputPolicy.TryParseDeck("", lookup, 1, out _), "Reject empty deck");
        check(!NetworkInputPolicy.TryParseDeck("J01,", lookup, 1, out _), "Reject empty card ID");
        string[,] recipes = { { "F01", "F01", "F02" }, { "F03", "F03", "F04" },
            { "F05", "F05", "F06" }, { "F07", "F07", "F08" }, { "F09", "F09", "F10" },
            { "F01", "F03", "F11" }, { "F05", "F07", "F12" } };
        for (int a = 1; a <= 12; a++)
            for (int b = 1; b <= 12; b++)
            {
                string first = "F" + a.ToString("00"), second = "F" + b.ToString("00"), expected = null;
                for (int i = 0; i < recipes.GetLength(0); i++)
                    if ((first == recipes[i, 0] && second == recipes[i, 1]) ||
                        (first == recipes[i, 1] && second == recipes[i, 0])) expected = recipes[i, 2];
                check(FieldCardRules.GetFusionId(first, second) == expected, "Fusion " + first + "/" + second);
            }
        check(FieldCardRules.GetFusionId(null, null) == null, "Null pair cannot fuse");
        check(FieldCardRules.IsAdvancedField("F12") && !FieldCardRules.IsAdvancedField("F01"), "Advanced field slot classification");
    }

    /// <summary>記錄斷言，失敗時立即中止以免產生誤導結果。</summary>
    private static void Check(bool condition, string name, List<string> results)
    {
        if (!condition) throw new InvalidOperationException(name);
        results.Add("PASS: " + name);
    }
}
