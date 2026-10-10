using System.Collections;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class MultiplayerGameController :
    MonoBehaviourPunCallbacks,
    IOnEventCallback
{
    private const byte EventCommandRequest = 1;
    private const byte EventPromotionRequest = 2;
    private const byte EventDeckSubmit = 3;
    private const byte EventCommandResult = 4;
    private const byte EventState = 5;
    private const byte EventDamageBatch = 6;
    private const byte EventPhaseChange = 7;
    private const byte EventMoveResult = 8;
    private const byte EventPromotionResult = 9;
    private const byte EventCardOnPieceResult = 10;
    private const byte EventFieldCardResult = 11;
    private const byte EventGameOver = 12;
    private const byte EventReturnToStart = 13;
    private const byte EventRestartRequest = 14;
    private const byte EventSideAssignment = 15;
    private const byte EventActiveSkillResult = 16;
    private const string RoomPropertyMasterWhite = "MasterWhite";
    private const string RoomPropertyClassicChess = "ClassicChess";

    public static bool IsClassicChessRoom
    {
        get
        {
            return PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null &&
                PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomPropertyClassicChess, out object value) &&
                value is bool classicChess && classicChess;
        }
    }

    [Header("Mode")]
    [SerializeField] private MultiplayerMode mode = MultiplayerMode.Local;
    [SerializeField] private PlayerSide localSide = PlayerSide.White;
    [SerializeField] private PlayerSide hostSide = PlayerSide.White;
    [SerializeField] private PlayerSide firstRemoteClientSide = PlayerSide.Black;
    [SerializeField] private bool followPhotonState = true;
    [SerializeField, Min(2)] private int requiredPlayerCount = 2;
    [SerializeField] private bool logCommands = true;
    [SerializeField] private string gameSceneName = "ChessScene";
    [SerializeField] private string startSceneName = "StartScene";

    [Header("Scene References")]
    [SerializeField] private LogicManager logicManager;
    [SerializeField] private CardHandManager cardHandManager;

    private int nextSequence = 1;
    private readonly NetworkInputPolicy inputPolicy = new NetworkInputPolicy();
    private bool hasFirstRemoteClientDeck;
    private bool isReturningToStartScene;
    private bool isRestartingGame;
    private bool submittedLocalDeck;
    private bool sideAssignmentReady;
    private bool masterPlaysWhite = true;
    private bool receivedInitialNetworkState;
    private bool isRemotePhaseQueuePlaying;
    private readonly Queue<string> pendingRemotePhaseMessages =
        new Queue<string>();

    public MultiplayerMode Mode
    {
        get { return mode; }
    }

    public PlayerSide LocalSide
    {
        get
        {
            RefreshModeFromPhoton();
            return localSide;
        }
    }

    public bool IsOnline
    {
        get { return PhotonNetwork.InRoom; }
    }

    public bool IsHostAuthority
    {
        get
        {
            return mode == MultiplayerMode.Local ||
                PhotonNetwork.IsMasterClient;
        }
    }

    public bool IsWaitingForPlayer
    {
        get
        {
            return PhotonNetwork.InRoom &&
                PhotonNetwork.CurrentRoom != null &&
                PhotonNetwork.CurrentRoom.PlayerCount < requiredPlayerCount;
        }
    }

    public bool IsWaitingForRemoteDeck
    {
        get
        {
            return PhotonNetwork.InRoom &&
                PhotonNetwork.IsMasterClient &&
                PhotonNetwork.CurrentRoom != null &&
                PhotonNetwork.CurrentRoom.PlayerCount >= requiredPlayerCount &&
                (!sideAssignmentReady || (!IsClassicChessRoom && !hasFirstRemoteClientDeck));
        }
    }

    public bool CanGameplayOperate
    {
        get
        {
            if (!PhotonNetwork.InRoom)
            {
                return true;
            }

            return !IsWaitingForPlayer &&
                sideAssignmentReady &&
                (PhotonNetwork.IsMasterClient || receivedInitialNetworkState) &&
                !IsWaitingForRemoteDeck;
        }
    }

    public bool IsClientOnly
    {
        get { return PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient; }
    }

    public bool IsServerAuthority
    {
        get { return !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient; }
    }

    /// <summary>
    /// 取得對局引用、啟用 Photon 場景同步並讀取陣營設定。
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        PhotonNetwork.AutomaticallySyncScene = true;
        TryReadSideAssignmentFromRoom();
    }

    /// <summary>
    /// 登錄 Photon 與場景回呼，並恢復目前房間狀態。
    /// </summary>
    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryReadSideAssignmentFromRoom();
        RefreshModeFromPhoton();

        if (PhotonNetwork.InRoom)
        {
            HandleJoinedRoomState();
        }
    }

    /// <summary>
    /// 解除 Photon 與場景回呼，避免停用後繼續接收事件。
    /// </summary>
    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    /// <summary>
    /// 編輯器重設元件時補齊對局引用。
    /// </summary>
    private void Reset()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 加入房間後更新連線狀態，並依角色啟動場景或對局準備流程。
    /// </summary>
    public override void OnJoinedRoom()
    {
        HandleJoinedRoomState();
    }

    /// <summary>
    /// 玩家加入時更新陣營分配，並由主機同步可用的對局狀態。
    /// </summary>
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshModeFromPhoton();
        TryAssignSidesIfReady();

        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        BroadcastCurrentStateIfReady();
        RefreshLocalCardUi();
    }

    /// <summary>
    /// 主機偵測玩家離房後重設牌組準備狀態並重新開局。
    /// </summary>
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        hasFirstRemoteClientDeck = false;
        Debug.Log(
            $"[NetworkGame][ClientDisconnected] Actor={otherPlayer.ActorNumber}"
        );
        Debug.Log("[NetworkGame][DisconnectedRestart] Resetting game for reconnect.");
        RestartGameAsAuthority();
    }

    /// <summary>
    /// 房間陣營屬性更新時，套用本機陣營並調整提示與視角。
    /// </summary>
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(RoomPropertyClassicChess))
        {
            ResolveReferences();
            if (IsClassicChessRoom && logicManager != null && !logicManager.IsClassicChess)
            {
                logicManager.ApplyClassicChessMode();
            }
            BroadcastCurrentStateIfReady();
        }
        if (
            propertiesThatChanged == null ||
            !propertiesThatChanged.ContainsKey(RoomPropertyMasterWhite)
        )
        {
            return;
        }

        bool wasReady = sideAssignmentReady;
        TryReadSideAssignmentFromRoom();
        RefreshModeFromPhoton();

        if (!wasReady && sideAssignmentReady)
        {
            GameFlowUI.Show(
                localSide == PlayerSide.White
                    ? "你是白方"
                    : "你是黑方"
            );
            ApplyLocalPlayerCameraPerspective();
        }
    }

    /// <summary>
    /// 處理 Photon 斷線通知，依目前離場流程更新本機狀態。
    /// </summary>
    public override void OnDisconnected(DisconnectCause cause)
    {
        if (!isReturningToStartScene && !isRestartingGame)
        {
            Debug.Log($"[NetworkGame][Disconnected] Returning to StartScene. Cause={cause}");
            LoadSceneLocal(startSceneName);
        }
    }

    /// <summary>
    /// 場景載入後重新取得引用並更新連線對局狀態。
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        ResolveReferences();
        TryReadSideAssignmentFromRoom();
        RefreshModeFromPhoton();

        if (PhotonNetwork.InRoom && scene.name == gameSceneName)
        {
            HandleJoinedRoomState();
        }
    }

    /// <summary>
    /// 處理房間已加入狀態，準備陣營分配與牌組提交。
    /// </summary>
    private void HandleJoinedRoomState()
    {
        TryReadSideAssignmentFromRoom();
        RefreshModeFromPhoton();

        if (PhotonNetwork.IsMasterClient)
        {
            receivedInitialNetworkState = true;
            hasFirstRemoteClientDeck =
                PhotonNetwork.CurrentRoom == null ||
                PhotonNetwork.CurrentRoom.PlayerCount < requiredPlayerCount;
            TryAssignSidesIfReady();
        }
        else if (!IsClassicChessRoom && !submittedLocalDeck)
        {
            TryReadSideAssignmentFromRoom();
            StartCoroutine(SubmitLocalDeckWhenReady());
        }

        BroadcastCurrentStateIfReady();
        RefreshLocalCardUi();
        ApplyLocalPlayerCameraPerspective();
    }

    /// <summary>
    /// 本機是主機時重新開局，否則向主機傳送重新開始要求。
    /// </summary>
    public void RequestRestartGame()
    {
        Time.timeScale = 1f;
        RefreshModeFromPhoton();

        if (!IsOnline)
        {
            LogicManager.SetNextGameMode(false);
            LoadSceneLocal(gameSceneName);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            RestartGameAsAuthority();
            return;
        }

        GameFlowUI.Show("已向主機要求重新開始");
        RaiseToMaster(EventRestartRequest, string.Empty);
    }

    /// <summary>
    /// F12 觸發後由主機同步普通西洋棋模式並重載；客戶端只等待主機結果。
    /// </summary>
    public void RequestClassicChessRestart()
    {
        RefreshModeFromPhoton();
        if (IsOnline && !PhotonNetwork.IsMasterClient) return;
        Time.timeScale = 1f;
        RestartGameAsAuthority(true);
    }

    /// <summary>
    /// 依連線狀態通知玩家返回主選單並啟動離場流程。
    /// </summary>
    public void RequestReturnToStart()
    {
        Time.timeScale = 1f;
        RefreshModeFromPhoton();

        if (!IsOnline)
        {
            LoadSceneLocal(startSceneName);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            RaiseToOthers(EventReturnToStart, "主機返回主選單");
        }

        StartCoroutine(ShutdownAndLoadStartSceneRoutine());
    }

    /// <summary>
    /// 依 Photon 房間與主機狀態更新本機模式及陣營。
    /// </summary>
    private void RefreshModeFromPhoton()
    {
        if (!followPhotonState)
        {
            return;
        }

        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            mode = MultiplayerMode.Host;
            localSide = sideAssignmentReady
                ? (masterPlaysWhite ? PlayerSide.White : PlayerSide.Black)
                : hostSide;
        }
        else
        {
            mode = MultiplayerMode.Client;
            localSide = sideAssignmentReady
                ? (masterPlaysWhite ? PlayerSide.Black : PlayerSide.White)
                : firstRemoteClientSide;
        }

        if (logCommands)
        {
            Debug.Log(
                $"[NetworkGame] Photon mode resolved | " +
                $"Mode={mode} | LocalSide={localSide}"
            );
        }
    }

    /// <summary>
    /// 依連線準備狀態與回合陣營判斷本機玩家能否操作。
    /// </summary>
    public bool CanLocalPlayerAct(bool isWhiteTurn)
    {
        if (!CanGameplayOperate)
        {
            return false;
        }

        if (mode == MultiplayerMode.Local)
        {
            return true;
        }

        if (localSide == PlayerSide.None)
        {
            return false;
        }

        return (localSide == PlayerSide.White) == isWhiteTurn;
    }

    /// <summary>
    /// 判斷指定陣營是否屬於本機玩家。
    /// </summary>
    private bool IsLocalPlayerSide(bool isWhitePlayer)
    {
        RefreshModeFromPhoton();
        return localSide != PlayerSide.None &&
            (localSide == PlayerSide.White) == isWhitePlayer;
    }

    /// <summary>
    /// 建立包含序號、陣營及起訖座標的移動命令。
    /// </summary>
    public NetworkGameCommand CreateMoveCommand(
        Piece piece,
        Vector2 targetCoordinates
    )
    {
        return NetworkGameCommand.Move(
            ConsumeSequence(),
            piece != null && piece.IsWhite,
            BoardCoordinate.FromVector2(
                piece != null ? piece.GetCoordinates() : Vector2.zero
            ),
            BoardCoordinate.FromVector2(targetCoordinates)
        );
    }

    /// <summary>建立主動技能命令，沿用主機序號及回合驗證。</summary>
    public NetworkGameCommand CreateActiveSkillCommand(Piece piece)
    {
        return new NetworkGameCommand { kind = NetworkGameCommandKind.UseActiveSkill,
            sequence = ConsumeSequence(), isWhitePlayer = piece.IsWhite,
            from = BoardCoordinate.FromVector2(piece.GetCoordinates()) };
    }

    /// <summary>
    /// 建立對棋子出牌的連線命令。
    /// </summary>
    public NetworkGameCommand CreatePlayCardOnPieceCommand(
        CardDefinition card,
        Piece target
    )
    {
        return NetworkGameCommand.CardOnPiece(
            ConsumeSequence(),
            logicManager != null && logicManager.isWhiteTurn,
            card != null ? card.id : string.Empty,
            BoardCoordinate.FromVector2(
                target != null ? target.GetCoordinates() : Vector2.zero
            ),
            target != null ? target.name : string.Empty
        );
    }

    /// <summary>
    /// 建立指定場地欄位的出牌命令。
    /// </summary>
    public NetworkGameCommand CreatePlayFieldCardCommand(
        CardDefinition card,
        FieldCardPlace place
    )
    {
        return NetworkGameCommand.FieldCard(
            ConsumeSequence(),
            logicManager != null && logicManager.isWhiteTurn,
            card != null ? card.id : string.Empty,
            place != null ? place.name : string.Empty
        );
    }

    /// <summary>
    /// 建立目前玩家的抽牌命令。
    /// </summary>
    public NetworkGameCommand CreateDrawCardCommand()
    {
        return NetworkGameCommand.Simple(
            NetworkGameCommandKind.DrawCard,
            ConsumeSequence(),
            logicManager != null && logicManager.isWhiteTurn
        );
    }

    /// <summary>
    /// 建立指定卡牌的回收命令。
    /// </summary>
    public NetworkGameCommand CreateRecycleCardCommand(CardDefinition card)
    {
        return NetworkGameCommand.Simple(
            NetworkGameCommandKind.RecycleCard,
            ConsumeSequence(),
            logicManager != null && logicManager.isWhiteTurn,
            card != null ? card.id : string.Empty
        );
    }

    /// <summary>
    /// 判斷升變選擇是否應由本機玩家操作。
    /// </summary>
    public bool ShouldLocalChoosePromotion(bool isWhitePlayer)
    {
        RefreshModeFromPhoton();

        if (!IsOnline)
        {
            return true;
        }

        return localSide != PlayerSide.None &&
            (localSide == PlayerSide.White) == isWhitePlayer;
    }

    /// <summary>
    /// 將兵的升變選擇交由主機執行，或在本機直接套用。
    /// </summary>
    public void SubmitPromotionChoice(Pawn pawn, string pieceName)
    {
        if (pawn == null || string.IsNullOrEmpty(pieceName))
        {
            return;
        }

        ResolveReferences();

        BoardCoordinate coordinate =
            BoardCoordinate.FromVector2(pawn.GetCoordinates());
        bool isWhitePlayer = pawn.IsWhite;
        int sequence = ConsumeSequence();

        RefreshModeFromPhoton();

        if (!IsOnline)
        {
            logicManager?.ApplyPromotionChoice(
                coordinate,
                isWhitePlayer,
                pieceName,
                true
            );
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            ExecutePromotionAsAuthority(
                sequence,
                isWhitePlayer,
                coordinate,
                pieceName,
                PhotonNetwork.LocalPlayer.ActorNumber
            );
            return;
        }

        RaiseToMaster(
            EventPromotionRequest,
            new object[]
            {
                sequence,
                isWhitePlayer,
                coordinate.x,
                coordinate.y,
                pieceName
            }
        );
    }

    /// <summary>
    /// 依目前連線角色將操作交給主機；離線呼叫僅留下命令紀錄。
    /// </summary>
    public void SubmitCommand(NetworkGameCommand command)
    {
        RefreshModeFromPhoton();

        if (!IsOnline)
        {
            LogCommand("Local command recorded only", command);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            ExecuteCommandAsAuthority(
                command,
                PhotonNetwork.LocalPlayer.ActorNumber
            );
            return;
        }

        RaiseToMaster(EventCommandRequest, JsonUtility.ToJson(command));
    }

    /// <summary>
    /// 取得目前回合與血量的簡要快照；卡牌欄位維持既有空值格式。
    /// </summary>
    public NetworkGameSnapshot CaptureSnapshot()
    {
        return new NetworkGameSnapshot
        {
            isWhiteTurn = logicManager != null && logicManager.isWhiteTurn,
            whiteHealth = logicManager != null ? logicManager.whiteHealth : 0,
            blackHealth = logicManager != null ? logicManager.blackHealth : 0,
            activeFieldCardIds = string.Empty,
            whiteHandCardIds = string.Empty,
            blackHandCardIds = string.Empty
        };
    }

    /// <summary>
    /// 主機在玩家與牌組準備完成後廣播回合、血量及卡牌狀態。
    /// </summary>
    public void BroadcastState(bool isWhiteTurn, int whiteHealth, int blackHealth)
    {
        RefreshModeFromPhoton();
        ResolveReferences();

        if (
            !IsOnline ||
            !PhotonNetwork.IsMasterClient ||
            IsWaitingForPlayer ||
            IsWaitingForRemoteDeck
        )
        {
            return;
        }

        string cardState = cardHandManager != null
            ? cardHandManager.SerializeNetworkCardState(!masterPlaysWhite)
            : string.Empty;
        bool canDrawThisTurn =
            cardHandManager != null && cardHandManager.CanDrawThisTurn;

        Debug.Log(
            $"[NetworkGame][BroadcastState] Turn={(isWhiteTurn ? "White" : "Black")} | " +
            $"HP={whiteHealth}/{blackHealth} | CanDraw={canDrawThisTurn}"
        );

        RaiseToOpponent(
            EventState,
            new object[]
            {
                isWhiteTurn,
                whiteHealth,
                blackHealth,
                cardState,
                canDrawThisTurn
            }
        );
    }

    /// <summary>
    /// 由主機廣播已序列化的傷害演出批次。
    /// </summary>
    public void BroadcastDamageCalculationBatch(string payload)
    {
        if (IsClassicChessRoom || (logicManager != null && logicManager.IsClassicChess)) return;
        RefreshModeFromPhoton();

        if (
            string.IsNullOrEmpty(payload) ||
            !IsOnline ||
            !PhotonNetwork.IsMasterClient ||
            IsWaitingForPlayer ||
            IsWaitingForRemoteDeck
        )
        {
            return;
        }

        if (logCommands)
        {
            Debug.Log(
                $"[NetworkGame][DamageVisual] Broadcast batch payload length={payload.Length}"
            );
        }

        RaiseToOthers(EventDamageBatch, payload);
    }

    /// <summary>
    /// 由主機廣播回合階段提示。
    /// </summary>
    public void BroadcastPhaseChange(string message)
    {
        if (IsClassicChessRoom || (logicManager != null && logicManager.IsClassicChess)) return;
        RefreshModeFromPhoton();

        if (
            string.IsNullOrWhiteSpace(message) ||
            !IsOnline ||
            !PhotonNetwork.IsMasterClient ||
            IsWaitingForPlayer ||
            IsWaitingForRemoteDeck
        )
        {
            return;
        }

        RaiseToOthers(EventPhaseChange, message);
    }

    /// <summary>
    /// 由主機廣播遊戲結束結果。
    /// </summary>
    public void BroadcastGameOver(string result)
    {
        RefreshModeFromPhoton();

        if (!IsOnline || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        RaiseToOthers(EventGameOver, result);
    }

    /// <summary>
    /// 依 Photon 事件代碼分派命令、狀態及演出通知。
    /// </summary>
    public void OnEvent(EventData photonEvent)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.MasterClient == null) return;
        if (!NetworkInputPolicy.IsTrustedSender(photonEvent.Code, photonEvent.Sender,
            PhotonNetwork.MasterClient.ActorNumber, PhotonNetwork.IsMasterClient,
            PhotonNetwork.CurrentRoom.Players.ContainsKey(photonEvent.Sender))) return;
        if (!NetworkInputPolicy.IsValidPayload(photonEvent.Code, photonEvent.CustomData))
        {
            Debug.LogWarning($"[NetworkGame] Invalid payload, event={photonEvent.Code}");
            return;
        }
        switch (photonEvent.Code)
        {
            case EventActiveSkillResult:
                if (!PhotonNetwork.IsMasterClient && logicManager != null)
                {
                    var data = (object[])photonEvent.CustomData;
                    Piece piece = logicManager.boardMap[(int)data[2], (int)data[3]];
                    if (piece != null && piece.IsWhite == (bool)data[1]) PieceActiveSkill.ApplyDisabled(logicManager, piece);
                }
                break;
            case EventCommandRequest:
                HandleCommandRequest(photonEvent);
                break;
            case EventPromotionRequest:
                HandlePromotionRequest(photonEvent);
                break;
            case EventDeckSubmit:
                HandleDeckSubmit(photonEvent);
                break;
            case EventCommandResult:
                ApplyCommandResult(photonEvent.CustomData as object[]);
                break;
            case EventState:
                ApplyState(photonEvent.CustomData as object[]);
                break;
            case EventDamageBatch:
                ResolveReferences();
                logicManager?.PlayRemoteDamageCalculationBatch(
                    photonEvent.CustomData as string
                );
                break;
            case EventPhaseChange:
                EnqueueRemotePhaseChange(photonEvent.CustomData as string);
                break;
            case EventMoveResult:
                ApplyMoveResult(photonEvent.CustomData as object[]);
                break;
            case EventPromotionResult:
                ApplyPromotionResult(photonEvent.CustomData as object[]);
                break;
            case EventCardOnPieceResult:
                ApplyCardOnPieceResult(photonEvent.CustomData as object[]);
                break;
            case EventFieldCardResult:
                ApplyFieldCardResult(photonEvent.CustomData as object[]);
                break;
            case EventGameOver:
                ResolveReferences();
                logicManager?.ApplyRemoteGameOver(photonEvent.CustomData as string);
                break;
            case EventReturnToStart:
                GameFlowUI.Show(photonEvent.CustomData as string);
                StartCoroutine(ShutdownAndLoadStartSceneRoutine());
                break;
            case EventRestartRequest:
                if (PhotonNetwork.IsMasterClient)
                {
                    Debug.Log(
                        $"[NetworkGame][RestartRequested] Sender={photonEvent.Sender}"
                    );
                    RestartGameAsAuthority();
                }
                break;
            case EventSideAssignment:
                ApplySideAssignment(photonEvent.CustomData as object[], true);
                break;
        }
    }

    /// <summary>
    /// 玩家人數足夠時由主機分配白黑陣營並同步結果。
    /// </summary>
    private void TryAssignSidesIfReady()
    {
        if (
            !PhotonNetwork.InRoom ||
            !PhotonNetwork.IsMasterClient ||
            PhotonNetwork.CurrentRoom == null ||
            PhotonNetwork.CurrentRoom.PlayerCount < requiredPlayerCount ||
            sideAssignmentReady
        )
        {
            return;
        }

        bool masterIsWhite = Random.value >= 0.5f;
        Hashtable properties = new Hashtable
        {
            { RoomPropertyMasterWhite, masterIsWhite }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);

        object[] payload =
            new object[]
            {
                PhotonNetwork.MasterClient.ActorNumber,
                masterIsWhite
            };
        ApplySideAssignment(payload, true);
        RaiseToOthers(EventSideAssignment, payload);
    }

    /// <summary>
    /// 嘗試讀取房間中保存的陣營分配。
    /// </summary>
    private void TryReadSideAssignmentFromRoom()
    {
        if (
            !PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(
                RoomPropertyMasterWhite
            )
        )
        {
            return;
        }

        masterPlaysWhite =
            ToBool(PhotonNetwork.CurrentRoom.CustomProperties[
                RoomPropertyMasterWhite
            ]);
        sideAssignmentReady = true;
    }

    /// <summary>
    /// 套用收到的陣營分配並依需要顯示提示。
    /// </summary>
    private void ApplySideAssignment(object[] data, bool showAlarm)
    {
        if (data == null || data.Length < 2)
        {
            return;
        }

        int masterActorNumber = ToInt(data[0]);
        masterPlaysWhite = ToBool(data[1]);
        sideAssignmentReady = true;
        RefreshModeFromPhoton();

        if (showAlarm)
        {
            GameFlowUI.Show(
                localSide == PlayerSide.White
                    ? "你是白方"
                    : "你是黑方"
            );
        }

        ApplyLocalPlayerCameraPerspective();

        Debug.Log(
            $"[NetworkGame][SideAssigned] MasterActor={masterActorNumber} | " +
            $"Master={(masterPlaysWhite ? "White" : "Black")} | " +
            $"Local={localSide}"
        );
    }

    /// <summary>
    /// 將遠端階段提示排入本機播放佇列。
    /// </summary>
    private void EnqueueRemotePhaseChange(string message)
    {
        if (IsClassicChessRoom || (logicManager != null && logicManager.IsClassicChess)) return;
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        pendingRemotePhaseMessages.Enqueue(message);

        if (!isRemotePhaseQueuePlaying)
        {
            StartCoroutine(PlayRemotePhaseQueue());
        }
    }

    /// <summary>
    /// 依序播放遠端階段提示，避免提示演出互相覆蓋。
    /// </summary>
    private IEnumerator PlayRemotePhaseQueue()
    {
        isRemotePhaseQueuePlaying = true;

        while (pendingRemotePhaseMessages.Count > 0)
        {
            string message = pendingRemotePhaseMessages.Dequeue();

            yield return WaitForPhaseUiReady();

            bool completed = false;
            GameFlowUI.PlayPhase(
                this,
                message,
                () =>
                {
                    completed = true;
                }
            );

            while (!completed)
            {
                yield return null;
            }
        }

        isRemotePhaseQueuePlaying = false;
    }

    /// <summary>
    /// 等待階段提示介面具備播放條件。
    /// </summary>
    private IEnumerator WaitForPhaseUiReady()
    {
        for (int frame = 0; frame < 120; frame++)
        {
            if (GameFlowUI.IsPhaseReady())
            {
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning(
            "[NetworkGame][PhaseChange] Phase UI was not ready before timeout."
        );
    }

    /// <summary>
    /// 主機解析收到的操作命令並交由權限與規則驗證流程處理。
    /// </summary>
    private void HandleCommandRequest(EventData photonEvent)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        string payload = photonEvent.CustomData as string;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        NetworkGameCommand command;
        try { command = JsonUtility.FromJson<NetworkGameCommand>(payload); }
        catch (System.ArgumentException) { return; }
        ExecuteCommandAsAuthority(command, photonEvent.Sender);
    }

    /// <summary>
    /// 主機解析升變要求並執行既有升變驗證流程。
    /// </summary>
    private void HandlePromotionRequest(EventData photonEvent)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        object[] data = photonEvent.CustomData as object[];
        if (data == null || data.Length < 5)
        {
            return;
        }

        ExecutePromotionAsAuthority(
            ToInt(data[0]),
            ToBool(data[1]),
            new BoardCoordinate(ToInt(data[2]), ToInt(data[3])),
            data[4] as string,
            photonEvent.Sender
        );
    }

    /// <summary>
    /// 主機依發送者陣營套用提交的牌組，成功後同步對局狀態。
    /// </summary>
    private void HandleDeckSubmit(EventData photonEvent)
    {
        if (!PhotonNetwork.IsMasterClient || IsClassicChessRoom || hasFirstRemoteClientDeck)
        {
            return;
        }

        object[] data = photonEvent.CustomData as object[];
        if (data == null || data.Length < 2)
        {
            return;
        }

        ResolveReferences();

        PlayerSide senderSide = GetSideForActor(photonEvent.Sender);
        if (senderSide == PlayerSide.None || photonEvent.Sender == PhotonNetwork.MasterClient.ActorNumber)
        {
            Debug.LogWarning(
                $"[NetworkGame][DeckRejected] Unknown sender={photonEvent.Sender}"
            );
            return;
        }

        string serializedDeckIds = data[0] as string;
        bool keepFirstCard = ToBool(data[1]);
        bool isWhitePlayer = senderSide == PlayerSide.White;
        bool accepted =
            cardHandManager != null &&
            cardHandManager.SetDeckForPlayerAsAuthority(
                isWhitePlayer,
                serializedDeckIds,
                keepFirstCard
            );

        if (!accepted)
        {
            Debug.LogWarning($"[NetworkGame][DeckRejected] Player={senderSide}");
            PhotonNetwork.RaiseEvent(EventReturnToStart, "牌組資料無效，請重新儲存牌組後加入",
                new RaiseEventOptions { TargetActors = new[] { photonEvent.Sender } }, SendOptions.SendReliable);
            return;
        }

        hasFirstRemoteClientDeck = true;
        Debug.Log(
            $"[NetworkGame][DeckReceived] Player={senderSide} | " +
            $"Cards validated"
        );

        if (logicManager != null)
        {
            BroadcastState(
                logicManager.isWhiteTurn,
                logicManager.whiteHealth,
                logicManager.blackHealth
            );
        }
    }

    /// <summary>
    /// 等待對局元件與陣營準備完成後提交本機牌組。
    /// </summary>
    private IEnumerator SubmitLocalDeckWhenReady()
    {
        for (int frame = 0; frame < 120; frame++)
        {
            if (IsClassicChessRoom) yield break;
            ResolveReferences();

            if (
                cardHandManager != null &&
                cardHandManager.cardLibrary != null
            )
            {
                string deckIds = cardHandManager.LocalSerializedDeckIds;
                bool keepFirstCard = cardHandManager.LocalDeckHasOpeningCard;

                if (!string.IsNullOrEmpty(deckIds))
                {
                    RaiseToMaster(
                        EventDeckSubmit,
                        new object[] { deckIds, keepFirstCard }
                    );
                    submittedLocalDeck = true;
                    Debug.Log(
                        $"[NetworkGame][DeckSubmitted] LocalSide={localSide} | " +
                        $"Ids={deckIds} | Opening={keepFirstCard}"
                    );
                    yield break;
                }
            }

            yield return null;
        }

        Debug.LogWarning(
            "[NetworkGame][DeckSubmitFailed] Could not find local deck before timeout."
        );
    }

    /// <summary>
    /// 主機驗證升變要求，套用選擇並廣播結果。
    /// </summary>
    private void ExecutePromotionAsAuthority(
        int sequence,
        bool isWhitePlayer,
        BoardCoordinate coordinate,
        string pieceName,
        int senderActorNumber
    )
    {
        ResolveReferences();

        PlayerSide senderSide = GetSideForActor(senderActorNumber);
        bool senderIsWhite = senderSide == PlayerSide.White;

        if (!inputPolicy.TryConsume(senderActorNumber, sequence) || !coordinate.IsValid ||
            senderSide == PlayerSide.None || senderIsWhite != isWhitePlayer)
        {
            AnnounceCommandResult(
                NetworkGameCommandKind.MovePiece,
                sequence,
                false,
                "Promotion rejected: sender side mismatch", senderActorNumber
            );
            return;
        }

        bool accepted =
            logicManager != null &&
            logicManager.ApplyPromotionChoice(
                coordinate,
                isWhitePlayer,
                pieceName,
                true
            );

        if (accepted)
        {
            RaiseToOthers(
                EventPromotionResult,
                new object[]
                {
                    sequence,
                    isWhitePlayer,
                    coordinate.x,
                    coordinate.y,
                    pieceName
                }
            );
        }

        AnnounceCommandResult(
            NetworkGameCommandKind.MovePiece,
            sequence,
            accepted,
            accepted
                ? $"Promotion applied: {pieceName}"
                : $"Promotion rejected: {pieceName}", senderActorNumber
        );
    }

    /// <summary>
    /// 主機驗證發送者與回合，執行對應操作並回報結果。
    /// </summary>
    private void ExecuteCommandAsAuthority(
        NetworkGameCommand command,
        int senderActorNumber
    )
    {
        ResolveReferences();

        if (!IsSenderAllowed(command, senderActorNumber, out string rejectReason))
        {
            LogCommand($"Rejected: {rejectReason}", command);
            AnnounceCommandResult(
                command.kind,
                command.sequence,
                false,
                rejectReason, senderActorNumber
            );
            return;
        }

        bool accepted = false;
        string message = "Not implemented";

        switch (command.kind)
        {
            case NetworkGameCommandKind.UseActiveSkill:
                accepted = logicManager != null && command.from.IsValid && PieceActiveSkill.TryUse(logicManager,
                    logicManager.boardMap[command.from.x, command.from.y], command.isWhitePlayer);
                message = accepted ? "電網已關閉" : "目前無法使用主動技能";
                if (accepted) RaiseToOthers(EventActiveSkillResult, new object[] {
                    command.sequence, command.isWhitePlayer, command.from.x, command.from.y });
                break;
            case NetworkGameCommandKind.MovePiece:
                accepted =
                    logicManager != null &&
                    logicManager.TryExecuteNetworkMove(
                        command.from,
                        command.to,
                        command.isWhitePlayer
                    );
                message = accepted ? "Move applied" : "Move rejected by LogicManager";
                if (accepted)
                {
                    RaiseToOpponent(
                        EventMoveResult,
                        new object[]
                        {
                            command.sequence,
                            command.isWhitePlayer,
                            command.from.x,
                            command.from.y,
                            command.to.x,
                            command.to.y,
                            logicManager.isWhiteTurn,
                            logicManager.whiteHealth,
                            logicManager.blackHealth,
                            cardHandManager != null
                                ? cardHandManager.SerializeNetworkCardState(!masterPlaysWhite)
                                : string.Empty,
                            cardHandManager != null &&
                                cardHandManager.CanDrawThisTurn
                        }
                    );
                }
                break;
            case NetworkGameCommandKind.DrawCard:
                accepted =
                    cardHandManager != null &&
                    cardHandManager.DrawForPlayerAsAuthority(
                        command.isWhitePlayer
                    );
                message = accepted ? "Draw applied" : "Draw rejected";
                if (accepted && logicManager != null)
                {
                    BroadcastState(
                        logicManager.isWhiteTurn,
                        logicManager.whiteHealth,
                        logicManager.blackHealth
                    );
                }
                break;
            case NetworkGameCommandKind.RecycleCard:
                accepted =
                    cardHandManager != null &&
                    cardHandManager.RecycleCardForPlayerAsAuthority(
                        command.isWhitePlayer,
                        command.cardId
                    );
                message = accepted ? "Recycle applied" : "Recycle rejected";
                if (accepted && logicManager != null)
                {
                    BroadcastState(
                        logicManager.isWhiteTurn,
                        logicManager.whiteHealth,
                        logicManager.blackHealth
                    );
                }
                break;
            case NetworkGameCommandKind.PlayCardOnPiece:
                accepted =
                    cardHandManager != null &&
                    cardHandManager.PlayCardOnPieceAsAuthority(
                        command.isWhitePlayer,
                        command.cardId,
                        command.to
                    );
                message = accepted
                    ? "Card on piece applied"
                    : "Card on piece rejected";
                if (accepted && logicManager != null)
                {
                    bool playedHostVisual =
                        cardHandManager != null &&
                        !IsLocalPlayerSide(command.isWhitePlayer) &&
                        cardHandManager.PlayUsingCardAnimationOnPiece(
                            command.cardId,
                            command.to
                        );
                    RaiseToOthers(
                        EventCardOnPieceResult,
                        new object[]
                        {
                            command.sequence,
                            command.isWhitePlayer,
                            command.cardId,
                            command.to.x,
                            command.to.y
                        }
                    );
                    BroadcastState(
                        logicManager.isWhiteTurn,
                        logicManager.whiteHealth,
                        logicManager.blackHealth
                    );
                    if (logCommands)
                    {
                        Debug.Log(
                            $"[NetworkGame] Host card visual | " +
                            $"Seq={command.sequence} | Visual={playedHostVisual} | " +
                            $"Card={command.cardId} | Target={command.to}"
                        );
                    }
                }
                break;
            case NetworkGameCommandKind.PlayFieldCard:
                accepted =
                    cardHandManager != null &&
                    cardHandManager.PlayFieldCardAsAuthority(
                        command.isWhitePlayer,
                        command.cardId,
                        command.fieldPlaceName
                    );
                message = accepted
                    ? "Field card applied"
                    : "Field card rejected";
                if (accepted && logicManager != null)
                {
                    bool playedHostVisual =
                        cardHandManager != null &&
                        !IsLocalPlayerSide(command.isWhitePlayer) &&
                        cardHandManager.PlayUsingCardAnimationOnField(
                            command.cardId,
                            command.fieldPlaceName
                        );
                    RaiseToOthers(
                        EventFieldCardResult,
                        new object[]
                        {
                            command.sequence,
                            command.isWhitePlayer,
                            command.cardId,
                            command.fieldPlaceName
                        }
                    );
                    BroadcastState(
                        logicManager.isWhiteTurn,
                        logicManager.whiteHealth,
                        logicManager.blackHealth
                    );
                    if (logCommands)
                    {
                        Debug.Log(
                            $"[NetworkGame] Host field visual | " +
                            $"Seq={command.sequence} | Visual={playedHostVisual} | " +
                            $"Card={command.cardId} | Place={command.fieldPlaceName}"
                        );
                    }
                }
                break;
            default:
                message = "Command kind is not implemented.";
                break;
        }

        LogCommand(message, command);
        AnnounceCommandResult(
            command.kind,
            command.sequence,
            accepted,
            message, senderActorNumber
        );
    }

    /// <summary>
    /// 檢查命令發送者的陣營與目前回合是否相符。
    /// </summary>
    private bool IsSenderAllowed(
        NetworkGameCommand command,
        int senderActorNumber,
        out string reason
    )
    {
        if (!inputPolicy.TryConsume(senderActorNumber, command.sequence))
        { reason = "操作已處理或已過期，請重新操作"; return false; }
        if (!System.Enum.IsDefined(typeof(NetworkGameCommandKind), command.kind) ||
            command.kind == NetworkGameCommandKind.EndTurn ||
            (command.kind == NetworkGameCommandKind.MovePiece && (!command.from.IsValid || !command.to.IsValid)) ||
            (command.kind == NetworkGameCommandKind.PlayCardOnPiece && !command.to.IsValid) ||
            (command.kind == NetworkGameCommandKind.UseActiveSkill && !command.from.IsValid))
        { reason = "操作資料無效"; return false; }
        if (logicManager == null || IsWaitingForPlayer || IsWaitingForRemoteDeck || isRestartingGame ||
            Time.timeScale == 0f || logicManager.IsOperationLocked || logicManager.isPromotionActive ||
            logicManager.IsFieldFusionPlaying)
        { reason = "請等待對局準備或目前演出完成"; return false; }
        if ((IsClassicChessRoom || (logicManager != null && logicManager.IsClassicChess)) &&
            command.kind != NetworkGameCommandKind.MovePiece)
        {
            reason = "普通西洋棋模式不能使用卡牌";
            return false;
        }
        PlayerSide senderSide = GetSideForActor(senderActorNumber);
        bool senderIsWhite = senderSide == PlayerSide.White;

        if (senderSide == PlayerSide.None)
        {
            reason = $"No side assigned to actor {senderActorNumber}";
            return false;
        }

        if (senderIsWhite != command.isWhitePlayer)
        {
            reason =
                $"Player side mismatch. Player={senderSide}, CommandWhite={command.isWhitePlayer}";
            return false;
        }

        if (
            logicManager != null &&
            logicManager.isWhiteTurn != command.isWhitePlayer
        )
        {
            reason = "Not this player's turn";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// 在非主機端套用收到的移動結果及附帶狀態。
    /// </summary>
    private void ApplyMoveResult(object[] data)
    {
        if (PhotonNetwork.IsMasterClient || data == null || data.Length < 11)
        {
            return;
        }

        ResolveReferences();

        int sequence = ToInt(data[0]);
        bool isWhitePlayer = ToBool(data[1]);
        BoardCoordinate from =
            new BoardCoordinate(ToInt(data[2]), ToInt(data[3]));
        BoardCoordinate to =
            new BoardCoordinate(ToInt(data[4]), ToInt(data[5]));
        bool finalIsWhiteTurn = ToBool(data[6]);
        int finalWhiteHealth = ToInt(data[7]);
        int finalBlackHealth = ToInt(data[8]);
        string finalCardState = data[9] as string;
        bool finalCanDrawThisTurn = ToBool(data[10]);

        bool applied =
            logicManager != null &&
            logicManager.ApplyNetworkMoveResult(
                from,
                to,
                isWhitePlayer
            );

        if (logCommands)
        {
            Debug.Log(
                $"[NetworkGame] Remote move result | Seq={sequence} | " +
                $"Applied={applied} | From={from} | To={to}"
            );
        }

        logicManager?.ApplyRemoteNetworkState(
            finalIsWhiteTurn,
            finalWhiteHealth,
            finalBlackHealth,
            finalCardState,
            finalCanDrawThisTurn
        );
    }

    /// <summary>
    /// 在非主機端套用收到的升變結果。
    /// </summary>
    private void ApplyPromotionResult(object[] data)
    {
        if (PhotonNetwork.IsMasterClient || data == null || data.Length < 5)
        {
            return;
        }

        ResolveReferences();

        int sequence = ToInt(data[0]);
        bool isWhitePlayer = ToBool(data[1]);
        BoardCoordinate coordinate =
            new BoardCoordinate(ToInt(data[2]), ToInt(data[3]));
        string pieceName = data[4] as string;
        bool applied =
            logicManager != null &&
            logicManager.ApplyPromotionChoice(
                coordinate,
                isWhitePlayer,
                pieceName,
                true
            );

        if (logCommands)
        {
            Debug.Log(
                $"[NetworkGame] Promotion result | Seq={sequence} | " +
                $"Applied={applied} | Cell={coordinate} | Piece={pieceName}"
            );
        }
    }

    /// <summary>
    /// 在非主機端重播對棋子的出牌效果；手牌由另外的狀態同步更新。
    /// </summary>
    private void ApplyCardOnPieceResult(object[] data)
    {
        if (PhotonNetwork.IsMasterClient || data == null || data.Length < 5)
        {
            return;
        }

        ResolveReferences();

        int sequence = ToInt(data[0]);
        bool isWhitePlayer = ToBool(data[1]);
        string cardId = data[2] as string;
        BoardCoordinate target =
            new BoardCoordinate(ToInt(data[3]), ToInt(data[4]));
        bool applied =
            cardHandManager != null &&
            cardHandManager.PlayCardOnPieceAsAuthority(
                isWhitePlayer,
                cardId,
                target,
                false
            );
        bool playedVisual = false;
        if (
            applied &&
            cardHandManager != null &&
            !IsLocalPlayerSide(isWhitePlayer)
        )
        {
            playedVisual =
                cardHandManager.PlayUsingCardAnimationOnPiece(
                    cardId,
                    target
                );
        }

        if (logCommands)
        {
            Debug.Log(
                $"[NetworkGame] Card result | Seq={sequence} | " +
                $"Applied={applied} | Visual={playedVisual} | " +
                $"Card={cardId} | Target={target}"
            );
        }
    }

    /// <summary>
    /// 在非主機端重播場地卡效果；手牌由另外的狀態同步更新。
    /// </summary>
    private void ApplyFieldCardResult(object[] data)
    {
        if (PhotonNetwork.IsMasterClient || data == null || data.Length < 4)
        {
            return;
        }

        ResolveReferences();

        int sequence = ToInt(data[0]);
        bool isWhitePlayer = ToBool(data[1]);
        string cardId = data[2] as string;
        string fieldPlaceName = data[3] as string;
        bool applied =
            cardHandManager != null &&
            cardHandManager.PlayFieldCardAsAuthority(
                isWhitePlayer,
                cardId,
                fieldPlaceName,
                false
            );
        bool playedVisual = false;
        if (
            applied &&
            cardHandManager != null &&
            !IsLocalPlayerSide(isWhitePlayer)
        )
        {
            playedVisual =
                cardHandManager.PlayUsingCardAnimationOnField(
                    cardId,
                    fieldPlaceName
                );
        }

        if (logCommands)
        {
            Debug.Log(
                $"[NetworkGame] Field card result | Seq={sequence} | " +
                $"Applied={applied} | Visual={playedVisual} | " +
                $"Card={cardId} | Place={fieldPlaceName}"
            );
        }
    }

    /// <summary>
    /// 在非主機端接收初始或更新狀態，並套用回合、血量及卡牌資料。
    /// </summary>
    private void ApplyState(object[] data)
    {
        if (PhotonNetwork.IsMasterClient || data == null || data.Length < 5)
        {
            return;
        }

        receivedInitialNetworkState = true;
        ResolveReferences();
        logicManager?.ApplyRemoteNetworkState(
            ToBool(data[0]),
            ToInt(data[1]),
            ToInt(data[2]),
            data[3] as string,
            ToBool(data[4])
        );
    }

    /// <summary>
    /// 處理命令接受或拒絕結果，必要時顯示操作提示。
    /// </summary>
    private void ApplyCommandResult(object[] data)
    {
        if (data == null || data.Length < 4)
        {
            return;
        }

        NetworkGameCommandKind kind =
            (NetworkGameCommandKind)ToInt(data[0]);
        int sequence = ToInt(data[1]);
        bool accepted = ToBool(data[2]);
        string message = data[3] as string;

        if (logCommands)
        {
            Debug.Log(
                $"[NetworkGame] Result | Kind={kind} | " +
                $"Seq={sequence} | Accepted={accepted} | {message}"
            );
        }

        if (!accepted)
        {
            GameFlowUI.Show(GetAlarmMessageForRejectedCommand(kind, message));
        }
    }

    /// <summary>
    /// 將命令結果只回報給發起操作的玩家，避免對手看到錯誤提示。
    /// </summary>
    private void AnnounceCommandResult(
        NetworkGameCommandKind kind, int sequence, bool accepted, string message, int recipientActor)
    {
        object[] payload = { (int)kind, sequence, accepted, message };
        if (!IsOnline || recipientActor == PhotonNetwork.LocalPlayer.ActorNumber)
            ApplyCommandResult(payload);
        else
            PhotonNetwork.RaiseEvent(EventCommandResult, payload,
                new RaiseEventOptions { TargetActors = new[] { recipientActor } }, SendOptions.SendReliable);
    }

    /// <summary>
    /// 依命令種類與拒絕原因取得玩家提示文字。
    /// </summary>
    private string GetAlarmMessageForRejectedCommand(
        NetworkGameCommandKind kind,
        string message
    )
    {
        if (message == "請等待對局準備或目前演出完成" || message == "操作已處理或已過期，請重新操作" ||
            message == "普通西洋棋模式不能使用卡牌") return message;
        if (message == "Not this player's turn") return "尚未輪到你，請等待對手完成回合";
        return kind switch
        {
            NetworkGameCommandKind.MovePiece => "非法走法",
            NetworkGameCommandKind.DrawCard => "現在不能抽牌",
            NetworkGameCommandKind.PlayCardOnPiece => "卡片不能裝備到該棋子",
            NetworkGameCommandKind.PlayFieldCard => "場地卡不能放置",
            NetworkGameCommandKind.RecycleCard => "現在不能回收卡片",
            _ => string.IsNullOrWhiteSpace(message) ? "操作失敗" : message
        };
    }

    /// <summary>
    /// 依 Photon 玩家編號取得分配的棋方。
    /// </summary>
    private PlayerSide GetSideForActor(int actorNumber)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null ||
            !PhotonNetwork.CurrentRoom.Players.ContainsKey(actorNumber))
        {
            return PlayerSide.None;
        }

        Player master = PhotonNetwork.MasterClient;
        if (master != null && actorNumber == master.ActorNumber)
        {
            return sideAssignmentReady
                ? (masterPlaysWhite ? PlayerSide.White : PlayerSide.Black)
                : hostSide;
        }

        if (actorNumber != GetRemoteActorNumber()) return PlayerSide.None;
        return sideAssignmentReady
            ? (masterPlaysWhite ? PlayerSide.Black : PlayerSide.White)
            : firstRemoteClientSide;
    }

    /// <summary>
    /// 補齊此元件所需的場景與 UI 引用。
    /// </summary>
    private void ResolveReferences()
    {
        if (logicManager == null)
        {
            logicManager = FindFirstObjectByType<LogicManager>();
        }

        if (cardHandManager == null)
        {
            cardHandManager = FindFirstObjectByType<CardHandManager>();
        }
    }

    /// <summary>
    /// 依目前對局資料刷新本機卡牌介面。
    /// </summary>
    private void RefreshLocalCardUi()
    {
        if (cardHandManager != null && logicManager != null)
        {
            cardHandManager.Refresh(logicManager.isWhiteTurn);
            cardHandManager.RefreshHealth();
        }
    }

    /// <summary>
    /// 依本機陣營更新相機觀看方向。
    /// </summary>
    private void ApplyLocalPlayerCameraPerspective()
    {
        CameraController cameraController =
            FindFirstObjectByType<CameraController>();
        if (cameraController != null)
        {
            cameraController.ApplyLocalPlayerPerspective();
        }
    }

    /// <summary>
    /// 當主機與對局資料就緒時廣播目前狀態。
    /// </summary>
    private void BroadcastCurrentStateIfReady()
    {
        ResolveReferences();

        if (logicManager == null)
        {
            return;
        }

        BroadcastState(
            logicManager.isWhiteTurn,
            logicManager.whiteHealth,
            logicManager.blackHealth
        );
    }

    /// <summary>
    /// 由主機重設對局準備狀態並重新載入遊戲場景。
    /// </summary>
    private void RestartGameAsAuthority(bool classicChess = false)
    {
        if (isRestartingGame)
        {
            return;
        }

        isRestartingGame = true;
        LogicManager.SetNextGameMode(classicChess);
        sideAssignmentReady = false;
        receivedInitialNetworkState = PhotonNetwork.IsMasterClient;
        hasFirstRemoteClientDeck =
            !PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null ||
            PhotonNetwork.CurrentRoom.PlayerCount < requiredPlayerCount;
        submittedLocalDeck = false;

        if (!IsOnline)
        {
            LoadSceneLocal(gameSceneName);
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        TryAssignSidesIfReady();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { RoomPropertyClassicChess, classicChess }
        });
        StartCoroutine(ReloadSceneAfterModeSync(classicChess));
    }

    /// <summary>
    /// 等待房間模式由 Photon 確認後再同步載入場景，讓雙方初始化相同的規則。
    /// </summary>
    private IEnumerator ReloadSceneAfterModeSync(bool classicChess)
    {
        while (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient &&
            IsClassicChessRoom != classicChess)
        {
            yield return null;
        }

        if (!PhotonNetwork.InRoom)
        {
            LogicManager.SetNextGameMode(false);
            LoadSceneLocal(startSceneName);
            yield break;
        }
        if (!PhotonNetwork.IsMasterClient) yield break;
        PhotonNetwork.LoadLevel(gameSceneName);
        Debug.Log($"[NetworkGame][Restart] ClassicChess={classicChess} | Scene={gameSceneName}");
    }

    /// <summary>
    /// 等待 Photon 離線後載入起始場景。
    /// </summary>
    private IEnumerator ShutdownAndLoadStartSceneRoutine()
    {
        if (isReturningToStartScene)
        {
            yield break;
        }

        isReturningToStartScene = true;

        yield return new WaitForSecondsRealtime(0.1f);

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            yield return null;
        }

        LoadSceneLocal(startSceneName);
    }

    /// <summary>
    /// 在本機載入指定場景。
    /// </summary>
    private static void LoadSceneLocal(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 以可靠傳送方式將事件送給 Photon Master Client。
    /// </summary>
    private void RaiseToMaster(byte eventCode, object payload)
    {
        RaiseReliableEvent(eventCode, payload, ReceiverGroup.MasterClient);
    }

    /// <summary>取得雙人棋局中唯一的對手，額外房間成員不獲得玩家私有資料。</summary>
    private int GetRemoteActorNumber()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null) return -1;
        int result = int.MaxValue;
        foreach (Player player in PhotonNetwork.PlayerList)
            if (player.ActorNumber != PhotonNetwork.MasterClient.ActorNumber && player.ActorNumber < result)
                result = player.ActorNumber;
        return result == int.MaxValue ? -1 : result;
    }

    /// <summary>將含有手牌的狀態只送給實際對手，避免廣播私有資料。</summary>
    private void RaiseToOpponent(byte eventCode, object payload)
    {
        int actor = GetRemoteActorNumber();
        if (actor > 0) PhotonNetwork.RaiseEvent(eventCode, payload,
            new RaiseEventOptions { TargetActors = new[] { actor } }, SendOptions.SendReliable);
    }

    /// <summary>
    /// 以可靠傳送方式將事件送給房間內其他玩家。
    /// </summary>
    private void RaiseToOthers(byte eventCode, object payload)
    {
        RaiseReliableEvent(eventCode, payload, ReceiverGroup.Others);
    }

    /// <summary>
    /// 依指定接收群組傳送可靠 Photon 事件，保留原有事件碼與資料格式。
    /// </summary>
    private static void RaiseReliableEvent(
        byte eventCode,
        object payload,
        ReceiverGroup receivers
    )
    {
        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = receivers
        };
        PhotonNetwork.RaiseEvent(
            eventCode,
            payload,
            options,
            SendOptions.SendReliable
        );
    }

    /// <summary>
    /// 依現有網路資料格式將物件轉換成整數。
    /// </summary>
    private static int ToInt(object value)
    {
        return value is int intValue ? intValue : System.Convert.ToInt32(value);
    }

    /// <summary>
    /// 依現有網路資料格式將物件轉換成布林值。
    /// </summary>
    private static bool ToBool(object value)
    {
        return value is bool boolValue
            ? boolValue
            : System.Convert.ToBoolean(value);
    }

    /// <summary>
    /// 取得下一個本機命令序號並推進計數器。
    /// </summary>
    private int ConsumeSequence()
    {
        int sequence = nextSequence;
        nextSequence++;
        return sequence;
    }

    /// <summary>
    /// 啟用命令紀錄時輸出模式、序號與命令內容。
    /// </summary>
    private void LogCommand(string label, NetworkGameCommand command)
    {
        if (!logCommands)
        {
            return;
        }

        Debug.Log(
            $"[NetworkGame] {label} | " +
            $"Mode={mode} | Kind={command.kind} | Seq={command.sequence} | " +
            $"Player={(command.isWhitePlayer ? "White" : "Black")} | " +
            $"From={command.from} | To={command.to} | " +
            $"Card={command.cardId} | Target={command.targetObjectName} | " +
            $"FieldPlace={command.fieldPlaceName}"
        );
    }
}
