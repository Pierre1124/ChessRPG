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
    private const string RoomPropertyMasterWhite = "MasterWhite";

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
                (!sideAssignmentReady || !hasFirstRemoteClientDeck);
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

    private void Awake()
    {
        ResolveReferences();
        PhotonNetwork.AutomaticallySyncScene = true;
        TryReadSideAssignmentFromRoom();
    }

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

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Reset()
    {
        ResolveReferences();
    }

    public override void OnJoinedRoom()
    {
        HandleJoinedRoomState();
    }

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

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
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

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (!isReturningToStartScene && !isRestartingGame)
        {
            Debug.Log($"[NetworkGame][Disconnected] Returning to StartScene. Cause={cause}");
            LoadSceneLocal(startSceneName);
        }
    }

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
        else if (!submittedLocalDeck)
        {
            TryReadSideAssignmentFromRoom();
            StartCoroutine(SubmitLocalDeckWhenReady());
        }

        BroadcastCurrentStateIfReady();
        RefreshLocalCardUi();
        ApplyLocalPlayerCameraPerspective();
    }

    public void RequestRestartGame()
    {
        Time.timeScale = 1f;
        RefreshModeFromPhoton();

        if (!IsOnline)
        {
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

    private bool IsLocalPlayerSide(bool isWhitePlayer)
    {
        RefreshModeFromPhoton();
        return localSide != PlayerSide.None &&
            (localSide == PlayerSide.White) == isWhitePlayer;
    }

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

    public NetworkGameCommand CreateDrawCardCommand()
    {
        return NetworkGameCommand.Simple(
            NetworkGameCommandKind.DrawCard,
            ConsumeSequence(),
            logicManager != null && logicManager.isWhiteTurn
        );
    }

    public NetworkGameCommand CreateRecycleCardCommand(CardDefinition card)
    {
        return NetworkGameCommand.Simple(
            NetworkGameCommandKind.RecycleCard,
            ConsumeSequence(),
            logicManager != null && logicManager.isWhiteTurn,
            card != null ? card.id : string.Empty
        );
    }

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
            ? cardHandManager.SerializeNetworkCardState()
            : string.Empty;
        bool canDrawThisTurn =
            cardHandManager != null && cardHandManager.CanDrawThisTurn;

        Debug.Log(
            $"[NetworkGame][BroadcastState] Turn={(isWhiteTurn ? "White" : "Black")} | " +
            $"HP={whiteHealth}/{blackHealth} | CanDraw={canDrawThisTurn}"
        );

        RaiseToOthers(
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

    public void BroadcastDamageCalculationBatch(string payload)
    {
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

    public void BroadcastPhaseChange(string message)
    {
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

    public void BroadcastGameOver(string result)
    {
        RefreshModeFromPhoton();

        if (!IsOnline || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        RaiseToOthers(EventGameOver, result);
    }

    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
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

    private void EnqueueRemotePhaseChange(string message)
    {
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

        NetworkGameCommand command =
            JsonUtility.FromJson<NetworkGameCommand>(payload);
        ExecuteCommandAsAuthority(command, photonEvent.Sender);
    }

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

    private void HandleDeckSubmit(EventData photonEvent)
    {
        if (!PhotonNetwork.IsMasterClient)
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
        if (senderSide == PlayerSide.None)
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
            return;
        }

        hasFirstRemoteClientDeck = true;
        Debug.Log(
            $"[NetworkGame][DeckReceived] Player={senderSide} | " +
            $"Ids={serializedDeckIds}"
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

    private IEnumerator SubmitLocalDeckWhenReady()
    {
        for (int frame = 0; frame < 120; frame++)
        {
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

        if (senderSide == PlayerSide.None || senderIsWhite != isWhitePlayer)
        {
            AnnounceCommandResult(
                NetworkGameCommandKind.MovePiece,
                sequence,
                false,
                "Promotion rejected: sender side mismatch"
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
                : $"Promotion rejected: {pieceName}"
        );
    }

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
                rejectReason
            );
            return;
        }

        bool accepted = false;
        string message = "Not implemented";

        switch (command.kind)
        {
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
                    RaiseToOthers(
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
                                ? cardHandManager.SerializeNetworkCardState()
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
            message
        );
    }

    private bool IsSenderAllowed(
        NetworkGameCommand command,
        int senderActorNumber,
        out string reason
    )
    {
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

    private void AnnounceCommandResult(
        NetworkGameCommandKind kind,
        int sequence,
        bool accepted,
        string message
    )
    {
        ApplyCommandResult(
            new object[] { (int)kind, sequence, accepted, message }
        );

        if (IsOnline)
        {
            RaiseToOthers(
                EventCommandResult,
                new object[] { (int)kind, sequence, accepted, message }
            );
        }
    }

    private string GetAlarmMessageForRejectedCommand(
        NetworkGameCommandKind kind,
        string message
    )
    {
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

    private PlayerSide GetSideForActor(int actorNumber)
    {
        if (!PhotonNetwork.InRoom)
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

        return sideAssignmentReady
            ? (masterPlaysWhite ? PlayerSide.Black : PlayerSide.White)
            : firstRemoteClientSide;
    }

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

    private void RefreshLocalCardUi()
    {
        if (cardHandManager != null && logicManager != null)
        {
            cardHandManager.Refresh(logicManager.isWhiteTurn);
            cardHandManager.RefreshHealth();
        }
    }

    private void ApplyLocalPlayerCameraPerspective()
    {
        CameraController cameraController =
            FindFirstObjectByType<CameraController>();
        if (cameraController != null)
        {
            cameraController.ApplyLocalPlayerPerspective();
        }
    }

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

    private void RestartGameAsAuthority()
    {
        if (isRestartingGame)
        {
            return;
        }

        isRestartingGame = true;
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
        PhotonNetwork.LoadLevel(gameSceneName);
        Debug.Log($"[NetworkGame][Restart] Photon loading scene: {gameSceneName}");
    }

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

    private static void LoadSceneLocal(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void RaiseToMaster(byte eventCode, object payload)
    {
        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.MasterClient
        };
        PhotonNetwork.RaiseEvent(
            eventCode,
            payload,
            options,
            SendOptions.SendReliable
        );
    }

    private void RaiseToOthers(byte eventCode, object payload)
    {
        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.Others
        };
        PhotonNetwork.RaiseEvent(
            eventCode,
            payload,
            options,
            SendOptions.SendReliable
        );
    }

    private static int ToInt(object value)
    {
        return value is int intValue ? intValue : System.Convert.ToInt32(value);
    }

    private static bool ToBool(object value)
    {
        return value is bool boolValue
            ? boolValue
            : System.Convert.ToBoolean(value);
    }

    private int ConsumeSequence()
    {
        int sequence = nextSequence;
        nextSequence++;
        return sequence;
    }

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
