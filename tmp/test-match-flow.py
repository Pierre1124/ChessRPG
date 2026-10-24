from pathlib import Path
p=Path('Assets/Editor/CoreRulesRegression.cs')
s=p.read_text(encoding='utf-8-sig')
pos=s.index('        var net =')
s=s[:pos]+'''        foreach (int requester in new[] { 1, 2 })
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
'''+s[pos:]
p.write_text(s,encoding='utf-8')
