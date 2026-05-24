using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

public class Netcode_Functions : NetworkBehaviour
{
    public static Netcode_Functions Instance { get; private set; }

    [SerializeField] TextMeshProUGUI debugText;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Register ConnectionApprovalCallback for host validation
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;

        if (debugText != null)
            debugText.text = "Network initialized";
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
    }

    // Approve all clients for connection
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        // Allow NetworkManager to create the default player object
        response.CreatePlayerObject = true;
        response.Approved = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RegisterPlayerNameServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        GameSessionData.Instance?.RegisterName(clientId, playerName);
        SyncPlayerNameClientRpc(clientId, playerName);
        Debug.Log($"[Netcode_Functions] Registered '{playerName}' for client {clientId}");
    }

    [ClientRpc]
    void SyncPlayerNameClientRpc(ulong clientId, string playerName)
    {
        // Server already called RegisterName above, skip to avoid double-firing the event
        if (IsServer) return;
        GameSessionData.Instance?.RegisterName(clientId, playerName);
    }
}