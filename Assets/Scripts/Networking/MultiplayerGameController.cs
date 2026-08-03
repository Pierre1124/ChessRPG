using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkObject))]
public class MultiplayerGameController : NetworkBehaviour
{
    [Header("Mode")]
    [SerializeField] private MultiplayerMode mode = MultiplayerMode.Local;
    [SerializeField] private PlayerSide localSide = PlayerSide.White;
    [SerializeField] private PlayerSide hostSide = PlayerSide.White;
    [SerializeField] private PlayerSide firstRemoteClientSide = PlayerSide.Black;
    [SerializeField] private bool followNetworkManagerState = true;
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

    public MultiplayerMode Mode
    {
        get { return mode; }
    }

    public PlayerSide LocalSide
    {
        get
        {
            RefreshModeFromNetworkManager();
            return localSide;
        }
    }

    public bool IsOnline
    {
        get
        {
            return mode != MultiplayerMode.Local ||
                (
                    NetworkManager.Singleton != null &&
                    NetworkManager.Singleton.IsListening
                );
        }
    }

    public bool IsHostAuthority
    {
        get { return mode == MultiplayerMode.Local || mode == MultiplayerMode.Host; }
    }

    public bool IsWaitingForPlayer
    {
        get
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (
                manager == null ||
                !manager.IsListening ||
                !(manager.IsHost || manager.IsServer)
            )
            {
                return false;
            }

            return manager.ConnectedClientsIds.Count < requiredPlayerCount;
        }
    }

    public bool IsWaitingForRemoteDeck
    {
        get
        {
            NetworkManager manager = NetworkManager.Singleton;
            return IsOnline &&
                manager != null &&
                manager.IsListening &&
                manager.IsServer &&
                manager.ConnectedClientsIds.Count >= requiredPlayerCount &&
                !hasFirstRemoteClientDeck;
        }
    }

    public bool CanGameplayOperate
    {
        get { return !IsWaitingForPlayer && !IsWaitingForRemoteDeck; }
    }

    public bool IsClientOnly
    {
        get
        {
            NetworkManager manager = NetworkManager.Singleton;
            return IsOnline &&
                manager != null &&
                manager.IsClient &&
                !manager.IsServer;
        }
    }

    public bool IsServerAuthority
    {
        get
        {
            if (!IsOnline)
            {
                return true;
            }

            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && manager.IsServer;
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    public override void OnNetworkSpawn()
    {
        RefreshModeFromNetworkManager();

        if (IsServer)
        {
            hasFirstRemoteClientDeck = requiredPlayerCount <= 1;
        }

        if (NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        if (IsClient && !IsServer)
        {
            StartCoroutine(SubmitLocalDeckWhenReady());
        }

        BroadcastCurrentStateIfReady();
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    public void RequestRestartGame()
    {
        Time.timeScale = 1f;
        RefreshModeFromNetworkManager();

        if (!IsOnline)
        {
            LoadSceneLocal(gameSceneName);
            return;
        }

        if (!IsSpawned)
        {
            GameFlowUI.Show("連線尚未準備完成");
            return;
        }

        if (IsServer)
        {
            RestartGameAsAuthority();
            return;
        }

        GameFlowUI.Show("已向主機請求重新開始");
        RequestRestartRpc();
    }

    public void RequestReturnToStart()
    {
        Time.timeScale = 1f;
        RefreshModeFromNetworkManager();

        if (!IsOnline)
        {
            LoadSceneLocal(startSceneName);
            return;
        }

        if (IsServer)
        {
            NotifyReturnToStartRpc("主機已返回主選單");
        }

        StartCoroutine(ShutdownAndLoadStartSceneRoutine());
    }

    private void RefreshModeFromNetworkManager()
    {
        if (!followNetworkManagerState)
        {
            return;
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
        {
            return;
        }

        if (manager.IsHost || manager.IsServer)
        {
            mode = MultiplayerMode.Host;
            localSide = hostSide;
        }
        else if (manager.IsClient)
        {
            mode = MultiplayerMode.Client;
            localSide = firstRemoteClientSide;
        }

        Debug.Log(
            $"[NetworkGame] Network mode resolved | " +
            $"Mode={mode} | LocalSide={localSide}"
        );
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
        RefreshModeFromNetworkManager();
        if (localSide == PlayerSide.None)
        {
            return false;
        }

        return (localSide == PlayerSide.White) == isWhitePlayer;
    }

    public NetworkGameCommand CreateMoveCommand(Piece piece, Vector2 targetCoordinates)
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
        RefreshModeFromNetworkManager();

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

        RefreshModeFromNetworkManager();

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

        if (!IsSpawned)
        {
            Debug.LogWarning(
                "[NetworkGame] Cannot submit promotion before NetworkObject is spawned."
            );
            return;
        }

        if (IsServer)
        {
            ExecutePromotionAsAuthority(
                sequence,
                isWhitePlayer,
                coordinate,
                pieceName,
                NetworkManager.LocalClientId
            );
            return;
        }

        RequestPromotionRpc(
            sequence,
            isWhitePlayer,
            coordinate.x,
            coordinate.y,
            pieceName
        );
    }

    public void SubmitCommand(NetworkGameCommand command)
    {
        RefreshModeFromNetworkManager();

        if (!IsOnline)
        {
            LogCommand("Local command recorded only", command);
            return;
        }

        if (!IsSpawned)
        {
            LogCommand("NetworkObject is not spawned yet", command);
            return;
        }

        if (IsServer)
        {
            ExecuteCommandAsAuthority(command, NetworkManager.LocalClientId);
            return;
        }

        switch (command.kind)
        {
            case NetworkGameCommandKind.MovePiece:
                RequestMoveRpc(
                    command.sequence,
                    command.isWhitePlayer,
                    command.from.x,
                    command.from.y,
                    command.to.x,
                    command.to.y
                );
                break;
            default:
                RequestCardCommandRpc(
                    (int)command.kind,
                    command.sequence,
                    command.isWhitePlayer,
                    command.cardId,
                    command.to.x,
                    command.to.y,
                    command.targetObjectName,
                    command.fieldPlaceName
                );
                break;
        }
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

    [Rpc(SendTo.Server)]
    private void RequestMoveRpc(
        int sequence,
        bool isWhitePlayer,
        int fromX,
        int fromY,
        int toX,
        int toY,
        RpcParams rpcParams = default
    )
    {
        NetworkGameCommand command = NetworkGameCommand.Move(
            sequence,
            isWhitePlayer,
            new BoardCoordinate(fromX, fromY),
            new BoardCoordinate(toX, toY)
        );

        ExecuteCommandAsAuthority(command, rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Server)]
    private void RequestCardCommandRpc(
        int kind,
        int sequence,
        bool isWhitePlayer,
        string cardId,
        int targetX,
        int targetY,
        string targetObjectName,
        string fieldPlaceName,
        RpcParams rpcParams = default
    )
    {
        NetworkGameCommand command = new NetworkGameCommand
        {
            kind = (NetworkGameCommandKind)kind,
            sequence = sequence,
            isWhitePlayer = isWhitePlayer,
            cardId = cardId,
            to = new BoardCoordinate(targetX, targetY),
            targetObjectName = targetObjectName,
            fieldPlaceName = fieldPlaceName
        };

        ExecuteCommandAsAuthority(command, rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Server)]
    private void RequestPromotionRpc(
        int sequence,
        bool isWhitePlayer,
        int x,
        int y,
        string pieceName,
        RpcParams rpcParams = default
    )
    {
        ExecutePromotionAsAuthority(
            sequence,
            isWhitePlayer,
            new BoardCoordinate(x, y),
            pieceName,
            rpcParams.Receive.SenderClientId
        );
    }

    [Rpc(SendTo.Server)]
    private void SubmitDeckRpc(
        string serializedDeckIds,
        bool keepFirstCard,
        RpcParams rpcParams = default
    )
    {
        ResolveReferences();

        PlayerSide senderSide = GetSideForClient(
            rpcParams.Receive.SenderClientId
        );
        if (senderSide == PlayerSide.None)
        {
            Debug.LogWarning(
                $"[NetworkGame][DeckRejected] Unknown sender={rpcParams.Receive.SenderClientId}"
            );
            return;
        }

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
            Debug.LogWarning(
                $"[NetworkGame][DeckRejected] Player={senderSide}"
            );
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

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceCommandResultRpc(
        int kind,
        int sequence,
        bool accepted,
        string message
    )
    {
        if (logCommands)
        {
            Debug.Log(
                $"[NetworkGame] Result | Kind={(NetworkGameCommandKind)kind} | " +
                $"Seq={sequence} | Accepted={accepted} | {message}"
            );
        }

        if (!accepted)
        {
            GameFlowUI.Show(GetAlarmMessageForRejectedCommand(
                (NetworkGameCommandKind)kind,
                message
            ));
        }
    }

    private string GetAlarmMessageForRejectedCommand(
        NetworkGameCommandKind kind,
        string message
    )
    {
        switch (kind)
        {
            case NetworkGameCommandKind.MovePiece:
                return "非法走法";
            case NetworkGameCommandKind.DrawCard:
                return "現在不能抽牌";
            case NetworkGameCommandKind.PlayCardOnPiece:
                return "卡片不能使用";
            case NetworkGameCommandKind.PlayFieldCard:
                return "場地區不能放";
            case NetworkGameCommandKind.RecycleCard:
                return "現在不能回收卡片";
            default:
                return string.IsNullOrWhiteSpace(message)
                    ? "操作失敗"
                    : message;
        }
    }

    public void BroadcastState(bool isWhiteTurn, int whiteHealth, int blackHealth)
    {
        RefreshModeFromNetworkManager();
        ResolveReferences();

        if (
            !IsOnline ||
            !IsServer ||
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

        ApplyStateRpc(
            isWhiteTurn,
            whiteHealth,
            blackHealth,
            cardState,
            canDrawThisTurn
        );
    }

    public void BroadcastDamageCalculationBatch(string payload)
    {
        RefreshModeFromNetworkManager();

        if (
            string.IsNullOrEmpty(payload) ||
            !IsOnline ||
            !IsServer ||
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

        ApplyDamageCalculationBatchRpc(payload);
    }

    public void BroadcastPhaseChange(string message)
    {
        RefreshModeFromNetworkManager();

        if (
            string.IsNullOrWhiteSpace(message) ||
            !IsOnline ||
            !IsServer ||
            IsWaitingForPlayer ||
            IsWaitingForRemoteDeck
        )
        {
            return;
        }

        ApplyPhaseChangeRpc(message);
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

    private void HandleClientConnected(ulong clientId)
    {
        RefreshModeFromNetworkManager();

        if (!IsServer)
        {
            return;
        }

        BroadcastCurrentStateIfReady();

        if (cardHandManager != null && logicManager != null)
        {
            cardHandManager.Refresh(logicManager.isWhiteTurn);
            cardHandManager.RefreshHealth();
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
                    SubmitDeckRpc(deckIds, keepFirstCard);
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

    public void BroadcastGameOver(string result)
    {
        RefreshModeFromNetworkManager();

        if (!IsOnline || !IsServer)
        {
            return;
        }

        ApplyGameOverRpc(result);
    }

    [Rpc(SendTo.Server)]
    private void RequestRestartRpc(RpcParams rpcParams = default)
    {
        Debug.Log(
            $"[NetworkGame][RestartRequested] Sender={rpcParams.Receive.SenderClientId}"
        );
        RestartGameAsAuthority();
    }

    [Rpc(SendTo.NotServer)]
    private void NotifyReturnToStartRpc(string message)
    {
        if (IsServer)
        {
            return;
        }

        GameFlowUI.Show(message);
        StartCoroutine(ShutdownAndLoadStartSceneRoutine());
    }

    [Rpc(SendTo.NotServer)]
    private void ApplyStateRpc(
        bool isWhiteTurn,
        int whiteHealth,
        int blackHealth,
        string cardState,
        bool canDrawThisTurn
    )
    {
        if (IsServer)
        {
            return;
        }

        ResolveReferences();
        logicManager?.ApplyRemoteNetworkState(
            isWhiteTurn,
            whiteHealth,
            blackHealth,
            cardState,
            canDrawThisTurn
        );
    }

    [Rpc(SendTo.NotServer)]
    private void ApplyGameOverRpc(string result)
    {
        if (IsServer)
        {
            return;
        }

        ResolveReferences();
        logicManager?.ApplyRemoteGameOver(result);
    }

    [Rpc(SendTo.NotServer)]
    private void ApplyDamageCalculationBatchRpc(string payload)
    {
        if (IsServer)
        {
            return;
        }

        ResolveReferences();
        logicManager?.PlayRemoteDamageCalculationBatch(payload);
    }

    [Rpc(SendTo.NotServer)]
    private void ApplyPhaseChangeRpc(string message)
    {
        if (IsServer)
        {
            return;
        }

        GameFlowUI.PlayPhase(this, message, null);
    }

    [Rpc(SendTo.NotServer)]
    private void ApplyMoveResultRpc(
        int sequence,
        bool isWhitePlayer,
        int fromX,
        int fromY,
        int toX,
        int toY,
        bool finalIsWhiteTurn,
        int finalWhiteHealth,
        int finalBlackHealth,
        string finalCardState,
        bool finalCanDrawThisTurn
    )
    {
        if (IsServer)
        {
            return;
        }

        ResolveReferences();

        BoardCoordinate from = new BoardCoordinate(fromX, fromY);
        BoardCoordinate to = new BoardCoordinate(toX, toY);
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

        if (logicManager != null)
        {
            logicManager.ApplyRemoteNetworkState(
                finalIsWhiteTurn,
                finalWhiteHealth,
                finalBlackHealth,
                finalCardState,
                finalCanDrawThisTurn
            );
        }
    }

    [Rpc(SendTo.NotServer)]
    private void ApplyPromotionResultRpc(
        int sequence,
        bool isWhitePlayer,
        int x,
        int y,
        string pieceName
    )
    {
        if (IsServer)
        {
            return;
        }

        ResolveReferences();

        BoardCoordinate coordinate = new BoardCoordinate(x, y);
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

    [Rpc(SendTo.NotServer)]
    private void ApplyCardOnPieceResultRpc(
        int sequence,
        bool isWhitePlayer,
        string cardId,
        int targetX,
        int targetY
    )
    {
        if (IsServer)
        {
            return;
        }

        ResolveReferences();

        BoardCoordinate target = new BoardCoordinate(targetX, targetY);
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

    [Rpc(SendTo.NotServer)]
    private void ApplyFieldCardResultRpc(
        int sequence,
        bool isWhitePlayer,
        string cardId,
        string fieldPlaceName
    )
    {
        if (IsServer)
        {
            return;
        }

        ResolveReferences();

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

    private void ExecutePromotionAsAuthority(
        int sequence,
        bool isWhitePlayer,
        BoardCoordinate coordinate,
        string pieceName,
        ulong senderClientId
    )
    {
        ResolveReferences();

        PlayerSide senderSide = GetSideForClient(senderClientId);
        bool senderIsWhite = senderSide == PlayerSide.White;

        if (senderSide == PlayerSide.None || senderIsWhite != isWhitePlayer)
        {
            AnnounceCommandResultRpc(
                (int)NetworkGameCommandKind.MovePiece,
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
            ApplyPromotionResultRpc(
                sequence,
                isWhitePlayer,
                coordinate.x,
                coordinate.y,
                pieceName
            );
        }

        AnnounceCommandResultRpc(
            (int)NetworkGameCommandKind.MovePiece,
            sequence,
            accepted,
            accepted
                ? $"Promotion applied: {pieceName}"
                : $"Promotion rejected: {pieceName}"
        );
    }

    private void ExecuteCommandAsAuthority(
        NetworkGameCommand command,
        ulong senderClientId
    )
    {
        ResolveReferences();

        if (!IsSenderAllowed(command, senderClientId, out string rejectReason))
        {
            LogCommand($"Rejected: {rejectReason}", command);
            AnnounceCommandResultRpc(
                (int)command.kind,
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
                    ApplyMoveResultRpc(
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
                    ApplyCardOnPieceResultRpc(
                        command.sequence,
                        command.isWhitePlayer,
                        command.cardId,
                        command.to.x,
                        command.to.y
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
                    ApplyFieldCardResultRpc(
                        command.sequence,
                        command.isWhitePlayer,
                        command.cardId,
                        command.fieldPlaceName
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
                message =
                    "Command reached host. Card commands are scaffolded only.";
                break;
        }

        LogCommand(message, command);
        AnnounceCommandResultRpc(
            (int)command.kind,
            command.sequence,
            accepted,
            message
        );
    }

    private bool IsSenderAllowed(
        NetworkGameCommand command,
        ulong senderClientId,
        out string reason
    )
    {
        PlayerSide senderSide = GetSideForClient(senderClientId);
        bool senderIsWhite = senderSide == PlayerSide.White;

        if (senderSide == PlayerSide.None)
        {
            reason = $"No side assigned to client {senderClientId}";
            return false;
        }

        if (senderIsWhite != command.isWhitePlayer)
        {
            reason =
                $"Client side mismatch. Client={senderSide}, CommandWhite={command.isWhitePlayer}";
            return false;
        }

        if (logicManager != null && logicManager.isWhiteTurn != command.isWhitePlayer)
        {
            reason = "Not this player's turn";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private PlayerSide GetSideForClient(ulong clientId)
    {
        if (
            NetworkManager != null &&
            IsServer &&
            clientId == NetworkManager.LocalClientId
        )
        {
            return hostSide;
        }

        return firstRemoteClientSide;
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

    private void RestartGameAsAuthority()
    {
        if (isRestartingGame)
        {
            return;
        }

        isRestartingGame = true;
        hasFirstRemoteClientDeck = requiredPlayerCount <= 1;

        if (!IsOnline)
        {
            LoadSceneLocal(gameSceneName);
            return;
        }

        if (NetworkManager == null || NetworkManager.SceneManager == null)
        {
            Debug.LogError(
                "[NetworkGame] Cannot restart without NetworkSceneManager."
            );
            isRestartingGame = false;
            return;
        }

        NetworkManager.SceneManager.LoadScene(
            gameSceneName,
            LoadSceneMode.Single
        );
        Debug.Log($"[NetworkGame][Restart] Loading scene: {gameSceneName}");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (NetworkManager == null)
        {
            return;
        }

        if (
            NetworkManager.IsClient &&
            clientId == NetworkManager.LocalClientId &&
            !isReturningToStartScene &&
            !isRestartingGame
        )
        {
            Debug.Log("[NetworkGame][Disconnected] Returning to StartScene.");
            LoadSceneLocal(startSceneName);
            return;
        }

        if (NetworkManager.IsServer && clientId != NetworkManager.LocalClientId)
        {
            hasFirstRemoteClientDeck = false;
            Debug.Log($"[NetworkGame][ClientDisconnected] Client={clientId}");
            Debug.Log("[NetworkGame][DisconnectedRestart] Resetting game for reconnect.");
            RestartGameAsAuthority();
        }
    }

    private IEnumerator ShutdownAndLoadStartSceneRoutine()
    {
        if (isReturningToStartScene)
        {
            yield break;
        }

        isReturningToStartScene = true;

        yield return new WaitForSecondsRealtime(0.1f);

        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening)
        {
            manager.Shutdown();
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
