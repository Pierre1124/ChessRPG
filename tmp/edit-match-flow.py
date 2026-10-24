from pathlib import Path
p=Path('Assets/Scripts/Networking/MultiplayerGameController.cs')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('    private const byte EventActiveSkillResult = 16;', '''    private const byte EventActiveSkillResult = 16;
    private const byte EventRestartStatus = 17;
    private const byte EventSurrenderRequest = 18;
    private const byte EventRestartCommit = 19;''')
s=s.replace('    private bool isRestartingGame;', '''    private bool isRestartingGame;
    private readonly RestartAgreement restartAgreement = new RestartAgreement();
    private string visibleRestartId;
    private float restartDeadline;
    private bool restartRequestSent;
    private float restartRequestDeadline;''')
a=s.index('        if (!PhotonNetwork.IsMasterClient)',s.index('public override void OnPlayerLeftRoom'))
b=s.index('\n    }',a)
s=s[:a]+'''        ClearRestartInvitation();
        hasFirstRemoteClientDeck = false;
        if (!isReturningToStartScene && !isRestartingGame)
        {
            GameFlowUI.Show("對手已離開，請返回主選單重新開房");
            ResolveReferences();
            if (logicManager != null && !logicManager.IsGameOver)
                logicManager.ApplyRemoteGameOver("對手已離開對局");
        }'''+s[b:]
s=s.replace('        Time.timeScale = 1f;\n        RefreshModeFromPhoton();\n\n        if (!IsOnline)\n        {\n            LogicManager.SetNextGameMode(false);', '        RefreshModeFromPhoton();\n\n        if (!IsOnline)\n        {\n            Time.timeScale = 1f;\n            LogicManager.SetNextGameMode(logicManager != null && logicManager.IsClassicChess);',1)
a=s.index('        if (PhotonNetwork.IsMasterClient)',s.index('public void RequestRestartGame()'))
b=s.index('\n    }',a)
s=s[:a]+'''        if (!CanGameplayOperate || isRestartingGame || isReturningToStartScene)
        { GameFlowUI.Show("請等待雙方準備完成"); return; }
        if (restartRequestSent || !string.IsNullOrEmpty(visibleRestartId))
        { GameFlowUI.Show("重開邀請處理中，請在確認視窗回覆"); return; }
        restartRequestSent = true;
        restartRequestDeadline = Time.unscaledTime + 35f;
        if (PhotonNetwork.IsMasterClient)
            HandleRestartVote(PhotonNetwork.LocalPlayer.ActorNumber, string.Empty, true);
        else
        {
            RaiseToMaster(EventRestartRequest, new object[] { string.Empty, true });
            GameFlowUI.Show("已送出重開邀請，等待對方同意");
        }'''+s[b:]
a=s.index('                if (PhotonNetwork.IsMasterClient)',s.index('            case EventRestartRequest:'))
b=s.index('            case EventSideAssignment:',a)
s=s[:a]+'''                var vote = (object[])photonEvent.CustomData;
                HandleRestartVote(photonEvent.Sender, (string)vote[0], (bool)vote[1]);
                break;
            case EventRestartStatus:
                var status = (object[])photonEvent.CustomData;
                ApplyRestartStatus((string)status[0], (int)status[1], (int)status[2]);
                break;
            case EventSurrenderRequest:
                SurrenderAsAuthority(photonEvent.Sender);
                break;
            case EventRestartCommit:
                if (!PhotonNetwork.IsMasterClient && !isRestartingGame && !isReturningToStartScene)
                {
                    ClearRestartInvitation();
                    isRestartingGame = true;
                    Time.timeScale = 1f;
                    LogicManager.SetNextGameMode((bool)((object[])photonEvent.CustomData)[0]);
                    PhotonNetwork.LoadLevel(gameSceneName);
                }
                break;
'''+s[b:]
s=s.replace('        RaiseToOthers(EventGameOver, result);','''        CancelRestartInvitation(2);
        RaiseToOthers(EventGameOver, result);''')
s=s.replace('        Time.timeScale = 1f;\n        RefreshModeFromPhoton();\n\n        if (!IsOnline)\n        {\n            LoadSceneLocal(startSceneName);', '        ClearRestartInvitation();\n        Time.timeScale = 1f;\n        RefreshModeFromPhoton();\n\n        if (!IsOnline)\n        {\n            LoadSceneLocal(startSceneName);')
s=s.replace('        isRestartingGame = true;\n        LogicManager.SetNextGameMode(classicChess);','        ClearRestartInvitation();\n        Time.timeScale = 1f;\n        isRestartingGame = true;\n        LogicManager.SetNextGameMode(classicChess);')
s=s.replace('        PhotonNetwork.LoadLevel(gameSceneName);\n        Debug.Log', '''        // PUN scene properties do not reload a peer already in this same scene.
        RaiseToOthers(EventRestartCommit, new object[] { classicChess });
        PhotonNetwork.SendAllOutgoingCommands();
        PhotonNetwork.LoadLevel(gameSceneName);
        Debug.Log''')
