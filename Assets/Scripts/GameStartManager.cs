using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class GameStartManager : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private Button GameStartBtn;
    [SerializeField] private Avatar CatcherAvatar;
    [SerializeField] private GameObject boundariesBeforeGameStart;
    [SerializeField] private RectTransform gameStartCanvas;
    [SerializeField] private RectTransform lobbyCanvas;
    // Populated externally when players connect (Auth ID → Netcode Client ID)
    // e.g. fill this from your player spawn manager on client connect
    public static Dictionary<string, ulong> AuthToClientId = new();

    public void StartGame()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var connectedClientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        if (connectedClientIds.Count == 0) return;

        ulong catcherClientId = connectedClientIds[Random.Range(0, connectedClientIds.Count)];

        // Get the player object for that client and assign catcher role
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(catcherClientId, out var client))
        {
            client.PlayerObject.GetComponent<PlayerVisuals>().AssignAsCatcher();
            GameSessionData.Instance.CatcherPlayerId = catcherClientId.ToString();
            Debug.Log($"[GameStartManager] Catcher assigned: client {catcherClientId}");
        }
        else
        {
            Debug.LogError($"[GameStartManager] Could not find player object for client {catcherClientId}");
        }
        StartGameForEveryClientClientRpc();
    }

    [ClientRpc]
    private void MakePlayerCatcherClientRpc(ulong catcherClientId, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[GameStartManager] I am the Catcher! Client ID: {catcherClientId}");
        debugText.text = $"[GameStartManager] I am the Catcher! Client ID: {catcherClientId}";
        // assign catcher role to local player here
    }

    [ClientRpc]
    void StartGameForEveryClientClientRpc()
    {
        GameSessionData.Instance.HasGameStartedYet = true;
        boundariesBeforeGameStart.SetActive(false);
        gameStartCanvas.localScale = (Vector3.one);
        lobbyCanvas.localScale = (Vector3.zero);
    }
}