using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkSessionLauncher : MonoBehaviour
{
    [Header("Direct Connect")]
    [SerializeField] private string address = "127.0.0.1";
    [SerializeField] private ushort port = 7777;
    [SerializeField] private string serverListenAddress = "0.0.0.0";

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "ChessScene";
    [SerializeField] private bool loadGameSceneAfterHostStarts = true;

    public void StartHost()
    {
        if (!PrepareTransport())
        {
            return;
        }

        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log("[NetworkGame] Host started.");
            LoadGameSceneForHost();
        }
        else
        {
            Debug.LogError("[NetworkGame] Failed to start Host.");
        }
    }

    public void StartClient()
    {
        if (!PrepareTransport())
        {
            return;
        }

        if (NetworkManager.Singleton.StartClient())
        {
            Debug.Log("[NetworkGame] Client started.");
        }
        else
        {
            Debug.LogError("[NetworkGame] Failed to start Client.");
        }
    }

    public void Shutdown()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.Shutdown();
        Debug.Log("[NetworkGame] Network shutdown.");
    }

    public void SetAddress(string newAddress)
    {
        address = string.IsNullOrWhiteSpace(newAddress)
            ? "127.0.0.1"
            : newAddress.Trim();
    }

    public void SetGameSceneName(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            gameSceneName = sceneName.Trim();
        }
    }

    private void LoadGameSceneForHost()
    {
        if (
            !loadGameSceneAfterHostStarts ||
            string.IsNullOrWhiteSpace(gameSceneName)
        )
        {
            return;
        }

        if (
            NetworkManager.Singleton == null ||
            NetworkManager.Singleton.SceneManager == null
        )
        {
            Debug.LogError(
                "[NetworkGame] Cannot load game scene without NetworkSceneManager."
            );
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(
            gameSceneName,
            LoadSceneMode.Single
        );
        Debug.Log($"[NetworkGame] Host loading scene: {gameSceneName}");
    }

    private bool PrepareTransport()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError(
                "[NetworkGame] Scene requires a NetworkManager."
            );
            return false;
        }

        UnityTransport transport =
            NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError(
                "[NetworkGame] NetworkManager requires UnityTransport."
            );
            return false;
        }

        transport.SetConnectionData(
            address,
            port,
            serverListenAddress
        );
        return true;
    }
}
