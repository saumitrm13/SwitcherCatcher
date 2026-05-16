using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

public class Netcode_Functions : NetworkBehaviour
{
    public static Netcode_Functions Instance { get; private set; }

    [SerializeField] NetworkObject catcherPrefab;
    [SerializeField] NetworkObject swithcer1Prefab;

    [SerializeField] Transform[] spawnPoints;
    [SerializeField] TextMeshProUGUI debugText;

    // Maps clientId → Unity Auth player ID, populated when each client registers
    private Dictionary<ulong, string> clientAuthIds = new Dictionary<ulong, string>();

    // Tracks which clients have already been spawned so we don't double-spawn
    private HashSet<ulong> spawnedClients = new HashSet<ulong>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    // Prevents NetworkManager from auto-spawning any player object.
    // We handle all spawning manually in OnClientConnected / RegisterClientAuthId.
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        response.CreatePlayerObject = false;
        response.Approved = true;
    }

    // Fires when a client connects. At this point their Auth ID may not be
    // registered yet (the ServerRpc hasn't arrived), so we just wait.
    // Once RegisterClientAuthId() is called the spawn will be triggered.
    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        TrySpawnPlayer(clientId);
    }

    // Called by each player's ServerRpc right after they spawn on the network.
    // Once we have the Auth ID we attempt the spawn.
    public void RegisterClientAuthId(ulong clientId, string authId)
    {
        if (!IsServer) return;
        clientAuthIds[clientId] = authId;
        TrySpawnPlayer(clientId);
    }

    private void TrySpawnPlayer(ulong clientId)
    {
        // Not ready yet — wait for RegisterClientAuthId to be called
        if (!clientAuthIds.TryGetValue(clientId, out string authId)) return;

        // Already spawned, do nothing
        if (spawnedClients.Contains(clientId)) return;

        spawnedClients.Add(clientId);

        string catcherAuthId = GameSessionData.Instance.CatcherPlayerId;
        bool isCatcher = (authId == catcherAuthId);

        NetworkObject prefabToSpawn = isCatcher ? catcherPrefab : swithcer1Prefab;

        // Use modulo so we never go out of bounds regardless of player count
        int spawnIndex = (spawnedClients.Count - 1) % spawnPoints.Length;
        Transform spawnPoint = spawnPoints[spawnIndex];

        if (debugText != null)
            debugText.text += $"\nSpawning {'{'}{(isCatcher ? "Catcher" : "Switcher")}{'}'}  for client {clientId}";

        SpawnPlayer(clientId, prefabToSpawn, spawnPoint);
    }

    private void SpawnPlayer(ulong clientId, NetworkObject playerPrefab, Transform spawnTransform)
    {
        var obj = Instantiate(playerPrefab, spawnTransform.position, spawnTransform.rotation);
        obj.SpawnAsPlayerObject(clientId);
    }
}