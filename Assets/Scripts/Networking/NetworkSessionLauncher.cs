using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkSessionLauncher : MonoBehaviourPunCallbacks
{
    [Header("Photon Room")]
    [SerializeField] private string roomName = "ChessRPG";
    [SerializeField, Range(2, 2), Tooltip("目前規則只支援兩位玩家，房間固定為雙人。 ")] private byte maxPlayers = 2;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "ChessScene";
    [SerializeField] private bool loadGameSceneAfterHostStarts = true;

    private bool wantsHost;
    private bool wantsClient;

    /// <summary>在選單或棋局的現有提示介面顯示連線狀態。</summary>
    private void ShowFeedback(string message)
    {
        StartMenuController menu = FindFirstObjectByType<StartMenuController>();
        if (menu != null) menu.ShowStatusMessage(message);
        else GameFlowUI.Show(message);
    }

    /// <summary>
    /// 啟用 Photon 場景同步，並以應用程式版本設定連線版本。
    /// </summary>
    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = Application.version + "-private-cards-v1";
    }

    /// <summary>
    /// 記錄開房意圖並連線到 Photon，準備加入或建立房間。
    /// </summary>
    public void StartHost()
    {
        wantsHost = true;
        wantsClient = false;
        ConnectOrJoinRoom();
    }

    /// <summary>
    /// 記錄加入意圖並連線到 Photon，準備加入指定房間。
    /// </summary>
    public void StartClient()
    {
        wantsHost = false;
        wantsClient = true;
        ConnectOrJoinRoom();
    }

    /// <summary>
    /// 要求中斷目前 Photon 連線。
    /// </summary>
    public void Shutdown()
    {
        wantsHost = false;
        wantsClient = false;
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        Debug.Log("[NetworkGame] Photon shutdown.");
    }

    /// <summary>
    /// 將輸入文字設為 Photon 房間名稱；空白時使用預設名稱。
    /// </summary>
    public void SetAddress(string newAddress)
    {
        roomName = string.IsNullOrWhiteSpace(newAddress)
            ? "ChessRPG"
            : newAddress.Trim();
    }

    /// <summary>
    /// 以有效的輸入更新遊戲場景名稱。
    /// </summary>
    public void SetGameSceneName(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            gameSceneName = sceneName.Trim();
        }
    }

    /// <summary>
    /// 依 Photon 連線狀態開始連線、等待完成或直接加入房間。
    /// </summary>
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

        ShowFeedback("正在連線，請稍候");
        Debug.Log($"[NetworkGame] Connecting to Photon. Room={roomName}");
        bool started = PhotonNetwork.ConnectUsingSettings();
        if (!started)
        {
            ShowFeedback("無法開始連線，請稍後重試");
            Debug.LogError(
                "[NetworkGame] Photon ConnectUsingSettings failed. " +
                "Check PhotonServerSettings AppIdRealtime."
            );
        }
    }

    /// <summary>
    /// 連上 Photon Master Server 後執行先前要求的房間操作。
    /// </summary>
    public override void OnConnectedToMaster()
    {
        JoinOrCreateConfiguredRoom();
    }

    /// <summary>
    /// 加入房間後更新連線狀態，並依角色啟動場景或對局準備流程。
    /// </summary>
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

    /// <summary>
    /// 將加入房間失敗的代碼與訊息寫入紀錄。
    /// </summary>
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        ShowFeedback("無法加入房間，請確認房名、遊戲版本及是否已滿員，再重新加入");
        Debug.LogError(
            $"[NetworkGame] Join room failed: {returnCode} {message}"
        );
    }

    /// <summary>
    /// 將建立房間失敗的代碼與訊息寫入紀錄。
    /// </summary>
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        ShowFeedback("建立房間失敗，請稍後重試或更換房名");
        Debug.LogError(
            $"[NetworkGame] Create room failed: {returnCode} {message}"
        );
    }

    /// <summary>
    /// 處理 Photon 斷線通知，依目前離場流程更新本機狀態。
    /// </summary>
    public override void OnDisconnected(DisconnectCause cause)
    {
        if (cause != DisconnectCause.DisconnectByClientLogic && (wantsHost || wantsClient))
            ShowFeedback("連線中斷，請確認網路後重新加入房間");
        Debug.Log($"[NetworkGame] Photon disconnected: {cause}");
    }

    /// <summary>
    /// 依開房或加入意圖，以既有房間選項連接指定房間。
    /// </summary>
    private void JoinOrCreateConfiguredRoom()
    {
        if (!wantsHost && !wantsClient)
        {
            return;
        }

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 2,
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
