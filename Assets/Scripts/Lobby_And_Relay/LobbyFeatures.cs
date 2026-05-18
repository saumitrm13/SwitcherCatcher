using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyFeatures : MonoBehaviour
{
    [SerializeField] private GameObject gameSessionContainer; // Assign in Inspector
    [SerializeField] private GameObject lobbyUIContainer;      // Assign in Inspector
    
    [SerializeField] private TextMeshProUGUI lobbyNameForDisplayText;
    [SerializeField] private TextMeshProUGUI lobbyJoinCodeForDisplayText;
    [SerializeField] private GameObject playerInfoPrefab;
    [SerializeField] private RectTransform verticalLayoutGroupForPlayerInfo;
    [SerializeField] private GameObject DeleteLobbyBtn;
    [SerializeField] private LobbyCanvasFunction lobbyFunctions;
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private GameObject FirstPanel;
    [SerializeField] private GameObject startGameButton;
    [SerializeField] private NetworkObject PlayerPrefab;
    private const float HeartbeatInterval = 15f;

    private static Lobby currentLobby;

    // Static so the subscription survives panel hide/show cycles.
    // The subscription belongs to the lobby session, not the panel lifecycle.
    private static ILobbyEvents _lobbyEvents;
    private static LobbyEventCallbacks _lobbyEventCallbacks;
    private static bool _isSubscribed = false;

    private Coroutine _heartbeatCoroutine;

    // ─── OnEnable: just refresh the display, never re-subscribe ──────────────
    private void OnEnable()
    {
        Debug.Log($"=== NetworkManager Configuration ===");
        Debug.Log($"NetworkManager.Singleton exists: {NetworkManager.Singleton != null}");
        if (NetworkManager.Singleton != null)
        {
            Debug.Log($"IsHost: {NetworkManager.Singleton.IsHost}");
            Debug.Log($"IsClient: {NetworkManager.Singleton.IsClient}");
            Debug.Log($"IsServer: {NetworkManager.Singleton.IsServer}");
            Debug.Log($"LocalClientId: {NetworkManager.Singleton.LocalClientId}");
            
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                Debug.Log($"UnityTransport found");
            }
        }
        Debug.Log($"====================================");

        ShowLobbyInfo();

        // Cache the game session container reference
        if (_gameSessionContainer == null)
        {
            _gameSessionContainer = GameObject.Find("GameSessionContainer");
        }

        // Heartbeat is instance-scoped (needs StartCoroutine on a MonoBehaviour).
        // Safe to restart here — stopped cleanly in OnDisable.
        if (currentLobby != null && IsHost())
            _heartbeatCoroutine = StartCoroutine(HeartbeatCoroutine());
    }

    private void OnDisable()
    {
        StopHeartbeat();
        // Do NOT unsubscribe here — the lobby is still alive, we just hid the panel.
    }

    // ─── Call once, right after CreateLobby or JoinLobby succeeds ────────────
    /// <summary>
    /// Subscribes to lobby events for the current lobby session.
    /// Call exactly once per lobby join/create. Safe to call again after a
    /// clean Leave/Delete because _isSubscribed is reset in SetCurrentLobby(null).
    /// </summary>
    public static async System.Threading.Tasks.Task SubscribeToCurrentLobbyEvents()
    {
        if (_isSubscribed || currentLobby == null) return;

        _lobbyEventCallbacks = new LobbyEventCallbacks();

        _lobbyEventCallbacks.LobbyChanged += OnLobbyChangedStatic;
        _lobbyEventCallbacks.LobbyDeleted += OnLobbyDeletedStatic;
        _lobbyEventCallbacks.DataChanged += OnDataChangedStatic;
        _lobbyEventCallbacks.DataAdded += OnDataChangedStatic;
        _lobbyEventCallbacks.DataRemoved += OnDataChangedStatic;
        _lobbyEventCallbacks.PlayerJoined += OnPlayerJoinedStatic;
        _lobbyEventCallbacks.PlayerLeft += OnPlayerLeftStatic;
        _lobbyEventCallbacks.PlayerDataChanged += OnPlayerDataChangedStatic;
        _lobbyEventCallbacks.PlayerDataAdded += OnPlayerDataChangedStatic;
        _lobbyEventCallbacks.PlayerDataRemoved += OnPlayerDataChangedStatic;
        _lobbyEventCallbacks.KickedFromLobby += OnKickedFromLobbyStatic;
        _lobbyEventCallbacks.LobbyEventConnectionStateChanged += OnConnectionStateChangedStatic;

        _lobbyEvents = await LobbyService.Instance.SubscribeToLobbyEventsAsync(currentLobby.Id, _lobbyEventCallbacks);
        _isSubscribed = true;

        Debug.Log($"[LobbyFeatures] Subscribed to lobby events for {currentLobby.Id}");
    }

    public static async System.Threading.Tasks.Task UnsubscribeFromCurrentLobbyEvents()
    {
        if (!_isSubscribed || _lobbyEvents == null) return;

        await _lobbyEvents.UnsubscribeAsync();
        _lobbyEvents = null;
        _lobbyEventCallbacks = null;
        _isSubscribed = false;

        Debug.Log("[LobbyFeatures] Unsubscribed from lobby events");
    }

    // ─── Static event handlers (fire even when the panel is hidden) ───────────

    private static async void OnLobbyChangedStatic(ILobbyChanges changes)
    {
        if (currentLobby == null) return;
        changes.ApplyToLobby(currentLobby);
        await CheckAndJoinRelay();
        FindAnyObjectByType<LobbyFeatures>()?.ShowLobbyInfo();
    }

    private static void OnLobbyDeletedStatic()
    {
        Debug.Log("[LobbyFeatures] Lobby deleted.");
        _ = UnsubscribeFromCurrentLobbyEvents();
        SetCurrentLobby(null);
        FindAnyObjectByType<LobbyFeatures>()?.HandleLobbyGone("Your lobby was deleted");
    }

    private static async void OnDataChangedStatic(
     Dictionary<string, ChangedOrRemovedLobbyValue<DataObject>> _)
    {
        // ── Refresh the lobby object to get the latest data ────────────────────
        if (currentLobby != null)
        {
            try
            {
                currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                Debug.Log("[LobbyFeatures] Lobby data refreshed from server");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyFeatures] Failed to refresh lobby: {ex.Message}");
            }
        }

        await CheckAndJoinRelay();
        FindAnyObjectByType<LobbyFeatures>()?.ShowLobbyInfo();
    }
    private static void OnPlayerJoinedStatic(List<LobbyPlayerJoined> _)
        => FindAnyObjectByType<LobbyFeatures>()?.RefreshPlayerList();

    private static void OnPlayerLeftStatic(List<int> _)
        => FindAnyObjectByType<LobbyFeatures>()?.RefreshPlayerList();

    private static void OnPlayerDataChangedStatic(
        Dictionary<int, Dictionary<string, ChangedOrRemovedLobbyValue<PlayerDataObject>>> _)
        => FindAnyObjectByType<LobbyFeatures>()?.RefreshPlayerList();

    private static void OnKickedFromLobbyStatic()
    {
        Debug.Log("[LobbyFeatures] Kicked from lobby.");
        _ = UnsubscribeFromCurrentLobbyEvents();
        SetCurrentLobby(null);
        FindAnyObjectByType<LobbyFeatures>()?.HandleLobbyGone("Either your lobby was deleted or you were kicked out");
    }

    private static void OnConnectionStateChangedStatic(LobbyEventConnectionState state)
    {
        // Only log unexpected states — removes the constant console spam.
        if (state != LobbyEventConnectionState.Subscribed)
            Debug.LogWarning($"[LobbyFeatures] Lobby connection state: {state}");
    }

    // ─── Instance helpers ─────────────────────────────────────────────────────
    private static bool _alreadyAttemptedJoin = false;
    [SerializeField] private static GameObject _gameSessionContainer; // Will be set by OnEnable

    private static async Task CheckAndJoinRelay()
    {
        if (currentLobby == null || IsHost()) return;
        
        if (_alreadyAttemptedJoin) return;
        
        if (!currentLobby.Data.ContainsKey("RelayJoinCode"))
        {
            return;
        }

        _alreadyAttemptedJoin = true;
        
        string relayJoinCode = currentLobby.Data["RelayJoinCode"].Value;
        string catcherPlayerId = currentLobby.Data["CatcherPlayerId"].Value;

        Debug.Log($"[Client] Received relay join code: {relayJoinCode}");

        if (GameSessionData.Instance == null)
        {
            GameObject gameSessionDataObj = new GameObject("GameSessionData");
            gameSessionDataObj.AddComponent<GameSessionData>();
        }

        GameSessionData.Instance.CatcherPlayerId = catcherPlayerId;
        GameSessionData.Instance.IsRelayHost = false;

        try
        {
            Debug.Log("[Client] Joining relay...");
            await RelayManager.JoinRelay(relayJoinCode);

            // ── Activate UI before connecting ──────────────────────────────────
            // Netcode_Functions lives inside gameSessionContainer.
            // It must be active BEFORE StartClient() so that OnNetworkSpawn
            // fires into a fully initialised scene object.
            var instance = FindObjectOfType<LobbyFeatures>();
            if (instance != null)
            {
                if (instance.lobbyUIContainer != null)
                    instance.lobbyUIContainer.SetActive(false);

                if (instance.gameSessionContainer != null)
                {
                    Debug.Log("[Client] Activating game session...");
                    instance.gameSessionContainer.SetActive(true);
                }
            }

            // Small stabilisation wait after relay join + UI setup
            Debug.Log("[Client] Waiting for transport to stabilize...");
            await System.Threading.Tasks.Task.Delay(1000);

            Debug.Log("[Client] Starting client connection...");
            if (NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("[Client] NetworkManager is already listening — skipping StartClient.");
            }
            else
            {
                NetworkManager.Singleton.StartClient();
                Debug.Log("[Client] Client started successfully");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Client] Error joining relay: {ex.Message}\n{ex.StackTrace}");
            _alreadyAttemptedJoin = false;
        }
    }
    private void HandleLobbyGone(string message)
    {
        StopHeartbeat();
        if (debugText != null) debugText.text = message;
        lobbyFunctions?.ActivatePanel(FirstPanel);
    }

    // ─── Heartbeat ────────────────────────────────────────────────────────────

    private IEnumerator HeartbeatCoroutine()
    {
        var wait = new WaitForSecondsRealtime(HeartbeatInterval);
        while (currentLobby != null)
        {
            yield return wait;
            if (currentLobby == null) yield break;

            bool done = false;
            SendHeartbeatAsync(currentLobby.Id, () => done = true);
            yield return new WaitUntil(() => done);
        }
    }

    private async void SendHeartbeatAsync(string lobbyId, System.Action onDone)
    {
        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"[LobbyFeatures] Heartbeat failed: {e.Message}");
        }
        finally
        {
            onDone?.Invoke();
        }
    }

    private void StopHeartbeat()
    {
        if (_heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = null;
        }
    }

    // ─── Display ──────────────────────────────────────────────────────────────

    private void ShowLobbyInfo()
    {
        if (currentLobby == null) return;

        lobbyNameForDisplayText.text = currentLobby.Name;
        lobbyJoinCodeForDisplayText.text = currentLobby.LobbyCode;
        DeleteLobbyBtn.SetActive(IsHost());
        if (startGameButton != null)
            startGameButton.SetActive(IsHost());
        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
        if (currentLobby == null) return;

        foreach (Transform child in verticalLayoutGroupForPlayerInfo)
            Destroy(child.gameObject);

        foreach (Player player in currentLobby.Players)
        {
            GameObject playerInfo = Instantiate(playerInfoPrefab, verticalLayoutGroupForPlayerInfo);
            playerInfo.transform.Find("PlayerIDText").GetComponent<TextMeshProUGUI>().text = player.Id;
            playerInfo.transform.Find("PlayerNameText").GetComponent<TextMeshProUGUI>().text
                = player.Data != null && player.Data.ContainsKey("PlayerName")
                ? player.Data["PlayerName"].Value : "Unknown";

            Button kickOutBtn = playerInfo.transform.Find("KickOutBtn")?.GetComponent<Button>();
            if (kickOutBtn != null)
            {
                if (!IsHost())
                {
                    kickOutBtn.gameObject.SetActive(false);
                }
                else
                {
                    string capturedId = player.Id;
                    kickOutBtn.onClick.AddListener(() => KickOutPlayerAsync(capturedId));
                }
            }
        }
    }

    // ─── Static accessors ─────────────────────────────────────────────────────

    public static Lobby GetCurrentLobby() => currentLobby;

    public static void SetCurrentLobby(Lobby newCurrentLobby)
    {
        currentLobby = newCurrentLobby;
        // Reset subscription flag so the next lobby create/join subscribes fresh.
        if (newCurrentLobby == null)
            _isSubscribed = false;
    }

    public static bool IsHost()
    {
        if (currentLobby == null) return false;
        return AuthenticationService.Instance.PlayerId == currentLobby.HostId;
    }

    async void KickOutPlayerAsync(String playerId)
    {
        if (!IsHost()) { debugText.text = "Only host can kick players out"; return; }
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, playerId);
            debugText.text = "Kicked out player";
        }
        catch (LobbyServiceException e)
        {
            debugText.text = e.Message;
        }
    }
    public async void StartGame()
    {
        if (!IsHost()) return;
        startGameButton.GetComponent<Button>().interactable = false;
        if (gameSessionContainer == null)
        {
            Debug.LogError("[Host] gameSessionContainer not assigned!");
            return;
        }
        if (lobbyUIContainer == null)
        {
            Debug.LogError("[Host] lobbyUIContainer not assigned!");
            return;
        }

        var players = currentLobby.Players;
        string catcherPlayerId = players[UnityEngine.Random.Range(0, players.Count)].Id;

        int maxPlayers = currentLobby.MaxPlayers;

        Debug.Log("[Host] Creating relay allocation...");
        string relayJoinCode = await RelayManager.CreateRelayAndGetJoinCode(maxPlayers);
        if (relayJoinCode == null)
        {
            Debug.LogError("[Host] Failed to create relay");
            return;
        }

        GameSessionData.Instance.CatcherPlayerId = catcherPlayerId;
        GameSessionData.Instance.IsRelayHost = true;

        // ── STEP 1: Activate game session BEFORE StartHost ─────────────────────
        // This ensures Netcode_Functions.OnNetworkSpawn() registers the
        // ConnectionApprovalCallback BEFORE any client attempts to connect.
        Debug.Log("[Host] Activating game session container...");
        lobbyUIContainer.SetActive(false);
        gameSessionContainer.SetActive(true);

        // ── STEP 2: Start host (approval callback is now ready) ─────────────────
        Debug.Log("[Host] Starting host...");
        NetworkManager.Singleton.StartHost();

        // ── STEP 3: Wait for the host to fully initialise ───────────────────────
        Debug.Log("[Host] Host started, waiting for full initialisation...");
        await System.Threading.Tasks.Task.Delay(2000);

        // ── STEP 4: Only NOW broadcast the relay code so clients connect AFTER
        //           the host approval callback is guaranteed to be registered ───
        Debug.Log("[Host] Broadcasting relay join code to clients...");
        await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
        {
            Data = new Dictionary<string, DataObject>
            {
                { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                { "CatcherPlayerId", new DataObject(DataObject.VisibilityOptions.Member, catcherPlayerId) }
            }
        });

        Debug.Log("[Host] Game started!");
        
        // Give players time to spawn and receive the player object from NetworkManager
        await System.Threading.Tasks.Task.Delay(1000);
        
        // NOW spawn the actual gameplay prefabs (Catcher/Switcher)
        //SpawnGameplayPrefabs();
    }

    private void SpawnGameplayPrefabs()
    {
        Debug.Log("[Host] Spawning gameplay prefabs...");
        // Instantiate and configure Catcher/Switcher prefabs based on GameSessionData
        NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(PlayerPrefab);
    }
}