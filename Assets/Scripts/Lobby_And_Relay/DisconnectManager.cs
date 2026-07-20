using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using UnityEngine;

/// <summary>
/// Handles mid-game disconnects after GameStartManager.StartGame() has been called.
///
/// Client (non-host) leaves:
///   - Server detects it via NetworkManager.OnClientDisconnectCallback.
///   - Server broadcasts the leaving player's ClientId to everyone still connected.
///   - Every machine (including the server/host) looks up that player's NetworkObject
///     via NetworkManager.SpawnManager.SpawnedObjectsList (still valid at the moment
///     the RPC handler runs, since we send it out before Netcode fully despawns/removes
///     the entry) and deactivates the player's root GameObject locally.
///
/// Host leaves:
///   - Every remaining client detects the server going away via
///     NetworkManager.Singleton.OnClientStopped (fires locally, no RPC possible —
///     the host is gone by definition).
///   - Each client independently: shuts down its own NetworkManager, deletes any
///     local lobby reference / best-effort deletes the lobby, resets GameSessionData,
///     and activates the lobby canvas with FirstPanel.
///   - The host, if it detects its own NetworkManager stopping unexpectedly (e.g. via
///     OnServerStopped) after the game had started, runs the same cleanup and — because
///     only the host can actually delete the Lobby via the Lobby Service — makes a
///     best-effort DeleteLobbyAsync call first.
/// </summary>
public class DisconnectManager : NetworkBehaviour
{
    public static DisconnectManager Instance { get; private set; }

    [Header("Scene References (assign in Inspector)")]
    [Tooltip("Root canvas / GameObject for the lobby UI to re-activate after a host-leave cleanup.")]
    [SerializeField] private GameObject lobbyCanvas;
    [Tooltip("The default first panel inside the lobby canvas to activate (e.g. main menu panel).")]
    [SerializeField] private GameObject firstPanel;
    [Tooltip("The in-game canvas / boundaries object that should be hidden once the game stops.")]
    [SerializeField] private GameObject gameSessionRoot;
    [Tooltip("Reference to the LobbyCanvasFunction for panel switching, optional convenience.")]
    [SerializeField] private LobbyCanvasFunction lobbyCanvasFunction;
    [SerializeField] private RectTransform gameCanvasPanel;
    // Tracks whether the game has actually started, so pre-game lobby disconnects
    // (still in the lobby, nobody spawned as a gameplay player yet) don't trigger
    // this heavier mid-game cleanup path. Static + set on the watchdog so it
    // survives independently of this NetworkBehaviour's own spawn/despawn.
    private static bool gameHasStarted = false;

    private void Awake()
    {
        // Set Instance in Awake (regular MonoBehaviour lifecycle, runs on scene
        // load) rather than waiting for OnNetworkSpawn. OnNetworkSpawn timing is
        // subject to Netcode spawn-order/synchronization, which is exactly the
        // kind of race that caused MarkGameStarted() calls to be silently
        // swallowed by `Instance?.` on some clients. Awake() always runs as soon
        // as the GameObject exists in the scene, independent of network state.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnServer_ClientDisconnected;
        }

