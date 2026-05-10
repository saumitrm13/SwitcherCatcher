using System.Collections.Generic;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.HID;
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

    private static Lobby currentLobby;
    private ILobbyEvents _lobbyEvents;                    // ← store reference for unsubscribing
    private LobbyEventCallbacks _lobbyEventCallbacks;     // ← store reference for unsubscribing

    // ─── Subscribe when this panel becomes active ────────────────────────────
    private async void OnEnable()
    {
        ShowLobbyInfo();

        if (currentLobby != null)
            await SubscribeToLobbyEvents();   // ← assign events here, after lobby is confirmed
    }

    // ─── Unsubscribe when this panel is hidden/destroyed ─────────────────────
    private async void OnDisable()
    {
        if (_lobbyEvents != null)
        {
            await _lobbyEvents.UnsubscribeAsync();
            _lobbyEvents = null;
        }
    }

    // ─── Event subscription setup ─────────────────────────────────────────────
    private async System.Threading.Tasks.Task SubscribeToLobbyEvents()
    {
        _lobbyEventCallbacks = new LobbyEventCallbacks();

        _lobbyEventCallbacks.LobbyChanged += OnLobbyChanged;
        _lobbyEventCallbacks.LobbyDeleted += OnLobbyDeleted;
        _lobbyEventCallbacks.DataChanged += OnLobbyDataChanged;
        _lobbyEventCallbacks.DataAdded += OnLobbyDataAdded;
        _lobbyEventCallbacks.DataRemoved += OnLobbyDataRemoved;
        _lobbyEventCallbacks.PlayerJoined += OnPlayerJoined;
        _lobbyEventCallbacks.PlayerLeft += OnPlayerLeft;
        _lobbyEventCallbacks.PlayerDataChanged += OnPlayerDataChanged;
        _lobbyEventCallbacks.PlayerDataAdded += OnPlayerDataAdded;
        _lobbyEventCallbacks.PlayerDataRemoved += OnPlayerDataRemoved;
        _lobbyEventCallbacks.KickedFromLobby += OnKickedFromLobby;
        _lobbyEventCallbacks.LobbyEventConnectionStateChanged += OnConnectionStateChanged;

        _lobbyEvents = await LobbyService.Instance.SubscribeToLobbyEventsAsync(currentLobby.Id, _lobbyEventCallbacks);
    }

    // ─── Handlers ─────────────────────────────────────────────────────────────
    private void OnLobbyChanged(ILobbyChanges changes)
    {
        changes.ApplyToLobby(currentLobby);
        ShowLobbyInfo();   // ← refresh the whole display on any lobby change
    }

    private void OnLobbyDeleted()
    {
        Debug.Log("Lobby deleted.");
        debugText.text = "Your lobby was deleted";
        lobbyFunctions.ActivatePanel(FirstPanel);
        // e.g. return to main menu
    }

    private void OnLobbyDataChanged(Dictionary<string, ChangedOrRemovedLobbyValue<DataObject>> changes) => ShowLobbyInfo();
    private void OnLobbyDataAdded(Dictionary<string, ChangedOrRemovedLobbyValue<DataObject>> added) => ShowLobbyInfo();
    private void OnLobbyDataRemoved(Dictionary<string, ChangedOrRemovedLobbyValue<DataObject>> removed) => ShowLobbyInfo();

    private void OnPlayerJoined(List<LobbyPlayerJoined> joined) => RefreshPlayerList();
    private void OnPlayerLeft(List<int> leftIndexes) => RefreshPlayerList();
    
    private void OnPlayerDataChanged(Dictionary<int, Dictionary<string, ChangedOrRemovedLobbyValue<PlayerDataObject>>> changes) => RefreshPlayerList();
    private void OnPlayerDataAdded(Dictionary<int, Dictionary<string, ChangedOrRemovedLobbyValue<PlayerDataObject>>> added) => RefreshPlayerList();
    private void OnPlayerDataRemoved(Dictionary<int, Dictionary<string, ChangedOrRemovedLobbyValue<PlayerDataObject>>> removed) => RefreshPlayerList();

    private void OnKickedFromLobby()
    {
        Debug.Log("Kicked from lobby.");
        debugText.text = "Either your lobby was deleted or you were kicked out";
        lobbyFunctions.ActivatePanel(FirstPanel);
        // e.g. return to main menu
    }

    private void OnConnectionStateChanged(LobbyEventConnectionState state)
    {
        Debug.Log($"Lobby connection state: {state}");
    }

    // ─── Display ───────────────────────────────────────────────────────────────
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
        // Clear old entries first to avoid duplicates
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
            if(kickOutBtn != null)
            {
                if (!IsHost())
                {
                    kickOutBtn.gameObject.SetActive(false);
                }
                else
                {
                    kickOutBtn.onClick.AddListener(() => { KickOutPlayerAsync(player.Id); } );
                }
            }
        }

        
    }

    // ─── Static accessors ──────────────────────────────────────────────────────
    public static Lobby GetCurrentLobby() => currentLobby;
    public static void SetCurrentLobby(Lobby newCurrentLobby) => currentLobby = newCurrentLobby;

    public static bool IsHost()
    {
        if(currentLobby == null) return false;
        string playerId = AuthenticationService.Instance.PlayerId;
        return playerId == currentLobby.HostId;
    }

    async void KickOutPlayerAsync(string playerId)
    {
        if (IsHost())
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, playerId);
                debugText.text = "Kicked out player";
            }
            catch(LobbyServiceException e) 
            {
                debugText.text = e.Message;
            }
        }
        else
        {
            debugText.text = "Only host can kick players out";
        }
    }
}