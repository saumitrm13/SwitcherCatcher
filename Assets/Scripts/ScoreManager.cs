using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Placed once in the scene (on a persistent GameObject or the NetworkManager object).
/// Keeps a NetworkList of per-client scores that is automatically replicated to every
/// connected client whenever a value changes.
/// </summary>

// ── Serialisable score entry ─────────────────────────────────────────────────
public struct PlayerScore : INetworkSerializable, System.IEquatable<PlayerScore>
{
    public ulong ClientId;
    public int Score;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Score);
    }

    public bool Equals(PlayerScore other) =>
        ClientId == other.ClientId && Score == other.Score;

    public override string ToString() => $"[{ClientId}] {Score}";
}

// ── Manager ──────────────────────────────────────────────────────────────────
public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Point Values")]
    [Tooltip("Points awarded to a Switcher when they return to their owned pole with resources.")]
    [SerializeField] int switcherSavePoints = 10;

    [Tooltip("Points awarded to the Catcher when they tag a Switcher outside a safe zone.")]
    [SerializeField] int catcherCatchPoints = 20;

    // ── Replicated state ─────────────────────────────────────────────────────
    // NetworkList must be created in Awake — not as a field initialiser.
    public NetworkList<PlayerScore> PlayerScores { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        PlayerScores = new NetworkList<PlayerScore>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Seed an entry for every client that is already connected
            // (the host is already connected when the game object spawns)
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                EnsureEntryExists(clientId);

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        // Always clear locally too — client-side NetworkList also needs to drop stale state
        if (PlayerScores != null && PlayerScores.Count > 0)
            PlayerScores.Clear();
    }

    // ── Server callbacks ──────────────────────────────────────────────────────
    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        EnsureEntryExists(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        RemovePlayerScore(clientId);
        GameSessionData.Instance?.RemoveClient(clientId);
    }

    /// <summary>
    /// Server-only. Removes a player's score entry from the replicated list.
    /// Triggers OnListChanged on all clients so ScoreboardUI refreshes automatically.
    /// </summary>
    public void RemovePlayerScore(ulong clientId)
    {
        if (!IsServer) return;
        for (int i = 0; i < PlayerScores.Count; i++)
        {
            if (PlayerScores[i].ClientId == clientId)
            {
                PlayerScores.RemoveAt(i);
                Debug.Log($"[ScoreManager] Removed score entry for departed client {clientId}.");
                return;
            }
        }
    }

    // ── Public scoring API (call only on the server) ──────────────────────────

    /// <summary>Called by SwitcherScript when a switcher saves their pole.</summary>
    public void AddSwitcherSaveScore(ulong clientId)
    {
        if (!IsServer) return;
        AddPoints(clientId, switcherSavePoints);
        AnnounceScoreClientRpc(clientId, switcherSavePoints, "pole saved");
    }

    /// <summary>Called by CatcherScript when the catcher makes a valid tag.</summary>
    public void AddCatcherCatchScore(ulong catcherClientId)
    {
        if (!IsServer) return;
        AddPoints(catcherClientId, catcherCatchPoints);
        AnnounceScoreClientRpc(catcherClientId, catcherCatchPoints, "switcher caught");
    }

    // ── Internals ─────────────────────────────────────────────────────────────
    private void EnsureEntryExists(ulong clientId)
    {
        for (int i = 0; i < PlayerScores.Count; i++)
            if (PlayerScores[i].ClientId == clientId) return;

        PlayerScores.Add(new PlayerScore { ClientId = clientId, Score = 0 });
    }

    private void AddPoints(ulong clientId, int points)
    {
        EnsureEntryExists(clientId);

        for (int i = 0; i < PlayerScores.Count; i++)
        {
            if (PlayerScores[i].ClientId != clientId) continue;

            // NetworkList requires replacing the whole struct to trigger replication
            PlayerScores[i] = new PlayerScore
            {
                ClientId = clientId,
                Score = PlayerScores[i].Score + points
            };
            return;
        }
    }

    // ── Broadcast ─────────────────────────────────────────────────────────────
    [ClientRpc]
    private void AnnounceScoreClientRpc(ulong clientId, int points, string reason)
    {
        // Scoreboard UI listens to PlayerScores.OnListChanged for live updates.
        // This RPC is a convenience hook for logging / toast notifications.
        Debug.Log($"[ScoreManager] Client {clientId} +{points} pts ({reason})");
    }
}