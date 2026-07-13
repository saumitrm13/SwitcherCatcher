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
    public void RegisterPlayerNameServerRpc(string playerName, string lobbyPlayerId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        GameSessionData.Instance?.RegisterName(clientId, playerName);
        GameSessionData.Instance?.RegisterLobbyPlayerId(clientId, lobbyPlayerId);
        SendExistingMappingsToClient(clientId);
        SyncPlayerNameClientRpc(clientId, playerName, lobbyPlayerId);
    }

    [ClientRpc]
    void SyncPlayerNameClientRpc(ulong clientId, string playerName, string lobbyPlayerId, ClientRpcParams clientRpcParams = default)
    {
        if (IsServer) return;
        GameSessionData.Instance?.RegisterName(clientId, playerName);
        GameSessionData.Instance?.RegisterLobbyPlayerId(clientId, lobbyPlayerId);
    }

    void SendExistingMappingsToClient(ulong newClientId)
    {
        var targetParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { newClientId } }
        };

        foreach (var kvp in GameSessionData.Instance.ClientIdToName)
        {
            string lobbyId = GameSessionData.Instance.ClientIdToLobbyPlayerId.TryGetValue(kvp.Key, out var id) ? id : null;
            SyncPlayerNameClientRpc(kvp.Key, kvp.Value, lobbyId, targetParams);
        }
    }
}