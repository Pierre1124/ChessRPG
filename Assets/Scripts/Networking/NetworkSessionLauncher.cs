using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkSessionLauncher : MonoBehaviourPunCallbacks
{
    [Header("Photon Room")]
    [SerializeField] private string roomName = "ChessRPG";
    [SerializeField, Min(2)] private byte maxPlayers = 2;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "ChessScene";
    [SerializeField] private bool loadGameSceneAfterHostStarts = true;

    private bool wantsHost;
    private bool wantsClient;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = Application.version;
    }

    public void StartHost()
    {
        wantsHost = true;
        wantsClient = false;
        ConnectOrJoinRoom();
    }

    public void StartClient()
    {
        wantsHost = false;
        wantsClient = true;
        ConnectOrJoinRoom();
    }

    public void Shutdown()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        Debug.Log("[NetworkGame] Photon shutdown.");
    }

    public void SetAddress(string newAddress)
    {
        roomName = string.IsNullOrWhiteSpace(newAddress)
            ? "ChessRPG"
            : newAddress.Trim();
    }

    public void SetGameSceneName(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            gameSceneName = sceneName.Trim();
        }
    }

    private void ConnectOrJoinRoom()
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            roomName = "ChessRPG";
        }

        PhotonNetwork.AutomaticallySyncScene = true;

        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinOrCreateConfiguredRoom();
            return;
        }

        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("[NetworkGame] Photon is connecting. Waiting for ready state.");
            return;
        }

        Debug.Log($"[NetworkGame] Connecting to Photon. Room={roomName}");
        bool started = PhotonNetwork.ConnectUsingSettings();
        if (!started)
        {
            Debug.LogError(
                "[NetworkGame] Photon ConnectUsingSettings failed. " +
                "Check PhotonServerSettings AppIdRealtime."
            );
        }
    }

    public override void OnConnectedToMaster()
    {
        JoinOrCreateConfiguredRoom();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log(
            $"[NetworkGame] Joined Photon room={PhotonNetwork.CurrentRoom.Name} | " +
            $"Master={PhotonNetwork.IsMasterClient} | Players={PhotonNetwork.CurrentRoom.PlayerCount}"
        );

        if (
            wantsHost &&
            PhotonNetwork.IsMasterClient &&
            loadGameSceneAfterHostStarts &&
            !string.IsNullOrWhiteSpace(gameSceneName)
        )
        {
            PhotonNetwork.LoadLevel(gameSceneName);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError(
            $"[NetworkGame] Join room failed: {returnCode} {message}"
        );
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError(
            $"[NetworkGame] Create room failed: {returnCode} {message}"
        );
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"[NetworkGame] Photon disconnected: {cause}");
    }

    private void JoinOrCreateConfiguredRoom()
    {
        if (!wantsHost && !wantsClient)
        {
            return;
        }

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            EmptyRoomTtl = 0,
            PlayerTtl = 0,
            CleanupCacheOnLeave = true
        };

        if (wantsHost)
        {
            PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
            Debug.Log($"[NetworkGame] Host JoinOrCreateRoom: {roomName}");
            return;
        }

        PhotonNetwork.JoinRoom(roomName);
        Debug.Log($"[NetworkGame] Client JoinRoom: {roomName}");
    }
}
