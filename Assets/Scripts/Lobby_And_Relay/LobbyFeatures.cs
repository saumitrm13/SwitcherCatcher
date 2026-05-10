using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyFeatures : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI lobbyNameForDisplayText;
    [SerializeField] private TextMeshProUGUI lobbyJoinCodeForDisplayText;
    [SerializeField] private GameObject playerInfoPrefab;
    [SerializeField] private RectTransform verticalLayoutGroupForPlayerInfo;
    [SerializeField] private GameObject DeleteLobbyBtn;
    [SerializeField] private LobbyCanvasFunction lobbyFunctions;
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private GameObject FirstPanel;

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
        ShowLobbyInfo();

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

    private static void OnLobbyChangedStatic(ILobbyChanges changes)
    {
        if (currentLobby == null) return;
        changes.ApplyToLobby(currentLobby);
        FindAnyObjectByType<LobbyFeatures>()?.ShowLobbyInfo();
    }

    private static void OnLobbyDeletedStatic()
    {
        Debug.Log("[LobbyFeatures] Lobby deleted.");
        _ = UnsubscribeFromCurrentLobbyEvents();
        SetCurrentLobby(null);
        FindAnyObjectByType<LobbyFeatures>()?.HandleLobbyGone("Your lobby was deleted");
    }

    private static void OnDataChangedStatic(
        Dictionary<string, ChangedOrRemovedLobbyValue<DataObject>> _)
        => FindAnyObjectByType<LobbyFeatures>()?.ShowLobbyInfo();

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

    async void KickOutPlayerAsync(string playerId)
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
}