insert='''    /// <summary>邀請以不受勝利畫面暫停影響的時間逾時。</summary>
    private void Update()
    {
        if (PhotonNetwork.IsMasterClient && restartAgreement.IsPending && Time.unscaledTime >= restartDeadline)
            CancelRestartInvitation(3);
        if (restartRequestSent && string.IsNullOrEmpty(visibleRestartId) && Time.unscaledTime >= restartRequestDeadline)
        {
            restartRequestSent = false;
            GameFlowUI.Show("重開要求逾時，請再試一次");
        }
    }

    private void HandleRestartVote(int actor, string invitationId, bool accept)
    {
        if (!PhotonNetwork.IsMasterClient || !CanGameplayOperate || isRestartingGame || isReturningToStartScene) return;
        int master = PhotonNetwork.MasterClient.ActorNumber;
        int remote = GetRemoteActorNumber();
        if (actor != master && actor != remote) return;
        if (string.IsNullOrEmpty(invitationId))
        {
            if (!accept) return;
            if (!restartAgreement.IsPending)
            {
                restartAgreement.Begin(actor, actor == master ? remote : master);
                restartDeadline = Time.unscaledTime + 30f;
            }
            // A second request is not an acceptance of an unseen invitation.
            PublishRestartStatus(0);
            return;
        }
        if (!restartAgreement.Reply(invitationId, actor, accept)) return;
        if (!accept) { CancelRestartInvitation(1); return; }
        if (restartAgreement.IsApproved)
        {
            RestartGameAsAuthority(logicManager != null && logicManager.IsClassicChess);
        }
    }

    private void PublishRestartStatus(int status)
    {
        object[] data = { restartAgreement.Id, restartAgreement.Requester, status };
        RaiseToOthers(EventRestartStatus, data);
        ApplyRestartStatus((string)data[0], (int)data[1], status);
    }

    private void CancelRestartInvitation(int status)
    {
        if (restartAgreement.IsPending) PublishRestartStatus(status);
        ClearRestartInvitation();
    }

    private void ApplyRestartStatus(string id, int requester, int status)
    {
        if (status != 0)
        {
            if (visibleRestartId != id) return;
            ClearRestartInvitation();
            GameFlowUI.Show(status == 1 ? "重開邀請已拒絕或取消" : status == 3 ? "重開邀請已逾時" : "對局已結束，重開邀請已取消");
            return;
        }
        visibleRestartId = id;
        restartRequestSent = false;
        bool requestedByMe = requester == PhotonNetwork.LocalPlayer.ActorNumber;
        GameFlowUI.ShowDecision("Restart", requestedByMe
            ? "已邀請重新開始，等待對方同意（30 秒）"
            : "對方邀請重新開始對局（30 秒）\\n只有雙方同意才會清除目前棋局。",
            requestedByMe ? null : (System.Action)(() => ReplyToRestart(id, true)),
            () => ReplyToRestart(id, false), "同意重開", requestedByMe ? "取消邀請" : "拒絕");
    }

    private void ReplyToRestart(string id, bool accept)
    {
        if (id != visibleRestartId || !IsOnline) return;
        if (PhotonNetwork.IsMasterClient) HandleRestartVote(PhotonNetwork.LocalPlayer.ActorNumber, id, accept);
        else RaiseToMaster(EventRestartRequest, new object[] { id, accept });
    }

    private void ClearRestartInvitation()
    {
        restartAgreement.Clear();
        visibleRestartId = null;
        restartRequestSent = false;
        GameFlowUI.HideDecision("Restart");
    }

    public void RequestSurrender()
    {
        ResolveReferences();
        if (logicManager == null || logicManager.IsGameOver || !CanGameplayOperate || isRestartingGame || isReturningToStartScene) return;
        if (!IsOnline)
            logicManager.Surrender(logicManager.isWhiteTurn);
        else if (PhotonNetwork.IsMasterClient)
            SurrenderAsAuthority(PhotonNetwork.LocalPlayer.ActorNumber);
        else RaiseToMaster(EventSurrenderRequest, string.Empty);
    }

    private void SurrenderAsAuthority(int actor)
    {
        ResolveReferences();
        if (!PhotonNetwork.IsMasterClient || !CanGameplayOperate || isRestartingGame || isReturningToStartScene ||
            logicManager == null || logicManager.IsGameOver) return;
        PlayerSide side = GetSideForActor(actor);
        if (side == PlayerSide.None) return;
        logicManager.Surrender(side == PlayerSide.White);
    }

'''
s=s.replace('    /// <summary>\n    /// F12 觸發',insert+'    /// <summary>\n    /// F12 觸發',1)
p.write_text(s,encoding='utf-8')