        // IMPORTANT: do NOT subscribe OnClientStopped here.
        // This component lives on a NetworkObject, and Netcode may despawn/destroy
        // networked objects (running OnNetworkDespawn, which unsubscribes below,
        // or destroying the GameObject outright) as part of the very shutdown
        // sequence that also raises OnClientStopped — the ordering between the
        // two is not guaranteed, so a NetworkBehaviour can miss its own
        // disconnect notification. See HostDisconnectWatchdog for the actual
        // subscription, which lives on a plain, non-networked MonoBehaviour.
        HostDisconnectWatchdog.EnsureExists(this);
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton == null) return;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnServer_ClientDisconnected;
        }
    }

    /// <summary>
    /// Call this once StartGame() actually kicks off gameplay. Static and
    /// side-effect-only on a plain flag so it can never be silently dropped
    /// by an `Instance?.` null-check race — callers should call
    /// DisconnectManager.MarkGameStarted() directly rather than going through
    /// Instance.
    /// </summary>
    public static void MarkGameStarted()
    {
        gameHasStarted = true;
    }

    /// <summary>
    /// Runs the same "host left" cleanup as the instance method below, but is
    /// safe to call from the watchdog even if this NetworkBehaviour instance
    /// has already been destroyed by Netcode's teardown — everything it touches
    /// here is either static or resolved fresh via FindAnyObjectByType.
    /// </summary>
    public static void RunHostLeftCleanupStatic()
    {
        gameHasStarted = false;

        GameSessionData.Instance?.ResetSession();

        var manager = Instance;
        if (manager != null)
        {
            manager.RunGameStoppedCleanupInstance();
        }
        else
        {
            Debug.LogWarning("[DisconnectManager] Instance was null during host-leave cleanup; falling back to FindAnyObjectByType for panel activation.");

            var lobbyFunctions = FindAnyObjectByType<LobbyCanvasFunction>();
            if (lobbyFunctions != null)
            {
                lobbyFunctions.gameObject.SetActive(true);
                lobbyFunctions.transform.localScale = Vector3.one;
            }
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }

    public static async void RunHostLeftLobbyDeleteThenCleanup(bool wasHost)
    {
        if (wasHost)
        {
            await TryDeleteLobbyBestEffortStatic();
        }
        RunHostLeftCleanupStatic();
    }

    private static async System.Threading.Tasks.Task TryDeleteLobbyBestEffortStatic()
    {
        var lobby = LobbyFeatures.GetCurrentLobby();
        if (lobby == null) return;

        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(lobby.Id);
            Debug.Log("[DisconnectManager] Deleted lobby after host left mid-game.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[DisconnectManager] Lobby delete on host-leave failed/skip: {e.Message}");
        }
        finally
        {
            LobbyFeatures.SetCurrentLobby(null);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // CLIENT (NON-HOST) LEAVES
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Server-only. Fires for every client (including ones that were never fully
    /// spawned) whenever they disconnect. We only care about this once the game
    /// has actually started — pre-game lobby drops are handled by LobbyFeatures.
    /// </summary>
    private void OnServer_ClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (!gameHasStarted)
        {
            // Pre-game: player was still in the lobby. Remove them from the
            // Unity lobby so the lobby player list stays consistent for everyone.
            TryRemovePlayerFromLobbyAsync(clientId);
            return;
        }

        // Mid-game: broadcast the departure so every client can react.
        Debug.Log($"[DisconnectManager] Client {clientId} disconnected mid-game.");
        NotifyClientLeftClientRpc(clientId);
    }

    /// <summary>
    /// Best-effort async lobby removal for a client that dropped before the game
    /// started. Uses GameSessionData.ClientIdToLobbyPlayerId to map the Netcode
    /// clientId back to the Unity Services Player ID that the Lobby API requires.
    /// Only the host can call RemovePlayerAsync, which is fine — this method is
    /// server-only and in this project the host IS the relay server.
    /// </summary>
    private static async void TryRemovePlayerFromLobbyAsync(ulong clientId)
    {
        var lobby = LobbyFeatures.GetCurrentLobby();
        if (lobby == null) return;

        if (GameSessionData.Instance == null ||
            !GameSessionData.Instance.ClientIdToLobbyPlayerId.TryGetValue(clientId, out string lobbyPlayerId))
        {
            Debug.LogWarning($"[DisconnectManager] No lobby player ID mapping found for client {clientId}; cannot remove from lobby.");
            return;
        }

        if (lobbyPlayerId == AuthenticationService.Instance.PlayerId)
        {
            Debug.LogWarning("[DisconnectManager] Attempted to remove host from lobby — skipping.");
            return;
        }

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(lobby.Id, lobbyPlayerId);
            Debug.Log($"[DisconnectManager] Removed lobby player {lobbyPlayerId} (client {clientId}) after pre-game disconnect.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[DisconnectManager] Failed to remove lobby player {lobbyPlayerId}: {e.Message}");
        }
        finally
        {
            GameSessionData.Instance?.RemoveClient(clientId);
        }
    }

    /// <summary>
    /// Runs on every machine still connected (server + remaining clients).
    /// Finds the leaving player's NetworkObject and deactivates it locally —
    /// no destruction/despawn is requested here; Netcode's own despawn/cleanup
    /// for the disconnected client's objects proceeds independently on the server.
    /// </summary>
    [ClientRpc]
    private void NotifyClientLeftClientRpc(ulong leftClientId)
    {
        GameObject leftPlayerObject = ResolvePlayerObject(leftClientId);
        if (leftPlayerObject == null)
        {
            Debug.LogWarning($"[DisconnectManager] Could not resolve player object for departed client {leftClientId}.");
            return;
        }

        Debug.Log($"[DisconnectManager] Deactivating network prefab for departed client {leftClientId}.");
        leftPlayerObject.SetActive(false);
    }

    private GameObject ResolvePlayerObject(ulong clientId)
    {
        // Prefer the authoritative ConnectedClients map when available (server, or
        // a client with visibility into it); fall back to scanning spawned objects
        // for the matching OwnerClientId, since a plain client won't have
        // ConnectedClients populated for peers.
        if (NetworkManager.Singleton.IsServer &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) &&
            client.PlayerObject != null)
        {
            return client.PlayerObject.gameObject;
        }

        foreach (var kvp in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            NetworkObject networkObject = kvp;
            if (networkObject != null && networkObject.IsPlayerObject && networkObject.OwnerClientId == clientId)
            {
                return networkObject.gameObject;
            }
        }

        return null;
    }

    // ────────────────────────────────────────────────────────────────────────
    // HOST LEAVES — actual UI/scene cleanup, invoked via the static entry
    // points above once the watchdog detects OnClientStopped.
    // ────────────────────────────────────────────────────────────────────────

    private void RunGameStoppedCleanupInstance()
    {
        if (gameCanvasPanel != null)
        {
            gameCanvasPanel.localScale = Vector3.zero;
        }

        // This project shows/hides the lobby canvas via localScale (see
        // GameStartManager.StartGameForEveryClientClientRpc, which sets
        // lobbyCanvas.localScale = Vector3.zero on game start) rather than
        // SetActive. SetActive(true) alone leaves it scaled to zero and
        // therefore invisible even though it's "active" in the hierarchy —
        // restore the scale explicitly here regardless of which activation
        // path below is taken.
        if (lobbyCanvas != null)
        {
            lobbyCanvas.transform.localScale = Vector3.one;
            lobbyCanvas.SetActive(true);
        }

        if (lobbyCanvasFunction != null && firstPanel != null)
        {
            lobbyCanvasFunction.ActivatePanel(firstPanel);
        }
        else if (firstPanel != null)
        {
            firstPanel.SetActive(true);
        }
    }
}